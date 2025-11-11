using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Ponggle/Character Data")]
public class CharacterData : ScriptableObject
{
    public string characterName;
    public Sprite portrait;
    [TextArea] public string description;

    [Header("Power")]
    public string powerName;
    [TextArea] public string powerDescription;
    public GameObject powerEffectPrefab;

    [Header("SFX")]
    public string powerSfxKey;
    public AudioClip fallbackClip;

    [Header("Launcher Display")]
    [Tooltip("Prefab tampilan karakter untuk dipasang di launcher (punya mata/pupil).")]
    public GameObject launcherDisplayPrefab;
}
