using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// Controller untuk Character Selection dengan dukungan lock Aposda & Porky.
public class CharacterSelectController : MonoBehaviour
{
    [Header("Big Portraits (hanya satu aktif)")]
    [SerializeField] GameObject portraitNeso;
    [SerializeField] GameObject portraitTuKaLa;
    [SerializeField] GameObject portraitAposda;
    [SerializeField] GameObject portraitPorky;
    [Tooltip("GO overlay gembok di dalam BigPortrait/Locked")]
    [SerializeField] GameObject portraitLockedOverlay;

    [Header("Detail Boxes")]
    [SerializeField] GameObject nesoDetail;
    [SerializeField] GameObject tuKaLaDetail;
    [SerializeField] GameObject aposdaDetail;      // detail asli
    [SerializeField] GameObject pigDetail;         // detail asli
    [SerializeField] GameObject lockedAposdaDetail; // versi LOCKED (UI Image or panel)
    [SerializeField] GameObject lockedPorkyDetail;  // versi LOCKED (UI Image or panel)

    [Header("Name Images (aktif hanya satu)")]
    [SerializeField] GameObject nameNeso;
    [SerializeField] GameObject nameTuKaLa;
    [SerializeField] GameObject nameAposda;
    [SerializeField] GameObject namePorky;

    [Header("UI Buttons & Labels")]
    [SerializeField] Button btnLeft;
    [SerializeField] Button btnRight;
    [SerializeField] Button btnConfirm;
    [SerializeField] TMP_Text nameLabel; // optional (tulisan nama di NameBox)

    [Header("Config")]
    [Tooltip("Urutan karakter di UI")]
    [SerializeField] string[] characterIds = { "NesoNesaNesda", "Tu Ka La", "Aposda", "Porky" };
    [SerializeField] int startIndex = 0;

    [Header("Flow")]
    [SerializeField] string nextSceneName = "Level 1-1";

    // Optional auto-unlock Aposda dari counter reactions (kalau kamu mau)
    [SerializeField, Min(0)] int aposdaRequiredReactions = 6;
    [SerializeField] bool autoUnlockAposdaFromCounters = true;

    int index;
    bool[] unlocked = new bool[4];

    void Start()
    {
        // sinkron unlock flags dari save
        RefreshUnlockFlags();
        index = Mathf.Clamp(startIndex, 0, characterIds.Length - 1);
        ApplySelection(true);
    }

    // =========================================================
    // Unlock states
    // =========================================================
    void RefreshUnlockFlags()
    {
        var data = SaveManager.I ? SaveManager.I.Data : null;
        // default: Neso & Tu Ka La selalu unlock
        bool aposda = false, porky = false;

        if (data != null)
        {
            // (opsional) auto-unlock Aposda dari penghitung reactions jika flag belum diset
            if (autoUnlockAposdaFromCounters && !data.aposdaUnlocked && data.elementReactionsSeen != null)
            {
                if (data.elementReactionsSeen.Count >= aposdaRequiredReactions)
                {
                    data.aposdaUnlocked = true;
                    SaveManager.I.SaveToDisk();
                }
            }

            aposda = data.aposdaUnlocked;
            // Porky terbuka setelah game terselesaikan sekali (dua flag ini kamu punya)
            porky = data.porkyUnlocked || data.gameClearedOnce;
        }

        unlocked = new bool[] { true, true, aposda, porky };
    }

    bool IsLocked(int i) => i >= 0 && i < unlocked.Length ? !unlocked[i] : true;

    // =========================================================
    // Navigation
    // =========================================================
    public void OnLeft()
    {
        index = (index - 1 + characterIds.Length) % characterIds.Length;
        ApplySelection();
    }

    public void OnRight()
    {
        index = (index + 1) % characterIds.Length;
        ApplySelection();
    }

    public void OnConfirm()
    {
        if (IsLocked(index))
        {
            AudioManager.I?.Play("MainMenuClick", Camera.main ? Camera.main.transform.position : Vector3.zero);
            btnConfirm.interactable = false;
            return;
        }

        string key = characterIds[index];             // mis. "NesoNesaNesda"
        SaveManager.I?.SetChosenCharacterKey(key);
        SaveManager.I?.SetSelectedCharacterIndex(index);  // fallback legacy (aman)
        SaveManager.I?.SaveToDisk();                         // PAKSA tulis ke disk

        Debug.Log($"[CharacterSelect] ChosenCharacterKey = {key} (idx {index})");

        // Pindah scene
        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            Debug.LogWarning("[CharacterSelect] nextSceneName kosong. Set di Inspector.");
    }


    // =========================================================
    // Visual state
    // =========================================================
    void ApplySelection(bool firstApply = false)
    {
        // aktifkan portrait yang tepat
        portraitNeso.SetActive(index == 0);
        portraitTuKaLa.SetActive(index == 1);
        portraitAposda.SetActive(index == 2);
        portraitPorky.SetActive(index == 3);

        // overlay + buram jika locked
        bool locked = IsLocked(index);
        if (portraitLockedOverlay) portraitLockedOverlay.SetActive(locked);
        DimActivePortrait(locked ? 0.55f : 1f);

        // detail box (normal vs locked)
        nesoDetail.SetActive(index == 0);
        tuKaLaDetail.SetActive(index == 1);

        bool showAposNormal = (index == 2) && !locked;
        bool showPigNormal = (index == 3) && !locked;
        bool showAposLock = (index == 2) && locked;
        bool showPigLock = (index == 3) && locked;

        if (aposdaDetail) aposdaDetail.SetActive(showAposNormal);
        if (pigDetail) pigDetail.SetActive(showPigNormal);
        if (lockedAposdaDetail) lockedAposdaDetail.SetActive(showAposLock);
        if (lockedPorkyDetail) lockedPorkyDetail.SetActive(showPigLock);

        // label nama (opsional text)
        if (nameLabel)
            nameLabel.text = characterIds[Mathf.Clamp(index, 0, characterIds.Length - 1)];

        // NEW: name images
        if (nameNeso) nameNeso.SetActive(index == 0);
        if (nameTuKaLa) nameTuKaLa.SetActive(index == 1);
        if (nameAposda) nameAposda.SetActive(index == 2);
        if (namePorky) namePorky.SetActive(index == 3);

        // Confirm hanya aktif kalau tidak locked
        if (btnConfirm) btnConfirm.interactable = !locked;


        // SFX navigasi
        if (!firstApply) AudioManager.I?.Play("MainMenuClick", Camera.main ? Camera.main.transform.position : Vector3.zero);
    }

    void DimActivePortrait(float alpha)
    {
        // ambil portrait aktif
        GameObject root =
            index == 0 ? portraitNeso :
            index == 1 ? portraitTuKaLa :
            index == 2 ? portraitAposda :
            portraitPorky;

        // kalau ada CanvasGroup, pakai itu
        var cg = root.GetComponent<CanvasGroup>();
        if (cg) { cg.alpha = alpha; return; }

        // fallback: set semua Image alpha
        var imgs = root.GetComponentsInChildren<Image>(true);
        foreach (var img in imgs)
        {
            var c = img.color; c.a = alpha; img.color = c;
        }
    }
}
