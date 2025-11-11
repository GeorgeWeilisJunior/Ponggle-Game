using UnityEngine;
using TMPro;

public class NameBox : MonoBehaviour
{
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] string defaultName = "PLAYER";

    void OnEnable() => Refresh();

    public void Refresh()
    {
        if (!nameLabel) return;

        string s = (SaveManager.I != null) ? SaveManager.I.Data.playerName : defaultName;
        if (string.IsNullOrWhiteSpace(s)) s = defaultName;

        nameLabel.text = s.ToUpperInvariant();
    }
}
