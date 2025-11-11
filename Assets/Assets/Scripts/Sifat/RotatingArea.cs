using UnityEngine;

/// <summary>
/// Area rotasi serbaguna:
///  - SelfRotate: transform ini muter; posisi anak ikut berputar, TAPI (opsional) rotasi anak dikunci agar tetap tegak
///  - OrbitChildren: anak-anak mengorbit titik pusat
///  - SweepPingPong: rotasi bolak-balik antara dua sudut
/// </summary>
public class RotatingArea : MonoBehaviour
{
    public enum Mode { SelfRotate, OrbitChildren, SweepPingPong }

    [Header("Mode")]
    public Mode mode = Mode.SelfRotate;

    [Header("Umum")]
    [Tooltip("Kalikan kecepatan dengan MudMoveSpeedMultiplier (disarankan ON).")]
    public bool useMudMultiplier = true;

    [Tooltip("Kalau OFF, objek tidak bergerak ketika belum Play.")]
    public bool simulateInEditor = false;

    /* ---------- Self Rotate ---------- */
    [Header("Self Rotate")]
    [Tooltip("Kecepatan rotasi (derajat/detik). Positif = searah jarum jam di tampilan 2D (sumbu Z menurun).")]
    public float angularSpeed = 45f;

    [Tooltip("Kunci rotasi anak-objek agar sprite tetap tegak (tidak ikut memutar).")]
    public bool keepChildrenUpright = true;

    /* ---------- Orbit Children ---------- */
    [Header("Orbit Children")]
    [Tooltip("Jika 0 atau negatif, radius tiap anak = jarak awalnya dari pusat.")]
    public float orbitRadius = 0f;
    [Tooltip("Kecepatan sudut orbit (derajat/detik).")]
    public float orbitAngularSpeed = 30f;
    [Tooltip("Offset sudut awal per anak (derajat).")]
    public float childAngleOffset = 0f;
    [Tooltip("Orientasi anak menghadap arah tangensial orbit.")]
    public bool orientTangential = false;

    /* ---------- Sweep Ping-Pong ---------- */
    [Header("Sweep Ping-Pong")]
    [Tooltip("Sumbu rotasi: Local = rotasi lokal Z; World = absolut di world.")]
    public Space sweepSpace = Space.Self;
    [Tooltip("Sudut awal (derajat).")]
    public float sweepStartAngle = -35f;
    [Tooltip("Sudut akhir (derajat).")]
    public float sweepEndAngle = 35f;
    [Tooltip("Kecepatan bolak-balik (derajat/detik).")]
    public float sweepSpeed = 60f;
    [Tooltip("Kurva easing untuk 0..1 → 0..1 di antara start↔end.")]
    public AnimationCurve sweepEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Mulai dari ujung mana (true = mulai dari end).")]
    public bool startFromEnd = false;

    // runtime cache
    Transform[] _children;
    float[] _baseAngles;                 // untuk OrbitChildren
    Vector3 _center;
    float _orbitAccumDeg;

    // simpan rotasi awal anak (world) supaya bisa dikunci saat SelfRotate
    Quaternion[] _childInitialWorldRot;

    // runtime untuk SweepPingPong
    float _sweepT;
    int _sweepDir = 1;
    float _currentAngle;

    void Awake()
    {
        CacheChildren();
        InitSweep();
    }

    void Start()
    {
        CacheChildren();
        InitSweep();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheChildren();
            InitSweep();
            if (mode == Mode.SweepPingPong)
                ApplySweepAngle(GetSweepAngleAt(0f), immediate: true);
        }
    }
