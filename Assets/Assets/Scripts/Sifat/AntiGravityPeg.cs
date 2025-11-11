using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AntiGravityPeg : MonoBehaviour
{
    [Header("Durasi Efek")]
    public float duration = 2.5f;

    [Header("Override Nilai")]
    public float gravityScaleOverride = 0f;
    public float dragOverride = 0.12f;
    [Range(0f, 1f)] public float bouncinessOverride = 0f;

    [Header("FX (opsional)")]
    public ParticleSystem onHitVfx;
    public string onHitSfxKey = "";  // ← pakai AudioManager

    PhysicsMaterial2D noBounceMat;

    void Awake()
    {
        noBounceMat = new PhysicsMaterial2D("AG_NoBounce_Peg")
        {
            bounciness = bouncinessOverride,
            friction = 0f
        };
    }

    void OnCollisionEnter2D(Collision2D collision) => TryApply(collision.collider);
    void OnTriggerEnter2D(Collider2D other) => TryApply(other);

    void TryApply(Collider2D other)
    {
        if (!other || !other.TryGetComponent<Rigidbody2D>(out _)) return;

        var eff = other.GetComponent<AntiGravityEffect>();
        if (!eff) eff = other.gameObject.AddComponent<AntiGravityEffect>();
        eff.Apply(gravityScaleOverride, dragOverride, noBounceMat, duration);

        if (onHitVfx) Instantiate(onHitVfx, transform.position, Quaternion.identity, transform.parent);

        if (!string.IsNullOrEmpty(onHitSfxKey) && AudioManager.I)
            AudioManager.I.Play(onHitSfxKey, transform.position);
    }
}
