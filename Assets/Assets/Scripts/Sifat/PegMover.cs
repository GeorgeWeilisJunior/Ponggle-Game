using UnityEngine;

/// <summary>
/// Gerakkan peg bolak-balik (ping-pong) dari posisi awal ke arah tertentu
/// atau sepanjang jalur waypoint. Kecepatan ikut tertahan oleh Mud Rain
/// lewat ReactionExecutorV2.MudMoveSpeedMultiplier.
/// </summary>
public class PegMover : MonoBehaviour
{
    public enum MoveMode { PingPongFromStart, Waypoints }
    public enum Axis { World, Local }

    [Header("Mode")]
    public MoveMode mode = MoveMode.PingPongFromStart;

    [Header("Ping-Pong")]
    [Tooltip("Gunakan arah world atau local untuk offset.")]
    public Axis axis = Axis.Local;

    [Tooltip("Arah + jarak dari titik awal (X,Y).")]
    public Vector2 offset = new Vector2(2f, 0f);

    [Tooltip("Kurva gerak 0..1 → 0..1 (opsional).")]
    public AnimationCurve ease = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Waypoints (opsional)")]
    [Tooltip("Jika kosong, akan diisi otomatis dgn posisi sekarang & posisi sekarang + offset (world-space).")]
    public Transform[] waypoints;

    [Tooltip("Apakah looping di ujung (0→1→2→…→0). Jika OFF, bolak-balik 0→1→…→2→1→…")]
    public bool loop = false;

    [Header("Kecepatan")]
    [Tooltip("Satuan unit/detik di lintasan.")]
    public float moveSpeed = 2.2f;

    [Tooltip("Kalikan kecepatan dengan MudMoveSpeedMultiplier (disarankan ON).")]
    public bool useMudMultiplier = true;

    [Header("Gizmo")]
    public Color gizmoColor = new Color(0.4f, 0.8f, 1f, 0.7f);
    public float gizmoRadius = 0.12f;

    [Header("Editor Preview")]
    public bool simulateInEditor = false;

    // runtime
    Vector3 _startPos;
    int _segFrom, _segTo;    // indeks segmen aktif (waypoints)
    int _dir = 1;            // arah segmen untuk ping-pong waypoints
    float _tOnSegment = 0f;  // 0..1 progres di segmen aktif
    bool _initialized;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            _startPos = transform.position;
            _initialized = false;
        }
    }