#endif

    void OnTransformChildrenChanged() => CacheChildren();

    void CacheChildren()
    {
        int n = transform.childCount;
        _children = new Transform[n];
        _baseAngles = new float[n];
        _childInitialWorldRot = new Quaternion[n];
        _center = transform.position;

        for (int i = 0; i < n; i++)
        {
            var c = transform.GetChild(i);
            _children[i] = c;

            // untuk OrbitChildren
            Vector2 v = (Vector2)(c.position - _center);
            float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            _baseAngles[i] = ang;

            // untuk SelfRotate (kunci rotasi anak)
            _childInitialWorldRot[i] = c.rotation;
        }
    }

    void InitSweep()
    {
        _sweepT = startFromEnd ? 1f : 0f;
        _sweepDir = startFromEnd ? -1 : 1;
        _currentAngle = GetSweepAngleAt(_sweepT);
    }

    void Update()
    {
        if (!Application.isPlaying && !simulateInEditor) return;

        float dt = Time.deltaTime;
        float mul = (useMudMultiplier ? GetMudMultiplierSafe() : 1f);

        switch (mode)
        {
            case Mode.SelfRotate:
                {
                    float w = angularSpeed * mul;
                    // putar parent
                    transform.Rotate(0f, 0f, w * dt, Space.Self);

                    // kunci sprite anak agar tidak ikut “memutar”
                    if (keepChildrenUpright && _children != null)
                    {
                        for (int i = 0; i < _children.Length; i++)
                        {
                            var c = _children[i];
                            if (!c) continue;
                            c.rotation = _childInitialWorldRot[i];
                        }
                    }
                    break;
                }

            case Mode.OrbitChildren:
                {
                    DoOrbitChildren(dt, mul);
                    break;
                }

            case Mode.SweepPingPong:
                {
                    DoSweepPingPong(dt, mul);
                    break;
                }
        }
    }

    /* ================= Orbit Children ================= */
    void DoOrbitChildren(float dt, float mul)
    {
        if (_children == null || _children.Length == 0) return;

        float w = orbitAngularSpeed * mul; // deg/s
        _orbitAccumDeg += w * dt;

        float rSetting = orbitRadius;
        for (int i = 0; i < _children.Length; i++)
        {
            var c = _children[i];
            if (!c) continue;

            float baseAng = _baseAngles[i] + childAngleOffset;
            float ang = baseAng + _orbitAccumDeg;
            float r = rSetting > 0f ? rSetting : Vector3.Distance(c.position, _center);

            float rad = ang * Mathf.Deg2Rad;
            Vector3 target = _center + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * r;
            c.position = target;

            if (orientTangential)
                c.rotation = Quaternion.Euler(0f, 0f, ang + 90f);
        }
    }

    /* ================= Sweep Ping-Pong ================= */
    void DoSweepPingPong(float dt, float mul)
    {
        float span = Mathf.DeltaAngle(0f, sweepEndAngle - sweepStartAngle);
        float absSpan = Mathf.Abs(span);
        if (absSpan < 0.0001f) return;

        float tSpeed = (absSpan <= 0f) ? 0f : (sweepSpeed * mul) / absSpan;

        _sweepT += _sweepDir * tSpeed * dt;

        if (_sweepT >= 1f) { _sweepT = 1f; _sweepDir = -1; }
        else if (_sweepT <= 0f) { _sweepT = 0f; _sweepDir = 1; }

        float ang = GetSweepAngleAt(_sweepT);
        ApplySweepAngle(ang, immediate: false);
    }

    float GetSweepAngleAt(float tClamped01)
    {
        float t = Mathf.Clamp01(tClamped01);
        float eased = sweepEase != null ? sweepEase.Evaluate(t) : t;
        return Mathf.Lerp(sweepStartAngle, sweepEndAngle, eased);
    }

    void ApplySweepAngle(float angleDeg, bool immediate)
    {
        _currentAngle = angleDeg;

        if (sweepSpace == Space.Self)
            transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        else
        {
            Vector3 e = transform.eulerAngles;
            e.z = angleDeg;
            transform.eulerAngles = e;
        }
    }

    /* ================= Helpers ================= */
    float GetMudMultiplierSafe()
    {
        try { return ReactionExecutorV2.MudMoveSpeedMultiplier; }
        catch { return 1f; }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (mode == Mode.SweepPingPong)
        {
            Gizmos.color = new Color(1f, .8f, .2f, .7f);
            Vector3 p = transform.position;
            float r = 0.75f;

            Vector3 dirA = DirFromDeg(sweepStartAngle);
            Vector3 dirB = DirFromDeg(sweepEndAngle);
            Gizmos.DrawLine(p, p + dirA * r);
            Gizmos.DrawLine(p, p + dirB * r);

            Gizmos.color = new Color(.3f, 1f, .6f, .9f);
            Vector3 dirC = DirFromDeg(_currentAngle);
            Gizmos.DrawLine(p, p + dirC * r);
        }
    }

    Vector3 DirFromDeg(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
    }
#endif
}
