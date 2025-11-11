// BrickPegGenerator.cs — FULL (Movement + Rotating + Auto Pivot + Safe InnerRing)
using System;
using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class BrickPegGenerator : MonoBehaviour
{
    public enum Variant { Brick, RoundedBrick, MoreRoundedBrick }
    public enum Pattern { CircleOrArc, Line, Grid, BoxPerimeter, RandomScatter }
    public enum ColorMode { AllBlue, RandomPercent, PatternCycle }
    public enum AlignRotation { Tangent, RadialOut, RadialIn }
    public enum RotPivot { GeneratorOrigin, PatternCenter, Custom }

    [System.Serializable]
    public class BrickPrefabs { public GameObject blue; public GameObject orange; public GameObject green; }

    [Header("Prefab Sets (Normal)")]
    public BrickPrefabs brick;
    public BrickPrefabs roundedBrick;
    public BrickPrefabs moreRoundedBrick;

    [Header("Prefab Sets (HARD)")]
    public BrickPrefabs hard_brick;
    public BrickPrefabs hard_roundedBrick;
    public BrickPrefabs hard_moreRoundedBrick;

    public enum HardMode { None, All, RandomPercent, EveryNth, PatternMask }

    [Header("Hard Placement")]
    public HardMode hardMode = HardMode.None;
    [Range(0f, 1f)] public float hardPercent = 0.25f;
    [Min(2)] public int hardEveryN = 5;
    [Tooltip("Gunakan 'H' untuk posisi Hard, karakter lain = normal. Contoh: ..H..H....")]
    public string hardPatternMask = "";
    [Tooltip("Batasi warna yang boleh diubah menjadi Hard.")]
    public bool hardAffectsBlue = true;
    public bool hardAffectsOrange = true;
    public bool hardAffectsGreen = false;

    [Header("Generator Mode")]
    public Variant variant = Variant.Brick;
    public Pattern pattern = Pattern.CircleOrArc;

    [Header("Color Mixing")]
    public ColorMode colorMode = ColorMode.AllBlue;
    [Range(0f, 1f)] public float orangePercent = 0.2f;
    [Range(0f, 1f)] public float greenPercent = 0.1f;
    public string patternCycle = "BBOG";

    [Header("General")]
    public bool alignToPath = true;
    public AlignRotation alignRotation = AlignRotation.RadialOut;
    [Range(-180f, 180f)] public float rotationOffsetDeg = 0f;
    public Vector3 uniformScale = Vector3.one;
    public bool overrideScale = false;
    public float jitterXY = 0f;
    public bool useSeed = false;
    public int seed = 12345;

    [Header("Editor Preview")]
    public bool livePreview = false;
    [Range(0, 200)] public int previewDelayMs = 60;

    // ───────── Circle / Arc ─────────
    [Header("Circle / Arc")]
    public Vector2 center = new(0, 0);
    public float radius = 6f;
    [Min(1)] public int ringCount = 24;
    [Range(0, 360)] public float startAngle = 0f;
    [Range(0, 360)] public float endAngle = 360f;
    public bool clockwise = false;

    [Header("Arc Placement")]
    [Tooltip("Geser sampling 1/2 langkah dari kedua ujung agar tidak ada peg persis di start/end angle.")]
    public bool centerBetweenEnds = true;
    [Range(0f, 1f)] public float endPadding01 = 0f;

    [Header("Arc End Caps (optional)")]
    public bool widenEnds = false;
    [Min(0f)] public float endBulgeRadius = 0.6f;
    [Range(0f, 180f)] public float endBulgeLengthDeg = 25f;
    [Range(1f, 6f)] public float endBulgeEase = 2f;

    [Header("Inner Ring (Circle/Arc only)")]
    public bool buildInnerRing = true;
    public float innerRingRadiusOffset = -0.06f;
    public PhysicsMaterial2D ballNormalMaterial;
    public PhysicsMaterial2D ballLowMaterial;
    public string ballTag = "Ball";
    public string innerRingLayerName = "Ignore Raycast";
    public bool innerRingFollowBulge = true;
    [Min(0.02f)] public float innerRingBandThickness = 0.20f;

    [Header("Optional Rail (OFF by default)")]
    public bool buildRail = false;
    public bool railAsTrigger = true;
    public PhysicsMaterial2D railMaterial;
    public float railRadiusOffset = -0.10f;
    public float arcSegmentDeg = 10f;
    public string railLayerName = "Ignore Raycast";

    [Header("Line")]
    public Vector2 lineStart = new(0f, 5f);
    public Vector2 lineEnd = new(0f, -5f);
    [Min(1)] public int lineCount = 12;

    [Header("Grid")]
    public Vector2 gridOrigin = new(-7f, 3f);
    [Min(1)] public int gridCols = 10;
    [Min(1)] public int gridRows = 5;
    public float gridSpacingX = 1.4f;
    public float gridSpacingY = 1.2f;
    public bool gridStaggerOddRows = false;

    [Header("Box Perimeter (rectangle border)")]
    public Vector2 boxCenter = new(0f, 0f);
    public Vector2 boxSize = new(10f, 6f);
    [Min(4)] public int boxPerimeterCount = 32;
    public bool includeCorners = true;

    [Header("Random Scatter")]
    [Tooltip("Jika true, spawn dalam lingkaran (radius). Jika false, spawn dalam kotak (areaSize).")]
    public bool randomScatterUseCircle = true;
    public Vector2 randomScatterCenter = new Vector2(0f, 0f);
    [Min(0.01f)] public float randomScatterRadius = 4f;
    public Vector2 randomScatterAreaSize = new Vector2(8f, 4f);
    [Min(1)] public int randomScatterCount = 20;
    [Range(-180f, 180f)] public float randomRotationMin = -180f;
    [Range(-180f, 180f)] public float randomRotationMax = 180f;
    [Tooltip("Jika true, pick prefab from any variant sets (brick/rounded/moreRounded). Otherwise uses current Variant.")]
    public bool randomUseAnyVariant = true;
    [Tooltip("Randomize local scale between min/max (1 = prefab scale)")]
    public float randomScaleMin = 1f;
    public float randomScaleMax = 1f;

    // ───────── Movement / Rotation ─────────
    [Header("Movement (per Peg)")]
    public bool addPegMover = false;
    public PegMover.MoveMode moveMode = PegMover.MoveMode.PingPongFromStart;
    public PegMover.Axis moveAxis = PegMover.Axis.Local;
    public Vector2 moveOffset = new Vector2(2f, 0f);
    public float moveSpeed = 2.2f;
    public bool moveLoop = false; // untuk Waypoints
    public bool moveUseMudMultiplier = true;
    public AnimationCurve moveEase = null;

    [Header("Rotation (wrap all pegs)")]
    public bool wrapWithRotatingArea = false;
    public RotatingArea.Mode rotatingMode = RotatingArea.Mode.SelfRotate;
    public bool rotatingUseMudMultiplier = true;
    public bool rotatingSimulateInEditor = false;

    [Header("Rotation Pivot")]
    public RotPivot rotatingPivot = RotPivot.PatternCenter;
    public Vector2 customPivot = Vector2.zero; // jika RotPivot.Custom

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

    // parent dinamis untuk pegs
    Transform _spawnParent = null;

    bool _pendingRegen;

    void OnEnable() => _pendingRegen = false;

    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        if (livePreview) QueueSafeRegenerate();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        if (_pendingRegen)
        {
            _pendingRegen = false;
            Regenerate();
        }
#endif
    }

    [ContextMenu("Regenerate")]
    public void Regenerate()
    {
        if (!HasAnyPrefabForVariant(variant))
        {
            Debug.LogWarning("[BrickPegGenerator] Prefab normal belum di-assign untuk varian ini.");
            return;
        }

        ClearChildren();

        RotatingArea rotRoot = null;
        _spawnParent = null;

        if (wrapWithRotatingArea)
        {
            var go = new GameObject("RotatingArea");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            // === set pivot RotatingArea sesuai pattern (spin in place) ===
            go.transform.position = GetRotationPivotWorld();

            rotRoot = go.AddComponent<RotatingArea>();
            rotRoot.mode = rotatingMode;
            rotRoot.useMudMultiplier = rotatingUseMudMultiplier;
            rotRoot.simulateInEditor = rotatingSimulateInEditor;

            // SelfRotate
            rotRoot.angularSpeed = angularSpeed;
            rotRoot.keepChildrenUpright = true;
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

        System.Random rng = useSeed ? new System.Random(seed) : new System.Random();

        switch (pattern)
        {
            case Pattern.CircleOrArc: GenerateCircleOrArc(rng); break;
            case Pattern.Line: GenerateLine(rng); break;
            case Pattern.Grid: GenerateGrid(rng); break;
            case Pattern.BoxPerimeter: GenerateBoxPerimeter(rng); break;
            case Pattern.RandomScatter: GenerateRandomScatter(rng); break;
        }

        _spawnParent = null;
    }

    Vector3 GetRotationPivotWorld()
    {
        switch (rotatingPivot)
        {
            case RotPivot.Custom:
                return new Vector3(customPivot.x, customPivot.y, 0f);
            case RotPivot.GeneratorOrigin:
                return transform.position;
            case RotPivot.PatternCenter:
            default:
                switch (pattern)
                {
                    case Pattern.CircleOrArc:
                        return new Vector3(center.x, center.y, 0f);
                    case Pattern.Line:
                        Vector2 mid = (lineStart + lineEnd) * 0.5f;
                        return new Vector3(mid.x, mid.y, 0f);
                    case Pattern.Grid:
                        {
                            float w = (Mathf.Max(1, gridCols) - 1) * gridSpacingX;
                            float h = (Mathf.Max(1, gridRows) - 1) * gridSpacingY;
                            Vector2 gmid = new(gridOrigin.x + w * 0.5f, gridOrigin.y - h * 0.5f);
                            return new Vector3(gmid.x, gmid.y, 0f);
                        }
                    case Pattern.BoxPerimeter:
                        return new Vector3(boxCenter.x, boxCenter.y, 0f);
                    default:
                        return transform.position;
                }
        }
    }

    [ContextMenu("Clear Children")]
    public void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Generators
    // ─────────────────────────────────────────────────────────────────────────────

    void GenerateCircleOrArc(System.Random rng)
    {
        int count = Mathf.Max(1, ringCount);

        float deltaRaw = Mathf.Repeat(endAngle - startAngle, 360f);
        bool fullCircle = Mathf.Approximately(deltaRaw, 0f);

        float sweepDeg = fullCircle ? 360f : Mathf.DeltaAngle(startAngle, endAngle);
        float sweepRad = Mathf.Deg2Rad * sweepDeg;
        if (clockwise && sweepRad > 0f) sweepRad = -sweepRad;

        float a0 = Mathf.Deg2Rad * startAngle;

        List<Vector3> pts = (fullCircle || !widenEnds || endBulgeRadius <= 0f)
            ? BuildPointsEqualAngle(count, a0, sweepRad, fullCircle, rng)
            : BuildPointsEqualArcLength(count, a0, sweepRad, rng);

        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 p = pts[i];

            Quaternion rot = Quaternion.identity;
            if (alignToPath)
            {
                switch (alignRotation)
                {
                    case AlignRotation.Tangent:
                        {
                            Vector3 prev = pts[Mathf.Max(0, i - 1)];
                            Vector3 next = pts[Mathf.Min(pts.Count - 1, i + 1)];
                            Vector2 tdir = (next - prev);
                            float tangDeg = Mathf.Atan2(tdir.y, tdir.x) * Mathf.Rad2Deg;
                            rot = Quaternion.Euler(0, 0, tangDeg + rotationOffsetDeg);
                            break;
                        }
                    case AlignRotation.RadialOut:
                        {
                            Vector2 n = ((Vector2)p - center).normalized;
                            float deg = Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg;
                            rot = Quaternion.Euler(0, 0, deg + 90f + rotationOffsetDeg);
                            break;
                        }
                    case AlignRotation.RadialIn:
                        {
                            Vector2 n = (center - (Vector2)p).normalized;
                            float deg = Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg;
                            rot = Quaternion.Euler(0, 0, deg + 90f + rotationOffsetDeg);
                            break;
                        }
                }
            }

            SpawnBrick(rng, p, rot, $"Circle_{i:D2}");
        }

        if (buildInnerRing)
            BuildInnerRingCircleOrArc(fullCircle, center, radius, startAngle, endAngle, clockwise);

        if (buildRail)
            BuildRailCircleOrArc(fullCircle, center, radius + railRadiusOffset, startAngle, endAngle, clockwise);
    }

    List<Vector3> BuildPointsEqualAngle(int n, float aStart, float sweepRad, bool fullCircle, System.Random rng)
    {
        var list = new List<Vector3>(n);
        float step = fullCircle ? (Mathf.PI * 2f) / n : (sweepRad / n);

        for (int i = 0; i < n; i++)
        {
            float t;
            if (fullCircle) t = i;
            else if (centerBetweenEnds) t = i + 0.5f;
            else
            {
                float tf = (n == 1 ? 0f : (float)i / (n - 1));
                if (endPadding01 > 0f)
                {
                    float pad = Mathf.Clamp01(endPadding01) / Mathf.Max(1, (n - 1));
                    tf = Mathf.Lerp(pad, 1f - pad, tf);
                }
                t = tf * n;
            }

            float ang = aStart + step * t;

            float r = radius;
            if (!fullCircle && widenEnds && endBulgeRadius > 0f)
            {
                float w = BulgeWeight01(
                    fullCircle ? (i / (float)n) : ((i + 0.5f) / n),
                    Mathf.Abs(Mathf.Rad2Deg * sweepRad),
                    endBulgeLengthDeg, endBulgeEase);
                r += endBulgeRadius * w;
            }

            Vector2 pos2 = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            list.Add((Vector3)pos2 + Jitter(rng));
        }
        return list;
    }

    List<Vector3> BuildPointsEqualArcLength(int n, float aStart, float sweepRad, System.Random rng)
    {
        int samples = Mathf.Max(128, n * 16);
        var poly = new List<Vector2>(samples);
        var accum = new float[samples];

        System.Func<float, Vector2> PosAt = (t) =>
        {
            float ang = aStart + sweepRad * t;
            float r = radius;
            float w = BulgeWeight01(t, Mathf.Abs(Mathf.Rad2Deg * sweepRad), endBulgeLengthDeg, endBulgeEase);
            r += endBulgeRadius * w;
            return center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
        };

        Vector2 prev = PosAt(0f);
        poly.Add(prev);
        accum[0] = 0f;
        float total = 0f;

        for (int i = 1; i < samples; i++)
        {
            float t = (float)i / (samples - 1);
            Vector2 p = PosAt(t);
            total += Vector2.Distance(p, prev);
            poly.Add(p);
            accum[i] = total;
            prev = p;
        }

        var list = new List<Vector3>(n);
        if (n == 1)
        {
            list.Add((Vector3)poly[0] + Jitter(rng));
            return list;
        }

        for (int i = 0; i < n; i++)
        {
            float t01;
            if (centerBetweenEnds) t01 = ((i + 0.5f) / (float)n);
            else
            {
                t01 = (float)i / (n - 1);
                if (endPadding01 > 0f)
                {
                    float pad = Mathf.Clamp01(endPadding01) / Mathf.Max(1, (n - 1));
                    t01 = Mathf.Lerp(pad, 1f - pad, t01);
                }
            }

            float target = total * t01;

            int lo = 0, hi = samples - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (accum[mid] < target) lo = mid + 1; else hi = mid;
            }
            int k1 = Mathf.Clamp(lo, 1, samples - 1);
            int k0 = k1 - 1;

            float segLen = Mathf.Max(1e-4f, accum[k1] - accum[k0]);
            float u = (target - accum[k0]) / segLen;

            Vector2 P = Vector2.Lerp(poly[k0], poly[k1], u);
            list.Add((Vector3)P + Jitter(rng));
        }
        return list;
    }

    static float BulgeWeight01(float t01, float sweepDegAbs, float lenDeg, float easePow)
    {
        if (lenDeg <= 0f || sweepDegAbs <= 0.0001f) return 0f;
        float edge = Mathf.Clamp01(lenDeg / sweepDegAbs);

        float w0 = 1f - Mathf.Clamp01(t01 / edge);
        float w1 = 1f - Mathf.Clamp01((1f - t01) / edge);
        float w = Mathf.Max(w0, w1);
        return Mathf.Pow(w, Mathf.Max(1f, easePow));
    }

    void GenerateLine(System.Random rng)
    {
        int count = Mathf.Max(1, lineCount);
        Vector2 a = lineStart;
        Vector2 b = lineEnd;
        Vector2 dir = (b - a).normalized;
        float deg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : (float)i / (count - 1);
            Vector2 p = Vector2.Lerp(a, b, t);
            Vector3 pos = (Vector3)p + Jitter(rng);

            Quaternion rot = alignToPath ? Quaternion.Euler(0, 0, deg) : Quaternion.identity;
            SpawnBrick(rng, pos, rot, $"Line_{i:D2}");
        }
    }

    void GenerateGrid(System.Random rng)
    {
        int cols = Mathf.Max(1, gridCols);
        int rows = Mathf.Max(1, gridRows);

        for (int r = 0; r < rows; r++)
        {
            float off = (gridStaggerOddRows && (r % 2 == 1)) ? gridSpacingX * 0.5f : 0f;

            for (int c = 0; c < cols; c++)
            {
                float x = gridOrigin.x + c * gridSpacingX + off;
                float y = gridOrigin.y - r * gridSpacingY;

                Vector3 pos = new Vector3(x, y, 0f) + Jitter(rng);
                SpawnBrick(rng, pos, Quaternion.identity, $"Grid_r{r:D2}_c{c:D2}");
            }
        }
    }

    void GenerateBoxPerimeter(System.Random rng)
    {
        int count = Mathf.Max(4, boxPerimeterCount);
        float w = boxSize.x, h = boxSize.y;
        float halfW = w * 0.5f, halfH = h * 0.5f;
        float perim = 2f * (w + h);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float dist = t * perim;

            Vector2 p; Vector2 tangent;

            if (dist < w) { p = new Vector2(halfW - dist, halfH); tangent = Vector2.left; }
            else if (dist < w + h) { p = new Vector2(-halfW, halfH - (dist - w)); tangent = Vector2.down; }
            else if (dist < w + h + w) { p = new Vector2(-halfW + (dist - (w + h)), -halfH); tangent = Vector2.right; }
            else { p = new Vector2(halfW, -halfH + (dist - (w + h + w))); tangent = Vector2.up; }

            Vector3 pos = (Vector3)(boxCenter + p) + Jitter(rng);
            float deg = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            Quaternion rot = alignToPath ? Quaternion.Euler(0, 0, deg) : Quaternion.identity;

            SpawnBrick(rng, pos, rot, $"Box_{i:D2}");
        }
    }

    void GenerateRandomScatter(System.Random rng)
    {
        int count = Mathf.Max(1, randomScatterCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 p;
            if (randomScatterUseCircle)
            {
                // pick random point inside circle
                float r = Mathf.Sqrt((float)rng.NextDouble()) * randomScatterRadius;
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                p = new Vector3(randomScatterCenter.x + Mathf.Cos(a) * r, randomScatterCenter.y + Mathf.Sin(a) * r, 0f);
            }
            else
            {
                float rx = ((float)rng.NextDouble() - 0.5f) * randomScatterAreaSize.x;
                float ry = ((float)rng.NextDouble() - 0.5f) * randomScatterAreaSize.y;
                p = new Vector3(randomScatterCenter.x + rx, randomScatterCenter.y + ry, 0f);
            }

            p += Jitter(rng);

            float rotDeg = Mathf.Lerp(randomRotationMin, randomRotationMax, (float)rng.NextDouble());
            Quaternion rot = Quaternion.Euler(0f, 0f, rotDeg);

            GameObject prefabToUse = null;
            if (randomUseAnyVariant)
                prefabToUse = PickAnyPrefab(rng);
            else
                prefabToUse = PickPrefab(rng); // uses existing color / hard logic for current variant

            SpawnBrickCustom(prefabToUse, rng, p, rot, $"Random_{i:D2}");
        }
    }


    // ─────────────────────────────────────────────────────────────────────────────
    // Spawn & helpers
    // ─────────────────────────────────────────────────────────────────────────────

    void SpawnBrick(System.Random rng, Vector3 pos, Quaternion rot, string nameTag)
    {
        var prefab = PickPrefab(rng);
        if (!prefab) return;

        var go = Instantiate(prefab, pos, rot, _spawnParent ? _spawnParent : transform);

        if (addPegMover)
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
        go.name = $"{prefab.name}_{nameTag}";

        go.transform.localScale = overrideScale ? uniformScale : prefab.transform.localScale;

        ForcePegTypeFromPrefab(go, prefab);
    }

    enum PegColor { Blue, Orange, Green }

    void ForcePegTypeFromPrefab(GameObject instance, GameObject prefabUsed)
    {
        var pc = instance.GetComponent<PegController>() ?? instance.GetComponentInChildren<PegController>();
        if (!pc) return;

        var setN = GetSet(variant);
        var setH = GetHardSet(variant);

        if (setN != null && prefabUsed != null)
        {
            if (prefabUsed == setN.orange || (setH != null && prefabUsed == setH.orange)) pc.ForceSetPegType(PegType.Orange);
            else if (prefabUsed == setN.green || (setH != null && prefabUsed == setH.green)) pc.ForceSetPegType(PegType.Green);
            else pc.ForceSetPegType(PegType.Blue);
        }
        else pc.ForceSetPegType(PegType.Blue);
    }

    GameObject PickPrefab(System.Random rng)
    {
        var normalSet = GetSet(variant);
        if (normalSet == null) return null;

        GameObject chosenNormal = PickColorPrefab(normalSet, rng);
        PegColor colorPicked = ColorOfPrefab(chosenNormal, normalSet);

        bool makeHard = ShouldBeHard(transform.childCount, colorPicked);

        var hardSet = GetHardSet(variant);
        if (makeHard && hardSet != null)
        {
            GameObject byColor = PickColorPrefabByColor(hardSet, colorPicked);
            if (byColor) return byColor;
        }

        return chosenNormal;
    }

    GameObject PickAnyPrefab(System.Random rng)
    {
        // collect available normal prefabs from all variant sets
        var pool = new List<GameObject>();
        void AddIfPresent(BrickPrefabs s)
        {
            if (s == null) return;
            if (s.blue) pool.Add(s.blue);
            if (s.orange) pool.Add(s.orange);
            if (s.green) pool.Add(s.green);
        }
        AddIfPresent(brick);
        AddIfPresent(roundedBrick);
        AddIfPresent(moreRoundedBrick);
        if (pool.Count == 0) return null;
        return pool[rng.Next(pool.Count)];
    }

    void SpawnBrickCustom(GameObject prefab, System.Random rng, Vector3 pos, Quaternion rot, string nameTag)
    {
        if (!prefab) return;
        var go = Instantiate(prefab, pos, rot, _spawnParent ? _spawnParent : transform);

        if (addPegMover)
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

        go.name = $"{prefab.name}_{nameTag}";

        // apply optional random scale override
        if (overrideScale)
            go.transform.localScale = uniformScale;
        else if (randomScaleMin != 1f || randomScaleMax != 1f)
        {
            float s = UnityEngine.Random.Range(randomScaleMin, randomScaleMax);
            go.transform.localScale = prefab.transform.localScale * s;
        }
        else
            go.transform.localScale = prefab.transform.localScale;

        ForcePegTypeFromPrefab(go, prefab);
    }

    GameObject PickColorPrefab(BrickPrefabs set, System.Random rng)
    {
        switch (colorMode)
        {
            case ColorMode.AllBlue:
                return set.blue ? set.blue : (set.orange ? set.orange : set.green);
            case ColorMode.RandomPercent:
                {
                    float o = Mathf.Clamp01(orangePercent);
                    float g = Mathf.Clamp01(greenPercent);
                    float r = (float)rng.NextDouble();
                    if (r < o && set.orange) return set.orange;
                    if (r < o + g && set.green) return set.green;
                    return set.blue ? set.blue : (set.orange ? set.orange : set.green);
                }
            case ColorMode.PatternCycle:
                {
                    if (string.IsNullOrEmpty(patternCycle)) return set.blue ?? set.orange ?? set.green;
                    int idx = Mathf.Max(0, transform.childCount);
                    char ch = patternCycle[idx % patternCycle.Length];
                    switch (char.ToUpperInvariant(ch))
                    {
                        case 'O': return set.orange ? set.orange : set.blue ?? set.green;
                        case 'G': return set.green ? set.green : set.blue ?? set.orange;
                        default: return set.blue ? set.blue : set.orange ?? set.green;
                    }
                }
        }
        return set.blue;
    }

    GameObject PickColorPrefabByColor(BrickPrefabs set, PegColor c)
    {
        return c switch
        {
            PegColor.Orange => set.orange ? set.orange : set.blue ?? set.green,
            PegColor.Green => set.green ? set.green : set.blue ?? set.orange,
            _ => set.blue ? set.blue : set.orange ?? set.green,
        };
    }

    PegColor ColorOfPrefab(GameObject prefab, BrickPrefabs set)
    {
        if (prefab == set.orange) return PegColor.Orange;
        if (prefab == set.green) return PegColor.Green;
        return PegColor.Blue;
    }

    bool ShouldBeHard(int index, PegColor color)
    {
        if ((color == PegColor.Blue && !hardAffectsBlue) ||
            (color == PegColor.Orange && !hardAffectsOrange) ||
            (color == PegColor.Green && !hardAffectsGreen))
            return false;

        switch (hardMode)
        {
            case HardMode.None: return false;
            case HardMode.All: return true;
            case HardMode.RandomPercent: return UnityEngine.Random.value < Mathf.Clamp01(hardPercent);
            case HardMode.EveryNth: return (hardEveryN > 0) && ((index + 1) % hardEveryN == 0);
            case HardMode.PatternMask:
                if (string.IsNullOrEmpty(hardPatternMask)) return false;
                char ch = hardPatternMask[index % hardPatternMask.Length];
                return char.ToUpperInvariant(ch) == 'H';
        }
        return false;
    }

    BrickPrefabs GetSet(Variant v) => v switch
    {
        Variant.Brick => brick,
        Variant.RoundedBrick => roundedBrick,
        Variant.MoreRoundedBrick => moreRoundedBrick,
        _ => brick
    };

    BrickPrefabs GetHardSet(Variant v) => v switch
    {
        Variant.Brick => hard_brick,
        Variant.RoundedBrick => hard_roundedBrick,
        Variant.MoreRoundedBrick => hard_moreRoundedBrick,
        _ => hard_brick
    };

    bool HasAnyPrefabForVariant(Variant v)
    {
        var s = GetSet(v);
        return s != null && (s.blue || s.orange || s.green);
    }

    Vector3 Jitter(System.Random rng)
    {
        if (jitterXY <= 0f) return Vector3.zero;
        float jx = ((float)rng.NextDouble() * 2f - 1f) * jitterXY;
        float jy = ((float)rng.NextDouble() * 2f - 1f) * jitterXY;
        return new Vector3(jx, jy, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Inner Ring & Rail builders
    // ─────────────────────────────────────────────────────────────────────────────

    void BuildInnerRingCircleOrArc(bool isFullCircle, Vector2 c, float baseRadius, float startDeg, float endDeg, bool cw)
    {
        float rBase = Mathf.Max(0.01f, baseRadius + innerRingRadiusOffset);

        if (isFullCircle)
        {
            var inner = new GameObject("InnerRing_Circle");
            inner.transform.position = new Vector3(c.x, c.y, 0f);
            inner.transform.SetParent(transform, true);
            inner.transform.localScale = Vector3.one;

            var circ = inner.AddComponent<CircleCollider2D>();
            circ.isTrigger = true;
            circ.radius = rBase;

            AttachBounceSwitcher(inner);
            SetLayer(inner, innerRingLayerName);
            return;
        }

        if (!innerRingFollowBulge || !widenEnds || endBulgeRadius <= 0f)
        {
            var inner = new GameObject("InnerRing_Arc");
            inner.transform.position = new Vector3(c.x, c.y, 0f);
            inner.transform.SetParent(transform, true);
            inner.transform.localScale = Vector3.one;

            var poly = inner.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;

            var pts = BuildArcSectorPoints(c, rBase, startDeg, endDeg, cw, arcSegmentDeg);
            poly.points = pts;

            AttachBounceSwitcher(inner);
            SetLayer(inner, innerRingLayerName);
            return;
        }

        {
            var inner = new GameObject("InnerRing_ArcRibbon");
            inner.transform.position = new Vector3(c.x, c.y, 0f);
            inner.transform.SetParent(transform, true);
            inner.transform.localScale = Vector3.one;

            var poly = inner.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;

            var pts = BuildBulgedRibbonPoints(
                c, rBase, startDeg, endDeg, cw,
                innerRingBandThickness,
                arcSegmentDeg);

            poly.points = pts;

            AttachBounceSwitcher(inner);
            SetLayer(inner, innerRingLayerName);
        }
    }

    void BuildRailCircleOrArc(bool isFullCircle, Vector2 c, float r, float startDeg, float endDeg, bool cw)
    {
        if (!railMaterial) return;

        if (isFullCircle)
        {
            var rail = new GameObject("Rail_Circle");
            rail.transform.position = new Vector3(c.x, c.y, 0f);
            rail.transform.SetParent(transform, true);
            rail.transform.localScale = Vector3.one;

            var rb = rail.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var cc = rail.AddComponent<CircleCollider2D>();
            cc.isTrigger = railAsTrigger;
            cc.radius = r;
            cc.sharedMaterial = railMaterial;

            SetLayer(rail, railLayerName);
        }
        else
        {
            var rail = new GameObject("Arc_Rail");
            rail.transform.position = new Vector3(c.x, c.y, 0f);
            rail.transform.SetParent(transform, true);
            rail.transform.localScale = Vector3.one;

            var rb = rail.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var ec = rail.AddComponent<EdgeCollider2D>();
            ec.isTrigger = railAsTrigger;
            ec.points = BuildArcPolylinePoints(c, r, startDeg, endDeg, cw, arcSegmentDeg);
            ec.sharedMaterial = railMaterial;

            SetLayer(rail, railLayerName);
        }
    }

    static Vector2[] BuildArcPolylinePoints(Vector2 c, float r, float startDeg, float endDeg, bool cw, float segDeg)
    {
        float deltaRaw = Mathf.Repeat(endDeg - startDeg, 360f);
        bool fullCircle = Mathf.Approximately(deltaRaw, 0f);

        float sweepDeg = fullCircle ? 360f : Mathf.DeltaAngle(startDeg, endDeg);
        if (cw && sweepDeg > 0f) sweepDeg = -sweepDeg;

        int segmentCount = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweepDeg) / Mathf.Max(1f, segDeg)));
        Vector2[] pts = new Vector2[segmentCount + 1];

        float a0 = Mathf.Deg2Rad * startDeg;
        float step = Mathf.Deg2Rad * (sweepDeg / segmentCount);

        for (int i = 0; i <= segmentCount; i++)
        {
            float ang = a0 + step * i;
            pts[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
        }
        return pts;
    }

    static Vector2[] BuildArcSectorPoints(Vector2 c, float r, float startDeg, float endDeg, bool cw, float segDeg)
    {
        var edge = BuildArcPolylinePoints(c, r, startDeg, endDeg, cw, segDeg);
        Vector2[] pts = new Vector2[edge.Length + 2];
        pts[0] = c;
        for (int i = 0; i < edge.Length; i++) pts[i + 1] = edge[i];
        pts[^1] = c;
        return pts;
    }

    Vector2[] BuildBulgedRibbonPoints(
        Vector2 c, float rBase, float startDeg, float endDeg, bool cw,
        float thickness, float segDeg)
    {
        float sweepDeg = Mathf.DeltaAngle(startDeg, endDeg);
        if (cw && sweepDeg > 0f) sweepDeg = -sweepDeg;

        int segCount = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(sweepDeg) / Mathf.Max(2f, segDeg)));
        int samples = segCount + 1;

        System.Func<float, Vector2> PosAt = (t) =>
        {
            float ang = Mathf.Deg2Rad * (startDeg + sweepDeg * t);
            float r = rBase + endBulgeRadius * BulgeWeight01(
                t, Mathf.Abs(sweepDeg), endBulgeLengthDeg, endBulgeEase);
            return c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
        };

        var path = new Vector2[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / (samples - 1);
            path[i] = PosAt(t);
        }

        var outer = new Vector2[samples];
        var inner = new Vector2[samples];
        float half = Mathf.Max(0.01f, thickness * 0.5f);

        for (int i = 0; i < samples; i++)
        {
            Vector2 a = path[Mathf.Max(0, i - 1)];
            Vector2 b = path[Mathf.Min(samples - 1, i + 1)];
            Vector2 tdir = (b - a).normalized;
            Vector2 n = new Vector2(-tdir.y, tdir.x);

            outer[i] = path[i] + n * half;
            inner[i] = path[i] - n * half;
        }

        var poly = new List<Vector2>(samples * 2);
        for (int i = 0; i < samples; i++) poly.Add(outer[i]);
        for (int i = samples - 1; i >= 0; i--) poly.Add(inner[i]);

        return poly.ToArray();
    }
    void AttachBounceSwitcher(GameObject host)
    {
        var sw = host.GetComponent<BounceSwitcher2D>();
        if (!sw) sw = host.AddComponent<BounceSwitcher2D>();

        sw.normalBall = ballNormalMaterial;
        sw.lowBall = ballLowMaterial;
        sw.ballTag = ballTag;

        sw.gravityScaleInside = 0f;   // biarkan 0 kecuali mau “melayang”
        sw.freezeRotationInside = true;    // ⬅️ kunci rotasi di dalam ring
        sw.angularDragInside = 12f;    // dipakai kalau freezeRotationInside = false
    }


    static void SetLayer(GameObject go, string layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return;
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0) go.layer = layer;
        foreach (Transform child in go.transform)
            if (child) child.gameObject.layer = go.layer;
    }

    void QueueSafeRegenerate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        _pendingRegen = true;
        int delay = Mathf.Max(0, previewDelayMs);
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!this || !isActiveAndEnabled) return;
            _pendingRegen = true;
        };
