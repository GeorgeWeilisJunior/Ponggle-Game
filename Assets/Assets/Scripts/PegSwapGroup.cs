using UnityEngine;

public enum PegSwapGroupId
{
    Normal = 0,
    Hard = 1,
    Moving = 2,
    Rotating = 3,
    Custom4 = 4,
    Custom5 = 5,

    // Tambahan baru — letakkan SETELAH nilai lama agar tidak menggeser serialized int
    AntiGravity = 6,
    Disappearing = 7,
    AntiGravityAndDisappearing = 8
}

public class PegSwapGroup : MonoBehaviour
{
    public PegSwapGroupId group = PegSwapGroupId.Normal;
}
