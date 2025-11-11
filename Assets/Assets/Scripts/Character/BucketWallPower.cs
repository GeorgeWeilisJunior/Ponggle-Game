using System.Linq;
using UnityEngine;

/// Dinding kiri/kanan untuk funnel ke bucket.
/// Mode:
///  • Auto   : script membuat Wall_L / Wall_R sendiri (BoxCollider2D + Sprite opsional)
///  • Manual : pakai GameObject dinding milikmu; script hanya ON/OFF
[RequireComponent(typeof(BucketController))]
public class BucketWallPower : MonoBehaviour
{
    public enum BuildMode { Auto, Manual }

    [Header("Mode")]
    public BuildMode mode = BuildMode.Auto;

    [Header("Geometry (AUTO mode)")]
    public float wallLength = 4.5f;
    public float wallThickness = 0.25f;
    [Range(0f, 85f)] public float wallAngleDeg = 60f;
    public float baseOffsetX = 1.2f;
    public float baseOffsetY = 0.0f;

    [Header("Collision")]
    public bool forceSolidCollider = true;
    public PhysicsMaterial2D physicsMaterial;

    [Header("Visual (AUTO mode, opsional)")]
    public Sprite brickSprite;
    public string sortingLayer = "HUDWorld";
    public int sortingOrder = 2;
    public bool hideSprites = false;

    [Header("Manual mode refs")]
    public Transform manualLeftRoot;
    public Transform manualRightRoot;
    public bool manualToggleGameObjects = true;

    [Header("Bounce Dampener")]
    [Tooltip("Kurangi kecepatan bola saat menyentuh dinding (1 = no effect).")]
    [Range(0.05f, 1f)] public float onHitVelocityScale = 0.1f;
    [Tooltip("Otomatis pasang komponen peredam ke collider dinding.")]
    public bool addDampenerToWalls = true;

    [Header("Debug")]
    public bool showGizmos = false;

    // AUTO internals
    Transform leftWall, rightWall;
    BoxCollider2D leftCol, rightCol;
    SpriteRenderer leftSR, rightSR;

    public bool IsActive { get; private set; }

    void Awake()
    {
        if (mode == BuildMode.Auto)
        {
            BindExistingChildren();
            BuildChildrenIfMissing();
            ApplyAutoGeometry();
            AttachDampener(leftWall);
            AttachDampener(rightWall);
            SetActive(false);
        }
        else // Manual
        {
            if (!manualLeftRoot) manualLeftRoot = transform.Find("Wall Left") ?? transform.Find("Wall_L");
            if (!manualRightRoot) manualRightRoot = transform.Find("Wall Right") ?? transform.Find("Wall_R");
            AttachDampener(manualLeftRoot);
            AttachDampener(manualRightRoot);
            SetActive(false);
        }
    }

    void OnValidate()
    {
        if (mode == BuildMode.Auto && leftWall && rightWall)
            ApplyAutoGeometry();           // jangan bikin child baru di OnValidate (biar tidak spam)
    }

    /* ================= AUTO MODE ================= */
    void BindExistingChildren()
    {
        leftWall = transform.Find("Wall_L");
        rightWall = transform.Find("Wall_R");

        // bersihkan duplikat (aman di editor)
        foreach (var t in GetChildrenByName("Wall_L").Skip(1).ToList())
            if (Application.isPlaying) Destroy(t.gameObject); else DestroyImmediate(t.gameObject);
        foreach (var t in GetChildrenByName("Wall_R").Skip(1).ToList())
            if (Application.isPlaying) Destroy(t.gameObject); else DestroyImmediate(t.gameObject);

        if (leftWall) { leftCol = leftWall.GetComponent<BoxCollider2D>(); leftSR = leftWall.GetComponent<SpriteRenderer>(); }
        if (rightWall) { rightCol = rightWall.GetComponent<BoxCollider2D>(); rightSR = rightWall.GetComponent<SpriteRenderer>(); }
    }

    void BuildChildrenIfMissing()
    {
        if (!leftWall)
        {
            leftWall = new GameObject("Wall_L").transform;
            leftWall.SetParent(transform, false);
            leftCol = leftWall.gameObject.AddComponent<BoxCollider2D>();
            leftSR = leftWall.gameObject.AddComponent<SpriteRenderer>();
        }
        if (!rightWall)
        {
            rightWall = new GameObject("Wall_R").transform;
            rightWall.SetParent(transform, false);
            rightCol = rightWall.gameObject.AddComponent<BoxCollider2D>();
            rightSR = rightWall.gameObject.AddComponent<SpriteRenderer>();
        }

        if (leftCol) { leftCol.sharedMaterial = physicsMaterial; leftCol.isTrigger = false; }
        if (rightCol) { rightCol.sharedMaterial = physicsMaterial; rightCol.isTrigger = false; }

        SetupSR(leftSR);
        SetupSR(rightSR);
    }

    void SetupSR(SpriteRenderer sr)
    {
        if (!sr) return;
        sr.sprite = brickSprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;
        sr.enabled = !hideSprites && sr.sprite;
    }