#endif
    }

    void SafeRegenerate()
    {
        if (!isActiveAndEnabled) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += Regenerate;
#endif
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// BounceSwitcher2D (helper untuk inner ring; aman kalau tak dipakai)
// ─────────────────────────────────────────────────────────────────────────────
public class BounceSwitcher2D : MonoBehaviour
{
    [Header("Ball materials")]
    public PhysicsMaterial2D normalBall;
    public PhysicsMaterial2D lowBall;
    public string ballTag = "Ball";

    [Header("Optional smoothing")]
    [Tooltip("0 = tidak pakai. >0 akan override gravityScale saat di dalam ring.")]
    public float gravityScaleInside = 0f;

    [Header("Rotation control inside ring")]
    [Tooltip("Jika true, rotasi bola dibekukan selama di dalam inner ring.")]
    public bool freezeRotationInside = true;
    [Tooltip("Jika tidak membekukan rotasi, setel drag rotasi besar agar cepat berhenti.")]
    public float angularDragInside = 12f;

    class MatCache : MonoBehaviour
    {
        public PhysicsMaterial2D[] originals;
        public Collider2D[] cols;
        public float originalGravity;
        public float originalAngularDrag;
        public RigidbodyConstraints2D originalConstraints;
        public int overlapCount;
        public bool initialized;
    }

    static MatCache GetCache(Rigidbody2D rb)
    {
        if (!rb) return null;
        var cache = rb.GetComponent<MatCache>();
        if (!cache) cache = rb.gameObject.AddComponent<MatCache>();
        return cache;
    }

    static void ApplyLow(Rigidbody2D rb, MatCache cache, PhysicsMaterial2D low, float gravityInside, bool freezeRot, float angDragInside)
    {
        if (!rb || !cache) return;

        if (!cache.initialized)
        {
            cache.cols = rb.GetComponents<Collider2D>();
            cache.originals = new PhysicsMaterial2D[cache.cols.Length];
            for (int i = 0; i < cache.cols.Length; i++)
                cache.originals[i] = cache.cols[i].sharedMaterial;
            cache.originalGravity = rb.gravityScale;
            cache.originalAngularDrag = rb.angularDrag;
            cache.originalConstraints = rb.constraints;
            cache.initialized = true;
        }

        // ganti material ke low
        foreach (var col in cache.cols) if (col) col.sharedMaterial = low;

        // kontrol gravitasi
        if (gravityInside != 0f) rb.gravityScale = gravityInside;

        // kontrol rotasi
        if (freezeRot)
        {
            rb.angularVelocity = 0f;
            rb.constraints = cache.originalConstraints | RigidbodyConstraints2D.FreezeRotation;
        }
        else
        {
            rb.angularDrag = angDragInside;
        }
    }

    static void ApplyNormal(Rigidbody2D rb, MatCache cache, PhysicsMaterial2D normal)
    {
        if (!rb || !cache || !cache.initialized) return;

        // pulihkan material
        for (int i = 0; i < cache.cols.Length; i++)
            if (cache.cols[i]) cache.cols[i].sharedMaterial = cache.originals[i];

        // pulihkan fisika
        rb.gravityScale = cache.originalGravity;
        rb.angularDrag = cache.originalAngularDrag;
        rb.constraints = cache.originalConstraints;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var rb = other.attachedRigidbody;
        if (!rb || !rb.CompareTag(ballTag)) return;

        var cache = GetCache(rb);
        cache.overlapCount++;
        ApplyLow(rb, cache, lowBall, gravityScaleInside, freezeRotationInside, angularDragInside);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var rb = other.attachedRigidbody;
        if (!rb || !rb.CompareTag(ballTag)) return;

        var cache = GetCache(rb);
        cache.overlapCount = Mathf.Max(0, cache.overlapCount - 1);
        if (cache.overlapCount == 0)
            ApplyNormal(rb, cache, normalBall);
    }

    void OnCollisionStay2D(Collision2D col)
    {
        var rb = col.rigidbody;
        if (!rb || !rb.CompareTag(ballTag)) return;

        var cache = GetCache(rb);
        if (cache.overlapCount == 0)
            ApplyNormal(rb, cache, normalBall);
    }
}

