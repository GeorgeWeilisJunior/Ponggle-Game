using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DrumController : MonoBehaviour
{
    [SerializeField] string sfxKey = "DrumHit";

    void OnCollisionEnter2D(Collision2D c)
    {
        if (!c.collider.CompareTag("Ball")) return;

        // Titik kontak pertama ≈ posisi suara
        Vector3 pos = c.contacts.Length > 0
            ? c.contacts[0].point
            : transform.position;

        AudioManager.I.Play(sfxKey, pos);
    }
}
