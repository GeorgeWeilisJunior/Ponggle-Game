using UnityEngine;

public enum OffsetSpace { Local, World }

[ExecuteAlways]
public class KeepShadowFollow : MonoBehaviour
{
    public Vector2 offset = new(-0.03f, -0.06f);
    [Range(0.8f, 1.2f)] public float scale = 1.10f;
    public bool rotateWithPeg = true;
    public OffsetSpace offsetSpace = OffsetSpace.World;

    void LateUpdate()
    {
        var p = transform.parent;
        if (!p) return;

        Vector3 worldOffset =
            (offsetSpace == OffsetSpace.Local) ? p.TransformDirection(offset) : (Vector3)offset;

        transform.position = p.position + worldOffset;
        transform.rotation = rotateWithPeg ? p.rotation : Quaternion.identity;
        transform.localScale = Vector3.one * scale;
    }
}
