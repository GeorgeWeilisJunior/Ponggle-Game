using UnityEngine;

/// Tempel ke SpriteRenderer yang harus berada DI ATAS base order (mis. pupil).
public class SortingBump : MonoBehaviour
{
    [Tooltip("Naikkan Order in Layer relatif terhadap base order dari mount.")]
    public int delta = 1;
}
