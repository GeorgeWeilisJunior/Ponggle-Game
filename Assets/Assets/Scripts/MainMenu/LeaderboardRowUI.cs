using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardRowUI : MonoBehaviour
{
    [Header("Refs")]
    public Image rankBox;     // kotak kecil di kiri
    public TMP_Text txtRank;
    public TMP_Text txtName;
    public TMP_Text txtScore;

    [Header("Colors")]
    public Color gold = new Color(1f, 0.88f, 0.35f); // #FFD95A kira-kira
    public Color silver = new Color(0.78f, 0.78f, 0.78f);
    public Color bronze = new Color(0.80f, 0.55f, 0.30f);
    public Color normal = new Color(1f, 1f, 1f, 0.15f);

    public void SetData(int rank, string name, int score)
    {
        if (txtRank) txtRank.text = rank.ToString();
        if (txtName) txtName.text = string.IsNullOrWhiteSpace(name) ? "-" : name.ToUpperInvariant();
        if (txtScore) txtScore.text = FormatScore(score);

        if (!rankBox) return;
        rankBox.color = rank switch
        {
            1 => gold,
            2 => silver,
            3 => bronze,
            _ => normal
        };
    }

    static string FormatScore(int s)
    {
        // 1.520.225
        return s.ToString("#,0").Replace(",", ".");
    }
}
