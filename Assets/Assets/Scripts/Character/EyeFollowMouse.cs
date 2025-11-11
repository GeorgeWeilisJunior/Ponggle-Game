using UnityEngine;

public class EyeFollowMouse : MonoBehaviour
{
    public enum ClampShape { Circle, Ellipse }

    [System.Serializable]
    public class Eye
    {
        [Header("Anchors")]
        public Transform center;   // titik tengah bola mata
        public Transform pupil;    // sprite pupil

        [Header("Clamp")]
        public ClampShape shape = ClampShape.Circle;
        [Tooltip("Radius gerak (world units). Circle=pakai X saja, Ellipse=pakai X=rx, Y=ry")]
        public Vector2 radius = new Vector2(0.08f, 0.08f);

        [Header("Tuning")]
        [Range(0f, 30f)] public float followLerp = 18f; // kecepatan smoothing khusus mata ini
    }

    [Header("Eyes")]
    [SerializeField] Eye leftEye;
    [SerializeField] Eye rightEye;

    Camera cam;

    void Awake() { cam = Camera.main; }
    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        MoveOne(leftEye, mouseWorld);
        MoveOne(rightEye, mouseWorld);
    }

    void MoveOne(Eye e, Vector3 targetWorld)
    {
        if (e == null || !e.center || !e.pupil) return;

        // ke ruang lokal si mata → clamp ellipse/circle → kembali ke world
        Vector3 local = e.center.InverseTransformPoint(targetWorld);
        local.z = 0f;

        Vector2 r = e.radius;
        Vector3 clamped;

        if (e.shape == ClampShape.Circle)
        {
            Vector2 v = new Vector2(local.x, local.y);
            v = Vector2.ClampMagnitude(v, Mathf.Max(0f, r.x));
            clamped = new Vector3(v.x, v.y, 0f);
        }
        else // Ellipse
        {
            // skala ke unit circle, clamp, balikkan skala
            float rx = Mathf.Max(0.0001f, r.x);
            float ry = Mathf.Max(0.0001f, r.y);
            Vector2 v = new Vector2(local.x / rx, local.y / ry);
            float m = v.magnitude;
            if (m > 1f) v /= m;
            clamped = new Vector3(v.x * rx, v.y * ry, 0f);
        }

        Vector3 desiredWorld = e.center.TransformPoint(clamped);
        desiredWorld.z = e.pupil.position.z;

        float k = 1f - Mathf.Exp(-(e.followLerp <= 0f ? 18f : e.followLerp) * Time.deltaTime);
        e.pupil.position = Vector3.Lerp(e.pupil.position, desiredWorld, k);
    }

    /* --- bantu visualize di editor --- */
    void OnDrawGizmosSelected()
    {
        DrawEyeGizmo(leftEye, Color.cyan);
        DrawEyeGizmo(rightEye, Color.magenta);
    }
    void DrawEyeGizmo(Eye e, Color c)
    {
        if (e == null || !e.center) return;
        Gizmos.color = c;
        if (e.shape == ClampShape.Circle)
            DrawCircle(e.center.position, e.radius.x, 24);
        else
            DrawEllipse(e.center.position, e.radius.x, e.radius.y, 28, e.center.rotation.eulerAngles.z);
    }
    void DrawCircle(Vector3 p, float r, int seg)
    {
        Vector3 prev = p + new Vector3(r, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            float ang = i * Mathf.PI * 2f / seg;
            Vector3 cur = p + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0);
            Gizmos.DrawLine(prev, cur); prev = cur;
        }
    }
    void DrawEllipse(Vector3 p, float rx, float ry, int seg, float zRot)
    {
        Quaternion q = Quaternion.Euler(0, 0, zRot);
        Vector3 prev = p + q * new Vector3(rx, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            float ang = i * Mathf.PI * 2f / seg;
            Vector3 cur = p + q * new Vector3(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry, 0);
            Gizmos.DrawLine(prev, cur); prev = cur;
        }
    }
}
