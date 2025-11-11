// RoundedPegGenerator.cs — FULL (with AntiGravity Prefab swap + Disappearing editor gizmo)
using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class RoundedPegGenerator : MonoBehaviour
{
    public enum Pattern { Wave, CircleOrArc, Line, Grid, Spiral, HelixRings, ConcentricRings, ZigZagVertical }

    [Header("Prefabs (rounded pegs)")]
    [SerializeField] GameObject bluePegPrefab;
    [SerializeField] GameObject orangePegPrefab;

    [Header("Pattern")]
    [SerializeField] Pattern pattern = Pattern.Wave;

    /* ───────── Wave Settings ───────── */
    [SerializeField] int pegCount = 20;
    [SerializeField] Vector2 startPos = new(-8f, 0f);
    [SerializeField] float spacingX = 1f;
    [SerializeField] float amplitude = 2f;
    [SerializeField] float frequency = 1f;
    [SerializeField] float phase = 0f;

    [Header("Wave Rows (optional)")]
    [SerializeField] int rows = 1;
    [SerializeField] float rowOffsetY = -1.2f;
    [SerializeField] float rowPhaseOffset = 0.5f;

    /* ───────── Circle / Arc Settings ───────── */
    [SerializeField] Vector2 center = new(0f, 0f);
    [SerializeField] float radius = 6f;
    [SerializeField] int ringCount = 24;
    [SerializeField, Range(0f, 360f)] float startAngle = 0f;
    [SerializeField, Range(0f, 360f)] float endAngle = 360f;
    [SerializeField] bool clockwise = false;

    /* ───────── Line Settings ───────── */
    [SerializeField] Vector2 lineStart = new(0f, 5f);
    [SerializeField] Vector2 lineEnd = new(0f, -5f);
    [SerializeField] int lineCount = 12;

    /* ───────── Grid Settings ───────── */
    [SerializeField] Vector2 gridOrigin = new(-7f, 3f);
    [SerializeField, Min(1)] int gridCols = 10;
    [SerializeField, Min(1)] int gridRows = 5;
    [SerializeField] float gridSpacingX = 1.4f;
    [SerializeField] float gridSpacingY = 1.2f;
    [SerializeField] bool gridStaggerOddRows = false;

    /* ───────── Spiral Settings ───────── */
    [SerializeField] Vector2 spiralCenter = new(0f, 0f);
    [SerializeField] float spiralStartRadius = 2f;
    [SerializeField] float spiralEndRadius = 7f;
    [SerializeField] float spiralTurns = 2.5f;
    [SerializeField, Min(1)] int spiralCount = 40;
    [SerializeField] bool spiralClockwise = true;

    /* ───────── HelixRings (NEW) ───────── */
    [Header("Helix Rings (NEW)")]
    [SerializeField] Vector2 helixCenter = new(0f, 0f);
    [SerializeField, Min(1)] int helixLoops = 5;
    [SerializeField, Min(3)] int helixPointsPerRing = 18;
    [SerializeField] float helixRadius = 2.2f;
    [SerializeField] float helixSpacingY = 2.2f;
    [SerializeField, Range(0f, 180f)] float helixGapDeg = 60f;
    [SerializeField] int helixBridgeCount = 3;
    [SerializeField] bool helixAlternateSide = true;

    /* ───────── ConcentricRings (NEW) ───────── */
    [Header("Concentric Rings (NEW)")]
    [SerializeField] Vector2 concentricCenter = new(0f, 0f);
    [SerializeField, Min(1)] int concentricRings = 3;
    [SerializeField] float concentricStartRadius = 1.5f;
    [SerializeField] float concentricRadiusStep = 1.2f;
    [SerializeField, Min(3)] int concentricPointsPerRing = 24;
    [SerializeField, Range(0f, 360f)] float concentricArcStart = 0f;
    [SerializeField, Range(0f, 360f)] float concentricArcEnd = 360f;
    [SerializeField] bool concentricClockwise = false;

    /* ───────── ZigZagVertical (NEW) ───────── */
    [Header("ZigZag Vertical (NEW)")]
    [SerializeField] Vector2 zigStart = new(-4f, 4.5f);
    [SerializeField] float zigWidth = 8f;
    [SerializeField] float zigStepY = 1.2f;
    [SerializeField, Min(1)] int zigLegs = 8;
    [SerializeField, Min(2)] int zigPointsPerLeg = 4;

    [Header("Variation / Noise (optional)")]
    [SerializeField] float jitterXY = 0f;

    /* ───────── Orange Mix ───────── */
    public enum OrangeMode { None, RandomByPercent, EveryNth }
    [SerializeField] OrangeMode orangeMode = OrangeMode.None;
    [SerializeField, Range(0f, 1f)] float orangePercent = 0.2f;
    [SerializeField, Min(2)] int orangeEveryN = 5;
    [SerializeField] bool useSeed = false;
    [SerializeField] int seed = 12345;

    /* ───────── Movement (per Peg) ───────── */
    [Header("Movement (per Peg)")]
    public bool addPegMover = false;
    public PegMover.MoveMode moveMode = PegMover.MoveMode.PingPongFromStart;
    public PegMover.Axis moveAxis = PegMover.Axis.Local;
    public Vector2 moveOffset = new Vector2(2f, 0f);
    public float moveSpeed = 2.2f;
    public bool moveLoop = false;
    public bool moveUseMudMultiplier = true;
    public AnimationCurve moveEase = null;

    /* ───────── Rotation (wrap group) ───────── */
    [Header("Rotation (wrap all pegs)")]
    public bool wrapWithRotatingArea = false;
    public RotatingArea.Mode rotatingMode = RotatingArea.Mode.SelfRotate;
    public bool rotatingUseMudMultiplier = true;
    public bool rotatingSimulateInEditor = false;

    // SelfRotate
    public float angularSpeed = 45f;

    // OrbitChildren
    public float orbitRadius = 0f;
    public float orbitAngularSpeed = 30f;
    public float childAngleOffset = 0f;
    public bool orientTangential = false;

    // SweepPingPong
    public Space sweepSpace = Space.Self;
    public float sweepStartAngle = -35f;
    public float sweepEndAngle = 35f;
    public float sweepSpeed = 60f;
    public AnimationCurve sweepEase = null;
    public bool sweepStartFromEnd = false;

    /* ───────── NEW: Special Traits Attach ───────── */
    public enum AssignMode { None, All, RandomByPercent, EveryNth }

    [Header("Anti-Gravity (attach AntiGravityPeg)")]
    public AssignMode antiGravityMode = AssignMode.None;
    [Range(0f, 1f)] public float antiGravityPercent = 0.25f;
    [Min(2)] public int antiGravityEveryN = 6;

    // Prefab override with child effect
    [Header("Anti-Gravity Prefab Override (optional)")]
    public bool useAntiGravityPrefab = true;
    public GameObject antiGravityBluePrefab;
    public GameObject antiGravityOrangePrefab;

    // Mirrors AntiGravityPeg fields
    public float ag_duration = 2.5f;
    public float ag_gravityScaleOverride = 0f;
    public float ag_dragOverride = 0.12f;
    [Range(0f, 1f)] public float ag_bouncinessOverride = 0f;
    public ParticleSystem ag_onHitVfx;
    public string ag_onHitSfxKey = "";

    [Header("Disappearing (attach DisappearingPeg)")]
    public AssignMode disappearingMode = AssignMode.None;
    [Range(0f, 1f)] public float disappearingPercent = 0.25f;
    [Min(2)] public int disappearingEveryN = 5;

    // Mirrors DisappearingPeg fields
    [Min(0f)] public float dsp_visibleDuration = 5f;
    [Min(0f)] public float dsp_hiddenDuration = 2f;
    [Min(0.05f)] public float dsp_fadeDuration = 0.35f;
    [Min(0f)] public float dsp_startDelay = 0f;
    public bool dsp_startHidden = false;
    [Min(0f)] public float dsp_randomPhaseJitter = 0.5f;
    public AnimationCurve dsp_fadeCurve = null;
    [Range(0f, 1f)] public float dsp_hiddenAlpha = 0f;
    public ParticleSystem dsp_appearVfxPrefab;
    public ParticleSystem dsp_disappearVfxPrefab;
    public string dsp_appearSfxKey = "";
    public string dsp_disappearSfxKey = "";

    [Header("Editor Preview")]
    [SerializeField] bool livePreview = false;
    [SerializeField, Range(0, 200)] int previewDelayMs = 60;

    bool _pendingRegen;
    Transform _spawnParent = null;

    void OnEnable() { _pendingRegen = false; }
    void OnValidate() { if (!isActiveAndEnabled) return; if (livePreview) QueueSafeRegenerate(); }

    void Update()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        if (_pendingRegen) { _pendingRegen = false; SafeRegenerate(); }
#endif
    }

    [ContextMenu("Regenerate")] public void Regenerate() => SafeRegenerate();

    [ContextMenu("Clear Children")]
    public void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
    }

    /* =========================================================
       SPAWN + ATTACH HELPERS
       ========================================================= */
    void SpawnPeg(
        System.Random rng,
        GameObject basePrefab,
        bool makeOrange,
        Vector3 pos,
        string name,
        int spawnIndex,
        bool assignAnti,
        bool assignDsp)
    {
        if (!basePrefab) return;

        // choose prefab (override when Anti-Gravity and override is provided)
        GameObject prefabToUse = basePrefab;
        if (assignAnti && useAntiGravityPrefab)
        {
            if (makeOrange && antiGravityOrangePrefab) prefabToUse = antiGravityOrangePrefab;
            else if (!makeOrange && antiGravityBluePrefab) prefabToUse = antiGravityBluePrefab;
        }

        Transform parent = _spawnParent ? _spawnParent : transform;
        var go = Instantiate(prefabToUse, pos, Quaternion.identity, parent);
        go.name = name;
        go.transform.localScale = prefabToUse.transform.localScale; // keep prefab scale

        // movement (optional)
        if (addPegMover) AttachMover(go);

        // special traits (optional)
        if (assignAnti) AttachAntiGravity(go);
        if (assignDsp) AttachDisappearing(go);
    }

    void AttachMover(GameObject go)
    {
        var mv = go.GetComponent<PegMover>();
        if (!mv) mv = go.AddComponent<PegMover>();
        mv.mode = moveMode;
        mv.axis = moveAxis;
        mv.offset = moveOffset;
        mv.moveSpeed = moveSpeed;
        mv.loop = moveLoop;
        mv.useMudMultiplier = moveUseMudMultiplier;
        mv.ease = moveEase ?? AnimationCurve.Linear(0, 0, 1, 1);
#if UNITY_EDITOR
        mv.simulateInEditor = !Application.isPlaying;
#endif
    }

    void AttachAntiGravity(GameObject go)
    {
        var ag = go.GetComponent<AntiGravityPeg>();
        if (!ag) ag = go.AddComponent<AntiGravityPeg>();
        ag.duration = ag_duration;
        ag.gravityScaleOverride = ag_gravityScaleOverride;
        ag.dragOverride = ag_dragOverride;
        ag.bouncinessOverride = ag_bouncinessOverride;
        ag.onHitVfx = ag_onHitVfx;
        ag.onHitSfxKey = ag_onHitSfxKey;
    }

    void AttachDisappearing(GameObject go)
    {
        var dp = go.GetComponent<DisappearingPeg>();
        if (!dp) dp = go.AddComponent<DisappearingPeg>();
        dp.visibleDuration = dsp_visibleDuration;
        dp.hiddenDuration = dsp_hiddenDuration;
        dp.fadeDuration = dsp_fadeDuration;
        dp.startDelay = dsp_startDelay;
        dp.startHidden = dsp_startHidden;
        dp.randomPhaseJitter = dsp_randomPhaseJitter;
        dp.fadeCurve = dsp_fadeCurve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
        dp.hiddenAlpha = dsp_hiddenAlpha;
        dp.appearVfxPrefab = dsp_appearVfxPrefab;
        dp.disappearVfxPrefab = dsp_disappearVfxPrefab;
        dp.appearSfxKey = dsp_appearSfxKey;
        dp.disappearSfxKey = dsp_disappearSfxKey;

        // Add editor-only gizmo marker
#if UNITY_EDITOR
        var marker = go.GetComponent<DisappearGizmoMarker>();
        if (!marker) marker = go.AddComponent<DisappearGizmoMarker>();
        if (!go.name.Contains("[DSP]")) go.name += " [DSP]";
#endif
    }

    bool ShouldAssign(System.Random rng, AssignMode mode, float percent, int everyN, int idx)
    {
        switch (mode)
        {
            case AssignMode.None: return false;
            case AssignMode.All: return true;
            case AssignMode.RandomByPercent:
                return (float)rng.NextDouble() < Mathf.Clamp01(percent);
            case AssignMode.EveryNth:
                return everyN > 0 && (idx + 1) % everyN == 0;
        }
        return false;
    }

    System.Random _rng;
    GameObject PickPrefabForIndex(bool makeOrange)
        => makeOrange ? (orangePegPrefab ? orangePegPrefab : bluePegPrefab) : bluePegPrefab;

    bool PickOrange(System.Random rng)
    {
        switch (orangeMode)
        {
            case OrangeMode.None: return false;
            case OrangeMode.EveryNth: return orangeEveryN > 0 && ((_spawnedSoFar + 1) % orangeEveryN == 0);
            case OrangeMode.RandomByPercent: return (float)rng.NextDouble() < Mathf.Clamp01(orangePercent);
        }
        return false;
    }

    Vector3 Jitter(System.Random rng)
    {
        if (jitterXY <= 0f) return Vector3.zero;
        float jx = ((float)rng.NextDouble() * 2f - 1f) * jitterXY;
        float jy = ((float)rng.NextDouble() * 2f - 1f) * jitterXY;
        return new Vector3(jx, jy, 0f);
    }

    bool CheckPrefabs()
    {
        if (!bluePegPrefab)
        {
            Debug.LogWarning("[RoundedPegGenerator] Blue prefab belum diisi.");
            return false;
        }
        return true;
    }

    int _spawnedSoFar;

    /* ====================== GENERATORS ====================== */
    void GenerateWave(System.Random rng)
    {
        int rCount = Mathf.Max(1, rows);
        int count = Mathf.Max(0, pegCount);
        _spawnedSoFar = 0;

        for (int r = 0; r < rCount; r++)
        {
            float rowY = startPos.y + r * rowOffsetY;
            float rowPhase = phase + r * rowPhaseOffset;
            for (int i = 0; i < count; i++)
            {
                float x = startPos.x + i * spacingX;
                float y = rowY + Mathf.Sin(i * frequency + rowPhase) * amplitude;

                bool makeOrange = PickOrange(rng);
                bool assignAnti = ShouldAssign(rng, antiGravityMode, antiGravityPercent, antiGravityEveryN, _spawnedSoFar);
                bool assignDsp = ShouldAssign(rng, disappearingMode, disappearingPercent, disappearingEveryN, _spawnedSoFar);

                var basePrefab = PickPrefabForIndex(makeOrange);
                SpawnPeg(rng, basePrefab, makeOrange, new Vector3(x, y, 0f) + Jitter(rng),
                    $"{nameof(Pattern.Wave)}_r{r:D2}_i{i:D2}", _spawnedSoFar, assignAnti, assignDsp);
                _spawnedSoFar++;
            }
        }
    }

    void GenerateCircleOrArc(System.Random rng)
    {
        int count = Mathf.Max(1, ringCount);
        _spawnedSoFar = 0;

        float a0 = Mathf.Deg2Rad * startAngle;
        float a1 = Mathf.Deg2Rad * endAngle;
        float sweep = a1 - a0;
        if (Mathf.Approximately(sweep, 0f)) sweep = Mathf.PI * 2f;
        if (clockwise && sweep > 0f) sweep = -sweep;

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : (float)i / (count - 1);
            float ang = a0 + sweep * t;
            float x = center.x + Mathf.Cos(ang) * radius;
            float y = center.y + Mathf.Sin(ang) * radius;

            bool makeOrange = PickOrange(rng);
            bool assignAnti = ShouldAssign(rng, antiGravityMode, antiGravityPercent, antiGravityEveryN, _spawnedSoFar);
            bool assignDsp = ShouldAssign(rng, disappearingMode, disappearingPercent, disappearingEveryN, _spawnedSoFar);

            var basePrefab = PickPrefabForIndex(makeOrange);
            SpawnPeg(rng, basePrefab, makeOrange, new Vector3(x, y, 0f) + Jitter(rng),
                $"{nameof(Pattern.CircleOrArc)}_{Mathf.RoundToInt(Mathf.Rad2Deg * ang)}", _spawnedSoFar, assignAnti, assignDsp);
            _spawnedSoFar++;
        }
    }

    void GenerateLine(System.Random rng)
    {
        int count = Mathf.Max(1, lineCount);
        _spawnedSoFar = 0;

        Vector2 a = lineStart;
        Vector2 b = lineEnd;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : (float)i / (count - 1);
            Vector2 p = Vector2.Lerp(a, b, t);

            bool makeOrange = PickOrange(rng);
            bool assignAnti = ShouldAssign(rng, antiGravityMode, antiGravityPercent, antiGravityEveryN, _spawnedSoFar);
            bool assignDsp = ShouldAssign(rng, disappearingMode, disappearingPercent, disappearingEveryN, _spawnedSoFar);

            var basePrefab = PickPrefabForIndex(makeOrange);
            SpawnPeg(rng, basePrefab, makeOrange, (Vector3)p + Jitter(rng),
                $"{nameof(Pattern.Line)}_{i:D2}", _spawnedSoFar, assignAnti, assignDsp);
            _spawnedSoFar++;
        }
    }

    void GenerateGrid(System.Random rng)
    {
        int cols = Mathf.Max(1, gridCols);
        int rowsLocal = Mathf.Max(1, gridRows);
        _spawnedSoFar = 0;

        for (int r = 0; r < rowsLocal; r++)
        {
            float rowOffset = (gridStaggerOddRows && (r % 2 == 1)) ? gridSpacingX * 0.5f : 0f;
            for (int c = 0; c < cols; c++)
            {
                float x = gridOrigin.x + c * gridSpacingX + rowOffset;
                float y = gridOrigin.y - r * gridSpacingY;

                bool makeOrange = PickOrange(rng);
                bool assignAnti = ShouldAssign(rng, antiGravityMode, antiGravityPercent, antiGravityEveryN, _spawnedSoFar);
                bool assignDsp = ShouldAssign(rng, disappearingMode, disappearingPercent, disappearingEveryN, _spawnedSoFar);

                var basePrefab = PickPrefabForIndex(makeOrange);
                SpawnPeg(rng, basePrefab, makeOrange, new Vector3(x, y, 0f) + Jitter(rng),
                    $"{nameof(Grid)}_r{r:D2}_c{c:D2}", _spawnedSoFar, assignAnti, assignDsp);
                _spawnedSoFar++;
            }
        }
    }

    void GenerateSpiral(System.Random rng)
    {
        int count = Mathf.Max(1, spiralCount);
        _spawnedSoFar = 0;

        float dir = spiralClockwise ? -1f : 1f;
        float totalAngle = dir * spiralTurns * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : (float)i / (count - 1);
            float ang = t * totalAngle;
            float rad = Mathf.Lerp(spiralStartRadius, spiralEndRadius, t);
            float x = spiralCenter.x + Mathf.Cos(ang) * rad;
            float y = spiralCenter.y + Mathf.Sin(ang) * rad;

            bool makeOrange = PickOrange(rng);
            bool assignAnti = ShouldAssign(rng, antiGravityMode, antiGravityPercent, antiGravityEveryN, _spawnedSoFar);
            bool assignDsp = ShouldAssign(rng, disappearingMode, disappearingPercent, disappearingEveryN, _spawnedSoFar);

            var basePrefab = PickPrefabForIndex(makeOrange);
            SpawnPeg(rng, basePrefab, makeOrange, new Vector3(x, y, 0f) + Jitter(rng),
                $"{nameof(Pattern.Spiral)}_{i:D2}", _spawnedSoFar, assignAnti, assignDsp);
            _spawnedSoFar++;
        }
    }

    void GenerateHelixRings(System.Random rng)
    {
        int loops = Mathf.Max(1, helixLoops);
        int pts = Mathf.Max(3, helixPointsPerRing);
        float gapRad = Mathf.Deg2Rad * Mathf.Clamp(helixGapDeg, 0f, 180f);

        _spawnedSoFar = 0;
        for (int k = 0; k < loops; k++)
        {
            float yCenter = helixCenter.y - k * helixSpacingY;
            float gapCenter = (helixAlternateSide && (k % 2 == 1)) ? 0f : Mathf.PI;

            for (int i = 0; i < pts; i++)
            {
                float t = (float)i / pts;
                float ang = t * Mathf.PI * 2f;

                float d = Mathf.Abs(Mathf.DeltaAngle(Mathf.Rad2Deg * ang, Mathf.Rad2Deg * gapCenter)) * Mathf.Deg2Rad;
                if (d <= gapRad * 0.5f) continue;

                float x = helixCenter.x + Mathf.Cos(ang) * helixRadius;
                float y = yCenter + Mathf.Sin(ang) * helixRadius;

                bool makeOrange = PickOrange(rng);
                bool assignAnti = ShouldAssign(rng, antiGravityMode, antiGravityPercent, antiGravityEveryN, _spawnedSoFar);
                bool assignDsp = ShouldAssign(rng, disappearingMode, disappearingPercent, disappearingEveryN, _spawnedSoFar);

                var basePrefab = PickPrefabForIndex(makeOrange);
                SpawnPeg(rng, basePrefab, makeOrange, new Vector3(x, y, 0f) + Jitter(rng),
                    $"{nameof(Pattern.HelixRings)}_ring{k:D2}_i{i:D2}", _spawnedSoFar, assignAnti, assignDsp);
                _spawnedSoFar++;
            }

            if (k < loops - 1 && helixBridgeCount > 0)
            {
                float nextY = helixCenter.y - (k + 1) * helixSpacingY;
                Vector2 pA = new(helixCenter.x + Mathf.Cos(0f) * helixRadius, yCenter + Mathf.Sin(0f) * helixRadius);
                Vector2 pB = new(helixCenter.x + Mathf.Cos(0f) * helixRadius, nextY + Mathf.Sin(0f) * helixRadius);

                int count = Mathf.Max(1, helixBridgeCount);
                for (int b = 0; b < count; b++)
                {
                    float tb = (float)(b + 1) / (count + 1);
                    Vector2 p = Vector2.Lerp(pA, pB, tb);

                    bool makeOrange = PickOrange(rng);
                    bool assignAnti = ShouldAssign(rng, antiGravityMode, antiGravityPercent, antiGravityEveryN, _spawnedSoFar);
                    bool assignDsp = ShouldAssign(rng, disappearingMode, disappearingPercent, disappearingEveryN, _spawnedSoFar);

                    var basePrefab = PickPrefabForIndex(makeOrange);
                    SpawnPeg(rng, basePrefab, makeOrange, (Vector3)p + Jitter(rng),
                        $"{nameof(Pattern.HelixRings)}_bridge{k:D2}_{b:D2}", _spawnedSoFar, assignAnti, assignDsp);
                    _spawnedSoFar++;
                }
            }
        }
    }

    void GenerateConcentricRings(System.Random rng)
    {
        int rings = Mathf.Max(1, concentricRings);
        int pts = Mathf.Max(3, concentricPointsPerRing);
        _spawnedSoFar = 0;

        float a0 = Mathf.Deg2Rad * concentricArcStart;
        float a1 = Mathf.Deg2Rad * concentricArcEnd;
        float sweep = a1 - a0;
        if (Mathf.Approximately(sweep, 0f)) sweep = Mathf.PI * 2f;
        if (concentricClockwise && sweep > 0f) sweep = -sweep;

        for (int r = 0; r < rings; r++)
        {
            float rad = concentricStartRadius + r * concentricRadiusStep;
            for (int i = 0; i < pts; i++)
            {
                float t = pts == 1 ? 0f : (float)i / (pts - 1);
                float ang = a0 + sweep * t;
                float x = concentricCenter.x + Mathf.Cos(ang) * rad;
                float y = concentricCenter.y + Mathf.Sin(ang) * rad;

                bool makeOrange = PickOrange(rng);
                bool assignAnti = ShouldAssign(rng, antiGravityMode, antiGravityPercent, antiGravityEveryN, _spawnedSoFar);
                bool assignDsp = ShouldAssign(rng, disappearingMode, disappearingPercent, disappearingEveryN, _spawnedSoFar);

                var basePrefab = PickPrefabForIndex(makeOrange);
                SpawnPeg(rng, basePrefab, makeOrange, new Vector3(x, y, 0f) + Jitter(rng),
                    $"{nameof(Pattern.ConcentricRings)}_r{r:D2}_i{i:D2}", _spawnedSoFar, assignAnti, assignDsp);
                _spawnedSoFar++;
            }
        }
    }

    void GenerateZigZagVertical(System.Random rng)
    {
        int legs = Mathf.Max(1, zigLegs);
        int perLeg = Mathf.Max(2, zigPointsPerLeg);
        _spawnedSoFar = 0;

        Vector2 left = new(zigStart.x, zigStart.y);
        Vector2 right = new(zigStart.x + zigWidth, zigStart.y);

        Vector2 curA = left;
        Vector2 curB = right;

        for (int l = 0; l < legs; l++)
        {
            for (int i = 0; i < perLeg; i++)
            {
                float t = (float)i / (perLeg - 1);
                Vector2 p = Vector2.Lerp(curA, curB, t);

                bool makeOrange = PickOrange(rng);
                bool assignAnti = ShouldAssign(rng, antiGravityMode, antiGravityPercent, antiGravityEveryN, _spawnedSoFar);
                bool assignDsp = ShouldAssign(rng, disappearingMode, disappearingPercent, disappearingEveryN, _spawnedSoFar);

                var basePrefab = PickPrefabForIndex(makeOrange);
                SpawnPeg(rng, basePrefab, makeOrange, (Vector3)p + Jitter(rng),
                    $"{nameof(Pattern.ZigZagVertical)}_{l:D2}_{i:D2}", _spawnedSoFar, assignAnti, assignDsp);
                _spawnedSoFar++;
            }

            curA = new Vector2(curB.x, curB.y - zigStepY);
            curB = new Vector2(curA.x == right.x ? left.x : right.x, curA.y);
        }
    }

    /* ====================== SAFE EXEC ====================== */
    void SafeRegenerate()
    {
        if (!CheckPrefabs()) return;

        _rng = useSeed ? new System.Random(seed) : new System.Random();
        System.Random rng = _rng;

        // Clear lama
        ClearChildren();

        // Hitung pivot (pusat rotasi) berdasarkan pattern
        Vector3 pivot = ComputePivot();

        // Siapkan RotatingArea kalau diminta
        RotatingArea rotRoot = null;
        _spawnParent = null;

        if (wrapWithRotatingArea)
        {
            var go = new GameObject("RotatingArea");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pivot - transform.position;

            rotRoot = go.AddComponent<RotatingArea>();
            rotRoot.mode = rotatingMode;
            rotRoot.useMudMultiplier = rotatingUseMudMultiplier;
            rotRoot.simulateInEditor = rotatingSimulateInEditor;

            // SelfRotate
            rotRoot.angularSpeed = angularSpeed;

            // Orbit
            rotRoot.orbitRadius = orbitRadius;
            rotRoot.orbitAngularSpeed = orbitAngularSpeed;
            rotRoot.childAngleOffset = childAngleOffset;
            rotRoot.orientTangential = orientTangential;

            // Sweep
            rotRoot.sweepSpace = sweepSpace;
            rotRoot.sweepStartAngle = sweepStartAngle;
            rotRoot.sweepEndAngle = sweepEndAngle;
            rotRoot.sweepSpeed = sweepSpeed;
            rotRoot.sweepEase = (sweepEase == null) ? AnimationCurve.EaseInOut(0, 0, 1, 1) : sweepEase;
            rotRoot.startFromEnd = sweepStartFromEnd;

            _spawnParent = rotRoot.transform;
        }

        // Generate sesuai pola
        switch (pattern)
        {
            case Pattern.Wave: GenerateWave(rng); break;
            case Pattern.CircleOrArc: GenerateCircleOrArc(rng); break;
            case Pattern.Line: GenerateLine(rng); break;
            case Pattern.Grid: GenerateGrid(rng); break;
            case Pattern.Spiral: GenerateSpiral(rng); break;
            case Pattern.HelixRings: GenerateHelixRings(rng); break;
            case Pattern.ConcentricRings: GenerateConcentricRings(rng); break;
            case Pattern.ZigZagVertical: GenerateZigZagVertical(rng); break;
        }

        _spawnParent = null;
    }

    Vector3 ComputePivot()
    {
        switch (pattern)
        {
            case Pattern.CircleOrArc:
                return new Vector3(center.x, center.y, 0f);
            case Pattern.Spiral:
                return new Vector3(spiralCenter.x, spiralCenter.y, 0f);
            case Pattern.Line:
                return ((Vector3)lineStart + (Vector3)lineEnd) * 0.5f;
            case Pattern.Grid:
                {
                    int cols = Mathf.Max(1, gridCols);
                    int rowsLocal = Mathf.Max(1, gridRows);
                    float w = (cols - 1) * gridSpacingX;
                    float h = (rowsLocal - 1) * gridSpacingY;
                    return new Vector3(gridOrigin.x + w * 0.5f, gridOrigin.y - h * 0.5f, 0f);
                }
            case Pattern.Wave:
                {
                    int count = Mathf.Max(0, pegCount);
                    float w = (count > 1 ? (count - 1) * spacingX : 0f);
                    float midY = startPos.y + (rows - 1) * rowOffsetY * 0.5f;
                    return new Vector3(startPos.x + w * 0.5f, midY, 0f);
                }
            case Pattern.ZigZagVertical:
                {
                    float totalH = (zigLegs - 1) * zigStepY;
                    return new Vector3(zigStart.x + zigWidth * 0.5f, zigStart.y - totalH * 0.5f, 0f);
                }
            case Pattern.ConcentricRings:
                return new Vector3(concentricCenter.x, concentricCenter.y, 0f);
            case Pattern.HelixRings:
                {
                    float totalH = (helixLoops - 1) * helixSpacingY;
                    return new Vector3(helixCenter.x, helixCenter.y - totalH * 0.5f, 0f);
                }
        }
        return Vector3.zero;
    }

    void QueueSafeRegenerate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        _pendingRegen = true;
        var delay = Mathf.Max(0, previewDelayMs);
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!this || !isActiveAndEnabled) return;
            if (delay > 0)
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (!this || !isActiveAndEnabled) return;
                    _pendingRegen = true;
                };
            else
                _pendingRegen = true;
        };
#endif
    }

    /* ───────── Editor-only helper for Disappearing ───────── */
#if UNITY_EDITOR
    [AddComponentMenu("")]
    class DisappearGizmoMarker : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0f, 0.9f); // kuning
            float r = 0.35f;
            Gizmos.DrawWireSphere(transform.position, r);
        }
    }
#endif
}
