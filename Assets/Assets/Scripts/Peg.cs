using UnityEngine;
public class Peg : MonoBehaviour
{
    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.collider.CompareTag("Ball"))
            Destroy(gameObject);
    }
}
