using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardUI : MonoBehaviour
{
    [Header("Refs")]
    public CanvasGroup cg;            // di Panel_Leaderboard
    public Button btnClose;           // Dialog/Btn_Close
    public Transform leftColumn;      // Content/LeftColumn
    public Transform rightColumn;     // Content/RightColumn
    public LeaderboardRowUI rowPrefab;

    [Header("Menu / Dim")]
    public MenuManager menu;          // drag MenuManager (untuk nyalain dim)

    [Header("Board Key")]
    [Tooltip("Kunci papan untuk total Adventure 25 level.")]
    public string boardKey = "Adventure_All25";

    [Header("Audio")]
    [SerializeField] private string uiClickSfxKey = "MainMenuClick";

    readonly List<GameObject> pooled = new();

    void Reset() { cg = GetComponent<CanvasGroup>(); }

    void Awake()
    {
        HideImmediate();
    }

    // --- kecil saja util SFX ---
    void PlayClick()
    {
        if (AudioManager.I != null && !string.IsNullOrEmpty(uiClickSfxKey))
            AudioManager.I.PlayUI(uiClickSfxKey);
    }

    void OnEnable()
    {
        if (LocalLeaderboardManager.I)
            LocalLeaderboardManager.I.OnChanged += Refresh;
    }

    void OnDisable()
    {
        if (LocalLeaderboardManager.I)
            LocalLeaderboardManager.I.OnChanged -= Refresh;
    }

    // === Public ===
    public void Show()
    {
        PlayClick();
        if (menu) menu.ShowDim();

        // sinkronkan key dari SaveManager (anti-mismatch inspector)
        if (SaveManager.I != null) boardKey = SaveManager.I.GetLeaderboardKey();

        // flush pending sebelum tampil
        SaveManager.I?.TryFlushPendingLeaderboard();

        gameObject.SetActive(true);
        if (cg)
        {
            cg.alpha = 0f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }

        Refresh();
        StopAllCoroutines();
        StartCoroutine(Fade(0f, 1f, 0.15f));
    }


    public void Hide()
    {
        PlayClick();
        if (cg)
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
        StopAllCoroutines();
        StartCoroutine(Fade(1f, 0f, 0.12f, () => {
            gameObject.SetActive(false);
            if (menu) menu.HideDim();
        }));
    }

    public void HideImmediate()
    {
        if (cg)
        {
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        foreach (var go in pooled) Destroy(go);
        pooled.Clear();

        var list = LocalLeaderboardManager.I
            ? LocalLeaderboardManager.I.GetTop(boardKey, 12)
            : System.Array.Empty<LocalLeaderboardManager.Entry>();

        int total = Mathf.Min(12, list.Count);

        for (int i = 0; i < 12; i++)
        {
            var targetCol = (i < 6) ? leftColumn : rightColumn;
            var row = Instantiate(rowPrefab, targetCol);
            pooled.Add(row.gameObject);

            if (i < total)
            {
                var e = list[i];
                row.SetData(i + 1, e.name, e.score);
            }
            else
            {
                row.SetData(i + 1, "-", 0);
            }
        }
    }

    System.Collections.IEnumerator Fade(float a, float b, float dur, System.Action done = null)
    {
        if (!cg) { done?.Invoke(); yield break; }
        float t = 0f;
        cg.alpha = a;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(a, b, t / dur);
            yield return null;
        }
        cg.alpha = b;
        done?.Invoke();
    }
}
