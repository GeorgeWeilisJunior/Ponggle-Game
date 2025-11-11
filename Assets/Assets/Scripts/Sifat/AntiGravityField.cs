using UnityEngine;

/// Area yang meniadakan gravitasi & mematikan pantulan selama berada di dalamnya.
/// Perlu Collider2D (isTrigger = ON). Visual bebas (sprite/particle).
[RequireComponent(typeof(Collider2D))]
public class AntiGravityField : MonoBehaviour
{
    [Header("Efek Override Saat DI DALAM area")]
    public float gravityScaleOverride = 0f;
    public float dragOverride = 0.15f;
    [Range(0f, 1f)] public float bouncinessOverride = 0f;

    [Header("Durasi refresh (detik)")]
    [Tooltip("Berapa lama satu aplikasi efek bertahan sebelum di-refresh lagi.\n"
           + "Nilai kecil memastikan efek tidak habis bila bola masih di dalam area.")]
    [Min(0.02f)] public float refreshDuration = 0.25f;

    [Header("SFX (opsional)")]
    public string enterSfxKey = "";   // mainkan sekali saat masuk
    public string exitSfxKey = "";    // mainkan sekali saat keluar

    [Header("Gizmo (opsional)")]
    public Color gizmoColor = new Color(0.3f, 0.8f, 1f, 0.2f);

    PhysicsMaterial2D noBounceMat;

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        noBounceMat = new PhysicsMaterial2D("AG_NoBounce")
        {
            bounciness = bouncinessOverride,
            friction = 0f
        };
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;

        // mainkan SFX sekali saat masuk
        if (!string.IsNullOrEmpty(enterSfxKey) && AudioManager.I)
            AudioManager.I.Play(enterSfxKey, other.transform.position);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // selama di dalam area, jaga efek tetap aktif (di-refresh)
        if (!other.TryGetComponent<Rigidbody2D>(out _)) return;

        var eff = other.GetComponent<AntiGravityEffect>();
        if (!eff) eff = other.gameObject.AddComponent<AntiGravityEffect>();
        eff.Apply(gravityScaleOverride, dragOverride, noBounceMat, refreshDuration);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;

        // mainkan SFX sekali saat keluar (opsional)
        if (!string.IsNullOrEmpty(exitSfxKey) && AudioManager.I)
            AudioManager.I.Play(exitSfxKey, other.transform.position);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        var c = GetComponent<Collider2D>();
        if (!c) return;

        if (c is BoxCollider2D bc)
        {
            var pos = transform.TransformPoint(bc.offset);
            var size = Vector3.Scale(bc.size, transform.lossyScale);
            Gizmos.DrawCube(pos, size);
        }
        else if (c is CircleCollider2D cc)
        {
            var pos = transform.TransformPoint(cc.offset);
            float r = cc.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            Gizmos.DrawSphere(pos, r);
        }
        else if (c is CapsuleCollider2D cap)
        {
            var pos = transform.TransformPoint(cap.offset);
            var size = new Vector3(
                cap.size.x * Mathf.Abs(transform.lossyScale.x),
                cap.size.y * Mathf.Abs(transform.lossyScale.y),
                0f
            );
            Gizmos.DrawCube(pos, size);
        }
    }
}
