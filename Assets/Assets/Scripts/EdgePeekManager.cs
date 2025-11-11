using System.Linq;
using UnityEngine;

public class EdgePeekManager : MonoBehaviour
{
    public Camera cam;
    public Collider2D worldBounds;     // Box/PolygonCollider2D yang membungkus arena
    [Range(0f, 0.2f)] public float edgeReveal = 0.04f; // seberapa dekat ke tepi untuk memunculkan
    public Transform ball;             // assign Ball transform runtime
    public EyeFollower[] eyes;         // isi semua mata yang kamu taruh di pinggir

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (eyes == null || eyes.Length == 0)
            eyes = FindObjectsOfType<EyeFollower>(true);
        foreach (var e in eyes) if (e) e.target = ball;
    }

    void LateUpdate()
    {
        if (!cam || !worldBounds) return;
        if (!ball)
        {
            var bc = FindObjectOfType<BallController>();
            if (bc)
            {
                ball = bc.transform;
                foreach (var e in eyes) if (e) e.target = ball; // set ke semua mata
            }
        }
        var b = worldBounds.bounds;
        float ortho = cam.orthographicSize;
        float halfW = ortho * cam.aspect;
        float halfH = ortho;

        // jarak viewport ke masing-masing tepi
        float leftGap = (cam.transform.position.x - halfW) - b.min.x;
        float rightGap = b.max.x - (cam.transform.position.x + halfW);
        float bottomGap = (cam.transform.position.y - halfH) - b.min.y;
        float topGap = b.max.y - (cam.transform.position.y + halfH);

        float thresh = edgeReveal * ortho;

        bool peeking = leftGap < thresh || rightGap < thresh || bottomGap < thresh || topGap < thresh;

        foreach (var e in eyes)
            if (e) e.SetVisible(peeking);
    }
}
