using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using DG.Tweening;

public class FeverMeterController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] Image fillImg;
    [SerializeField] Image topCapImg;
    [SerializeField] Image frameImg;

    [Header("Connectors (icon)")]
    [SerializeField] Image connectorX2;
    [SerializeField] Image connectorX3;
    [SerializeField] Image connectorX5;
    [SerializeField] Image connectorX10;

    [Header("Connectors (label child)")]
    [Tooltip("Tarik Image anak bernama X2/X3/X5/X10 ke sini.")]
    [SerializeField] Image connectorX2Label;
    [SerializeField] Image connectorX3Label;
    [SerializeField] Image connectorX5Label;
    [SerializeField] Image connectorX10Label;

    [Header("Crown")]
    [SerializeField] Transform crown;
    [SerializeField] float crownPopDuration = 0.35f;

    [Header("Colors")]
    [SerializeField] Color offColor = new(.75f, .75f, .75f, 1f);
    [SerializeField] Color onColor = new(1f, .6f, .2f, 1f);
    [SerializeField] Color topCapGold = new(1f, .85f, .35f, 1f);

    [Header("Thresholds (0–1)")]
    [SerializeField, Range(0, 1)] float tX2 = .25f;
    [SerializeField, Range(0, 1)] float tX3 = .50f;
    [SerializeField, Range(0, 1)] float tX5 = .75f;
    [SerializeField, Range(0, 1)] float tX10 = .90f;

    [Header("Tolerance")]
    [SerializeField, Range(0f, 0.05f)] float thresholdSlack = 0.01f; // 1% toleransi

    [Header("Mode Isi")]
    [SerializeField] bool fillOnHit = true;
    public bool FillOnHit => fillOnHit;

    int extremeOrangeTotal;
    int orangeCleared;
    int destroyedPegs;
    int totalPegsInLevel;

    bool extremeTriggered;
    bool ultraTriggered;

    readonly HashSet<int> hitOranges = new HashSet<int>();

    bool aX2, aX3, aX5, aX10;

    public bool IsExtreme => extremeTriggered;
    public float FillPercent => fillImg ? fillImg.fillAmount : 0f;

    void Awake()
    {
        var allPegs = FindObjectsOfType<PegController>();
        totalPegsInLevel = allPegs.Length;
        extremeOrangeTotal = Mathf.Max(1, allPegs.Count(p => p.Type == PegType.Orange));
    }

    void Start() => ResetMeter();

    void OnEnable()
    {
        PegController.OnPegCleared += HandlePegCleared;
        ScoreManager.OnMultiplierChanged += HandleMultiplierChanged;
    }

    void OnDisable()
    {
        PegController.OnPegCleared -= HandlePegCleared;
        ScoreManager.OnMultiplierChanged -= HandleMultiplierChanged;
    }

    void HandlePegCleared(PegType type)
    {
        destroyedPegs++;

        if (type == PegType.Orange && !fillOnHit)
            IncrementOrangeAndTween();

        if (!ultraTriggered && destroyedPegs >= totalPegsInLevel)
            TriggerUltraExtreme();
    }

    public void AddOrangeHitInstant(int pegInstanceId)
    {
        if (!fillOnHit) return;
        if (hitOranges.Contains(pegInstanceId)) return;
        hitOranges.Add(pegInstanceId);
        IncrementOrangeAndTween();
    }

    public void AddOrangeHitInstant()
    {
        if (!fillOnHit) return;
        IncrementOrangeAndTween();
    }

    void IncrementOrangeAndTween()
    {
        if (orangeCleared >= extremeOrangeTotal) return;

        orangeCleared++;
        float progress = Mathf.Clamp01((float)orangeCleared / extremeOrangeTotal);

        ScoreManager.SetMultiplierFromFever(progress, tX2, tX3, tX5, tX10, thresholdSlack);

        if (fillImg)
            fillImg.DOFillAmount(progress, 0.20f).SetEase(Ease.OutQuad)
                   .OnUpdate(() => UpdateConnectors(fillImg.fillAmount));

        UpdateConnectors(progress);

        if (!extremeTriggered && orangeCleared >= extremeOrangeTotal)
            TriggerExtreme();
    }

    void UpdateConnectors(float p)
    {
        float s = thresholdSlack;
        TryActivate(connectorX2, connectorX2Label, ref aX2, p + s >= tX2);
        TryActivate(connectorX3, connectorX3Label, ref aX3, p + s >= tX3);
        TryActivate(connectorX5, connectorX5Label, ref aX5, p + s >= tX5);
        TryActivate(connectorX10, connectorX10Label, ref aX10, p + s >= tX10);
    }

    void TryActivate(Image icon, Image label, ref bool already, bool condition)
    {
        if (icon) icon.DOKill();
        if (label) label.DOKill();

        if (condition && !already)
        {
            already = true;
            if (icon) icon.DOColor(onColor, 0.12f);
            if (label) label.DOColor(onColor, 0.12f);
            // TANPA punch/scale — supaya tidak membesar sama sekali
            try { AudioManager.I.PlayUI("FeverTick"); } catch { }
        }
        else if (!condition && already)
        {
            already = false;
            if (icon) icon.DOColor(offColor, 0.12f);
            if (label) label.DOColor(offColor, 0.12f);
        }
        else
        {
            // sinkron warna saat pertama kali setup
            if (icon) icon.color = condition ? onColor : offColor;
            if (label) label.color = condition ? onColor : offColor;
        }
    }

    void HandleMultiplierChanged(int mult)
    {
        if (!topCapImg) return;
        // matikan nudge; hanya ubah warna saat extreme
        topCapImg.color = extremeTriggered ? topCapGold : Color.white;
    }

    void TriggerExtreme()
    {
        if (extremeTriggered) return;
        extremeTriggered = true;

        if (fillImg) fillImg.DOFillAmount(1f, 0.2f).SetEase(Ease.OutQuad);
        if (topCapImg) topCapImg.DOColor(topCapGold, 0.15f);

        if (frameImg)
        {
            var baseCol = frameImg.color;
            frameImg.color = new Color(onColor.r, onColor.g, onColor.b, baseCol.a);
            frameImg.DOFade(baseCol.a, 0.35f).SetEase(Ease.OutSine);
        }

        if (crown)
        {
            crown.gameObject.SetActive(true);
            crown.localScale = Vector3.zero;
            crown.DOScale(1f, crownPopDuration).SetEase(Ease.OutBack);
            crown.DOLocalRotate(new Vector3(0, 0, -10f), 0.15f).From(new Vector3(0, 0, 20f));
        }

        try { AudioManager.I.PlayUI("FeverStart"); } catch { }
        Debug.Log("🔥 Extreme Fever!");
    }

    void TriggerUltraExtreme()
    {
        if (ultraTriggered) return;
        ultraTriggered = true;
        Debug.Log("🚀 Ultra Extreme Fever!");
    }

    public void ResetMeter()
    {
        orangeCleared = destroyedPegs = 0;
        extremeTriggered = ultraTriggered = false;
        hitOranges.Clear();

        aX2 = aX3 = aX5 = aX10 = false;

        ScoreManager.SetMultiplierFromFever(0f, tX2, tX3, tX5, tX10, thresholdSlack);

        if (fillImg) fillImg.DOFillAmount(0f, 0.2f).SetEase(Ease.InOutSine);
        if (topCapImg) topCapImg.color = Color.white;

        SetConnectorColor(connectorX2, connectorX2Label, offColor);
        SetConnectorColor(connectorX3, connectorX3Label, offColor);
        SetConnectorColor(connectorX5, connectorX5Label, offColor);
        SetConnectorColor(connectorX10, connectorX10Label, offColor);

        if (frameImg) frameImg.DOKill();
        if (crown)
        {
            crown.DOKill();
            crown.gameObject.SetActive(false);
            crown.localScale = Vector3.one;
        }
    }

    void SetConnectorColor(Image icon, Image label, Color c)
    {
        if (icon) icon.color = c;
        if (label) label.color = c;
    }
}
