using UnityEngine;

public enum PegFamily
{
    Unknown = 0,
    Rounded = 1,
    Brick = 2,
    RoundedBrick = 3,
    MoreRoundedBrick = 4
}

public class PegFamilyTag : MonoBehaviour
{
    public PegFamily family = PegFamily.Unknown;
}
