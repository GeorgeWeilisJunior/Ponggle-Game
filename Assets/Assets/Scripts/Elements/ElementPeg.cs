using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ElementPeg : MonoBehaviour
{
    [Header("Element for this peg")]
    public ElementType element = ElementType.Fire;

    [Header("Rules")]
    [Tooltip("Jika ON, Next Ball hanya diubah ketika bola yang menabrak sedang NEUTRAL.")]
    public bool onlyWhenBallIsNeutral = true;

    void OnCollisionEnter2D(Collision2D c)
    {
        // hanya respon ke bola
        if (!c.collider.CompareTag("Ball")) return;

        // cek elemen bola saat ini (untuk rule optional)
        var ballElem = c.collider.GetComponent<BallElement>();
        if (onlyWhenBallIsNeutral && ballElem && ballElem.Current != ElementType.Neutral)
            return; // bola sudah ber-elemen → abaikan (sesuai rule)

        // UPDATE NEXT BALL → elemen peg ini
        ElementSystem.SetNext(element);

        // overwrite by design:
        // kalau dalam 1 tembakan bola netral menyentuh beberapa peg elemen,
        // yang TERAKHIR kena akan memanggil SetNext lagi → nilai terakhir menang.

        // (opsional) SFX/VFX kecil bisa dimainkan di sini.
        // AudioManager.I.Play("ElementPick", transform.position);
    }
}