#endif

    void Awake()
    {
        _startPos = transform.position;
    }

    void Start()
    {
        InitIfNeeded();
    }

    void InitIfNeeded()
    {
        if (_initialized) return;

        if (mode == MoveMode.Waypoints)
        {
            // Jika tak ada waypoint, isi 2 titik default dari posisi sekarang
            if (waypoints == null || waypoints.Length == 0)
            {
                waypoints = new Transform[2];
                var a = new GameObject(name + "_wp0").transform;
                var b = new GameObject(name + "_wp1").transform;

                a.position = transform.position;
                b.position = transform.position + GetAxisOffsetVector(); // ← FIX

                a.SetParent(transform.parent, true);
                b.SetParent(transform.parent, true);
                waypoints[0] = a;
                waypoints[1] = b;
            }
            else if (waypoints.Length == 1)
            {
                // Pastikan minimal 2 titik
                var b = new GameObject(name + "_wpExtra").transform;
                b.position = waypoints[0].position + GetAxisOffsetVector(); // ← FIX
                b.SetParent(transform.parent, true);

                var arr = new Transform[2];
                arr[0] = waypoints[0];
                arr[1] = b;
                waypoints = arr;
            }

            _segFrom = 0;
            _segTo = (waypoints.Length > 1) ? 1 : 0;
            _dir = 1;
            _tOnSegment = 0f;
            transform.position = waypoints[0].position;
        }
        else
        {
            transform.position = _startPos;
        }

        _initialized = true;
    }

    void Update()
    {
        if (!Application.isPlaying && !simulateInEditor) return;
        InitIfNeeded();

        float dt = Application.isPlaying ? Time.deltaTime : 1f / 60f;
        float spd = Mathf.Max(0f, moveSpeed);

        // Terapkan Mud Rain multiplier (hanya untuk entitas non-bola)
        if (useMudMultiplier)
            spd *= GetMudMultiplierSafe();

        if (mode == MoveMode.PingPongFromStart)
        {
            DoPingPong(dt, spd);
        }
        else
        {
            DoWaypoints(dt, spd);
        }
    }

    void DoPingPong(float dt, float spd)
    {
        // vektor perpindahan (world-space) dari start ke target
        Vector3 disp = GetAxisOffsetVector();              // ← FIX: langsung vektor offset
        float dist = disp.magnitude;
        if (dist < 0.0001f || spd <= 0f) return;

        // waktu satu perjalanan (pergi) = dist / spd
        float tOne = dist / spd;

        // Ping-pong 0..1..0
        float raw = Mathf.PingPong(Time.time / Mathf.Max(0.0001f, tOne), 1f);
        float f = ease.Evaluate(raw);

        // start + disp * f (bukan mengalikan Vector3 × Vector3)
        transform.position = _startPos + disp * f;         // ← FIX
    }

    void DoWaypoints(float dt, float spd)
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Vector3 a = waypoints[_segFrom].position;
        Vector3 b = waypoints[_segTo].position;
        float segLen = Vector3.Distance(a, b);
        if (segLen < 0.0001f)
        {
            AdvanceSegment(); // lompat segmen jika sama
            return;
        }

        float dv = (spd * dt) / segLen; // progress 0..1
        _tOnSegment += dv;

        if (_tOnSegment >= 1f)
        {
            // Pindah ke segmen berikutnya, carry sisa t
            _tOnSegment -= 1f;
            AdvanceSegment();
            // posisi ulang point a/b berdasarkan segmen baru
            a = waypoints[_segFrom].position;
            b = waypoints[_segTo].position;
            segLen = Vector3.Distance(a, b);
            if (segLen < 0.0001f) return;
        }

        float f = ease.Evaluate(Mathf.Clamp01(_tOnSegment));
        transform.position = Vector3.LerpUnclamped(a, b, f);
    }

    void AdvanceSegment()
    {
        if (loop)
        {
            _segFrom = _segTo;
            _segTo = (_segTo + 1) % waypoints.Length;
            return;
        }

        // ping-pong
        int next = _segTo + _dir;
        if (next < 0 || next >= waypoints.Length)
        {
            _dir *= -1; // balik arah
            next = _segTo + _dir;
        }
        _segFrom = _segTo;
        _segTo = next;
    }

    /// <summary>
    /// Menghasilkan vektor perpindahan world-space sesuai setting axis+offset.
    /// </summary>
    Vector3 GetAxisOffsetVector()
    {
        Vector3 off3 = new Vector3(offset.x, offset.y, 0f);
        if (off3.sqrMagnitude < 1e-8f) return Vector3.zero;

        // Local: transform.TransformVector → konversi ke world-space
        // World: pakai apa adanya
        return (axis == Axis.Local) ? transform.TransformVector(off3) : off3;
    }

    float GetMudMultiplierSafe()
    {
        // aman jika class belum ada / null (misal di editor)
        try
        {
            return ReactionExecutorV2.MudMoveSpeedMultiplier;
        }
        catch { return 1f; }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;

        if (mode == MoveMode.PingPongFromStart)
        {
            Vector3 start = Application.isPlaying ? _startPos : transform.position;
            Vector3 disp = GetAxisOffsetVector();
            Vector3 end = start + disp;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(start, gizmoRadius);
            Gizmos.DrawSphere(end, gizmoRadius);
        }
        else
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                for (int i = 0; i < waypoints.Length; i++)
                {
                    if (!waypoints[i]) continue;
                    Gizmos.DrawSphere(waypoints[i].position, gizmoRadius);
                    if (i < waypoints.Length - 1 && waypoints[i + 1])
                        Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                    else if (loop && i == waypoints.Length - 1 && waypoints[0])
                        Gizmos.DrawLine(waypoints[i].position, waypoints[0].position);
                }
            }
        }
    }
#endif
}
