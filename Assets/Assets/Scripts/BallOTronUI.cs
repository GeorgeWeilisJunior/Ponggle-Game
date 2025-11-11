using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class BallOTronUI : MonoBehaviour
{
    /* ───────── Inspector ───────── */
    [Header("References")]
    [SerializeField] TMP_Text countText;
    [SerializeField] Transform ballStackContainer;
    [SerializeField] GameObject ballIconPrefab;
    [SerializeField] RectTransform plunger;
    [SerializeField] float plungerStep = 25f;

    [Header("Plunger rebound")]
    [SerializeField] float reboundDelay = 5f;

    /* ───────── Runtime ───────── */
    public static BallOTronUI Instance { get; private set; }

    readonly List<GameObject> icons = new();
    Coroutine reboundRoutine;

    /* ═══════════ LIFECYCLE ═══════════ */
    void Awake() => Instance = this;

    void Start()
    {
        RebuildIcons(GameManager.Instance.BallsLeft);

        GameManager.Instance.OnBallsLeftChanged += RebuildIcons;
        GameManager.Instance.OnBallUsed += LowerPlunger;
        GameManager.Instance.OnBallGained += RaisePlunger;
    }

    /* ═══════════ ICON MAINTENANCE ═══════════ */
    void RebuildIcons(int count)
    {
        countText.text = count.ToString();

        while (icons.Count < count)
            icons.Add(Instantiate(ballIconPrefab, ballStackContainer));

        while (icons.Count > count)
        {
            Destroy(icons[^1]);
            icons.RemoveAt(icons.Count - 1);
        }
    }

    /* Public API dipanggil GameManager saat Fever selesai */
    public void ConsumeOneBall()
    {
        if (icons.Count == 0) return;

        Destroy(icons[^1]);
        icons.RemoveAt(icons.Count - 1);

        countText.text = icons.Count.ToString();
        MovePlunger(-plungerStep);
        AudioManager.I.Play("Plop", plunger.position);
    }

    public void ClearAllBalls()
    {
        foreach (var go in icons) Destroy(go);
        icons.Clear();
        countText.text = "0";
        // reset plunger (turun penuh)
        var p = plunger.anchoredPosition;
        p.y -= plungerStep * GameManager.Instance.BallsLeft;
        plunger.anchoredPosition = p;
    }

    /* ═══════════ PLUNGER ANIM ═══════════ */
    void LowerPlunger()
    {
        MovePlunger(-plungerStep);

        if (reboundRoutine != null) StopCoroutine(reboundRoutine);
        reboundRoutine = StartCoroutine(Rebound());
    }

    IEnumerator Rebound()
    {
        yield return new WaitForSeconds(reboundDelay);
        MovePlunger(+plungerStep);          // naik lagi
    }

    void RaisePlunger() => MovePlunger(+plungerStep);

    void MovePlunger(float delta)
    {
        var pos = plunger.anchoredPosition;
        pos.y += delta;
        plunger.anchoredPosition = pos;
    }
}
