using TMPro;
using UnityEngine;

public class ScoreHUD : MonoBehaviour
{
    [SerializeField] TMP_Text scoreText;

    void OnEnable() => ScoreManager.OnScoreChanged += Refresh;
    void OnDisable() => ScoreManager.OnScoreChanged -= Refresh;

    void Refresh(int total, int _)
    {
        scoreText.text = total.ToString("N0",
           new System.Globalization.CultureInfo("id-ID")); // 1.851.610
    }
}
