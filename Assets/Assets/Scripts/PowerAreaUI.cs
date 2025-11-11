using TMPro;
using UnityEngine;
using System.Collections;

/// <summary>
/// Menampilkan nama power; wavy + rainbow aktif hanya ketika
/// CharacterPowerManager.IsPowerReady == true.
/// </summary>
public class PowerAreaUI : MonoBehaviour
{
    [SerializeField] TMP_Text powerNameText;
    [SerializeField] CharacterPowerManager powerManager;

    Coroutine rainbowRoutine;
    Color normalColor;

    void Awake()
    {
        if (!powerManager) powerManager = CharacterPowerManager.Instance;
        if (!powerNameText) powerNameText = GetComponentInChildren<TMP_Text>();

        normalColor = powerNameText.color;          // oranye/cokelat awal
    }

    void OnEnable()
    {
        if (powerManager != null)
            powerManager.OnPowerChanged += HandlePowerChanged;

        HandlePowerChanged(powerManager ? powerManager.CurrentPowerName : "—");
    }

    void OnDisable()
    {
        if (powerManager != null)
            powerManager.OnPowerChanged -= HandlePowerChanged;

        StopRainbow();
    }

    /* ───────── EVENT ───────── */
    void HandlePowerChanged(string powerName)
    {
        powerNameText.text = string.IsNullOrEmpty(powerName) ? "—" : powerName;

        if (powerManager != null && powerManager.IsPowerReady)
            StartRainbow();
        else
            ApplyPlain();
    }

    /* ───────── STYLE ───────── */
    void ApplyPlain()
    {
        StopRainbow();
        powerNameText.color = normalColor;          // balik ke oranye
        powerNameText.ForceMeshUpdate();
        powerNameText.UpdateVertexData();           // sinkron warna default
    }

    void StartRainbow()
    {
        StopRainbow();
        powerNameText.color = Color.white;          // dasar putih supaya cerah
        rainbowRoutine = StartCoroutine(RainbowWavy());
    }

    void StopRainbow()
    {
        if (rainbowRoutine != null)
        {
            StopCoroutine(rainbowRoutine);
            rainbowRoutine = null;
        }
    }

    /* ───────── ANIMASI ───────── */
    IEnumerator RainbowWavy()
    {
        float waveSpeed = 5f;
        float waveHeight = 5f;
        float hueShift = 0f;

        TMP_TextInfo textInfo = powerNameText.textInfo;
        Vector3[][] baseVerts = new Vector3[textInfo.meshInfo.Length][];

        // Cache posisi asli
        for (int m = 0; m < textInfo.meshInfo.Length; m++)
            baseVerts[m] = textInfo.meshInfo[m].vertices.Clone() as Vector3[];

        while (true)
        {
            powerNameText.ForceMeshUpdate();
            textInfo = powerNameText.textInfo;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible) continue;

                int mIdx = textInfo.characterInfo[i].materialReferenceIndex;
                int vIdx = textInfo.characterInfo[i].vertexIndex;

                var verts = textInfo.meshInfo[mIdx].vertices;
                var colors = textInfo.meshInfo[mIdx].colors32;

                /* ← Posisi awal */
                verts[vIdx + 0] = baseVerts[mIdx][vIdx + 0];
                verts[vIdx + 1] = baseVerts[mIdx][vIdx + 1];
                verts[vIdx + 2] = baseVerts[mIdx][vIdx + 2];
                verts[vIdx + 3] = baseVerts[mIdx][vIdx + 3];

                /* Wavy offset */
                float yOffset = Mathf.Sin(Time.time * waveSpeed + i * 0.5f)
                                * waveHeight;
                Vector3 off = new Vector3(0, yOffset, 0);

                for (int v = 0; v < 4; v++) verts[vIdx + v] += off;

                /* Rainbow color */
                float hue = Mathf.Repeat(hueShift + i * 0.12f, 1f);
                Color32 col = Color.HSVToRGB(hue, 1f, 1f);

                for (int v = 0; v < 4; v++) colors[vIdx + v] = col;
            }

            /* PUSH perubahan ke GPU satu kali */
            powerNameText.UpdateVertexData(
                TMP_VertexDataUpdateFlags.Vertices |
                TMP_VertexDataUpdateFlags.Colors32);

            hueShift += Time.deltaTime * 0.5f;
            yield return null;
        }
    }
}