    void ApplyAutoGeometry()
    {
        if (!leftWall || !rightWall) return;

        float ang = wallAngleDeg;
        Vector3 size = new Vector3(wallThickness, wallLength, 1f);

        // kiri (miring ke kanan)
        leftWall.localPosition = new Vector3(-baseOffsetX, baseOffsetY, 0f);
        leftWall.localRotation = Quaternion.Euler(0, 0, ang);
        SetBox(leftCol, size);
        FitSprite(leftSR, size);

        // kanan (miring ke kiri)
        rightWall.localPosition = new Vector3(+baseOffsetX, baseOffsetY, 0f);
        rightWall.localRotation = Quaternion.Euler(0, 0, -ang);
        SetBox(rightCol, size);
        FitSprite(rightSR, size);

        if (forceSolidCollider)
        {
            if (leftCol) leftCol.isTrigger = false;
            if (rightCol) rightCol.isTrigger = false;
        }

        if (leftSR) leftSR.enabled = !hideSprites && leftSR.sprite;
        if (rightSR) rightSR.enabled = !hideSprites && rightSR.sprite;
    }

    void SetBox(BoxCollider2D col, Vector3 size)
    {
        if (!col) return;
        col.size = new Vector2(size.x, size.y);
        col.offset = Vector2.zero;
    }

    void FitSprite(SpriteRenderer sr, Vector3 size)
    {
        if (!sr) return;
        if (!sr.sprite) { sr.enabled = false; return; }
        var spSize = sr.sprite.bounds.size; // world tanpa scale
        float sx = size.x / Mathf.Max(0.0001f, spSize.x);
        float sy = size.y / Mathf.Max(0.0001f, spSize.y);
        sr.transform.localScale = new Vector3(sx, sy, 1f);
    }

    /* ================= MANUAL MODE ================= */
    void ToggleManual(bool on)
    {
        if (manualToggleGameObjects)
        {
            if (manualLeftRoot) manualLeftRoot.gameObject.SetActive(on);
            if (manualRightRoot) manualRightRoot.gameObject.SetActive(on);
        }

        EnableAllColliders(manualLeftRoot, on);
        EnableAllColliders(manualRightRoot, on);
    }

    void EnableAllColliders(Transform root, bool on)
    {
        if (!root) return;
        var cols = root.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols)
        {
            if (forceSolidCollider && !(c is CompositeCollider2D)) c.isTrigger = false;
            c.enabled = on;
        }
    }

    /* ================= PUBLIC API ================= */
    public void SetActive(bool on)
    {
        IsActive = on;

        if (mode == BuildMode.Auto)
        {
            if (leftCol) leftCol.enabled = on;
            if (rightCol) rightCol.enabled = on;

            bool show = on && !hideSprites;
            if (leftSR) leftSR.enabled = show && leftSR.sprite;
            if (rightSR) rightSR.enabled = show && rightSR.sprite;

            if (leftWall) leftWall.gameObject.SetActive(on);
            if (rightWall) rightWall.gameObject.SetActive(on);
        }
        else // Manual
        {
            ToggleManual(on);
        }
    }

    public void Refresh()
    {
        if (mode == BuildMode.Auto)
        {
            BindExistingChildren();
            ApplyAutoGeometry();
            AttachDampener(leftWall);
            AttachDampener(rightWall);
        }
        else
        {
            AttachDampener(manualLeftRoot);
            AttachDampener(manualRightRoot);
        }
    }

    /* ================= Utils & Gizmos ================= */
    Transform[] GetChildrenByName(string name)
    {
        return transform.Cast<Transform>().Where(t => t.name == name).ToArray();
    }

    void AttachDampener(Transform root)
    {
        if (!addDampenerToWalls || !root) return;
        var cols = root.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols)
        {
            var d = c.GetComponent<WallBounceDampen>();
            if (!d) d = c.gameObject.AddComponent<WallBounceDampen>();
            d.velocityScale = onHitVelocityScale;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        Gizmos.color = Color.yellow;
        var p0 = transform.TransformPoint(new Vector3(-baseOffsetX, baseOffsetY, 0));
        var p1 = transform.TransformPoint(new Vector3(+baseOffsetX, baseOffsetY, 0));
        Gizmos.DrawSphere(p0, 0.07f);
        Gizmos.DrawSphere(p1, 0.07f);
    }
}

/* ────────────────────────────────
 * Komponen kecil untuk meredam pantulan bola di dinding.
 * Tempel otomatis oleh BucketWallPower (addDampenerToWalls = true).
 * ────────────────────────────────*/
public class WallBounceDampen : MonoBehaviour
{
    [Range(0.05f, 1f)] public float velocityScale = 0.1f; // 0.1 = hilang 90%

    void OnCollisionEnter2D(Collision2D col)
    {
        var rb = col.rigidbody;               // RB dari collider yang menabrak (bola)
        if (!rb) return;
        rb.velocity *= velocityScale;
    }
}
