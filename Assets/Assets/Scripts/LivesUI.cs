using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// UI nyawa 3 hati. Setiap kehilangan nyawa, Life -> LifeDeath dengan fade+scale.
/// Ambil sumber kebenaran dari SaveManager.I.Data.lives (0..3).
/// </summary>
public class LivesUI : MonoBehaviour
{
    [System.Serializable]
    public class HeartSlot
    {
        [Tooltip("Gambar hati hidup (GO 'Life')")]
        public GameObject alive;

        [Tooltip("Gambar hati mati (GO 'Life Death')")]
        public GameObject dead;

        [HideInInspector] public CanvasGroup aliveCg;
        [HideInInspector] public CanvasGroup deadCg;

        public void CacheCanvasGroups()
        {
            if (alive) aliveCg = EnsureCanvasGroup(alive);
            if (dead) deadCg = EnsureCanvasGroup(dead);
        }

        static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (!cg) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }
    }

    public static LivesUI Instance { get; private set; }

    [Header("Hearts (urut kiri → kanan)")]
    public HeartSlot[] hearts = new HeartSlot[3];

    [Header("Animasi")]
    [Min(0f)] public float fadeDur = 0.22f;
    [Min(0f)] public float scalePunch = 0.12f;     // besar “punch”
    [Min(0f)] public float scaleDur = 0.20f;       // durasi scale
    public Ease scaleEase = Ease.OutBack;

    [Header("Opsi")]
    [Tooltip("Isi hati dari kiri dulu (true) atau dari kanan (false).")]
    public bool fillFromLeft = true;

    int maxLives => hearts != null ? hearts.Length : 3;
    int shownLives = -1;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        foreach (var h in hearts)
            if (h != null) h.CacheCanvasGroups();
    }

    void OnEnable()
    {
        RefreshFromSave(immediate: true);
    }

    /// <summary>Panggil untuk set tampilan sesuai Save (mis. saat ganti scene atau reset stage).</summary>
    public void RefreshFromSave(bool immediate = false)
    {
        int lives = Mathf.Clamp(SaveManager.I != null ? SaveManager.I.Data.lives : maxLives, 0, maxLives);
        SetLives(lives, immediate);
    }

    /// <summary>Dipanggil setelah kehilangan nyawa. livesAfterLoss = nilai baru di Save.</summary>
    public void PlayLoseLife(int livesAfterLoss)
    {
        livesAfterLoss = Mathf.Clamp(livesAfterLoss, 0, maxLives);

        // kalau UI belum pernah init, langsung set saja
        if (shownLives < 0)
        {
            SetLives(livesAfterLoss, immediate: true);
            return;
        }

        // Jika angka naik (mis. reset stage → 3), langsung refresh (tanpa animasi lose).
        if (livesAfterLoss >= shownLives)
        {
            SetLives(livesAfterLoss, immediate: true);
            return;
        }

        // Turun: animasikan 1 per 1
        int toFlipCount = shownLives - livesAfterLoss;
        StartCoroutine(AnimateLoseSequence(toFlipCount));
    }

    IEnumerator AnimateLoseSequence(int count)
    {
        for (int i = 0; i < count; i++)
        {
            int flipIndex = GetLastAliveIndex();  // ambil hati hidup paling “akhir”
            if (flipIndex < 0) break;

            yield return StartCoroutine(FlipAliveToDead(hearts[flipIndex]));
            shownLives = Mathf.Max(0, shownLives - 1);
            yield return new WaitForSeconds(0.03f); // sedikit jeda
        }
    }

    int GetLastAliveIndex()
    {
        if (hearts == null || hearts.Length == 0) return -1;

        if (fillFromLeft)
        {
            for (int i = hearts.Length - 1; i >= 0; i--)
            {
                if (IsAlive(i)) return i;
            }
        }
        else
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                if (IsAlive(i)) return i;
            }
        }
        return -1;
    }

    bool IsAlive(int index)
    {
        var h = hearts[index];
        return h != null && h.alive && h.alive.activeSelf && (!h.dead || !h.dead.activeSelf);
    }

    IEnumerator FlipAliveToDead(HeartSlot h)
    {
        if (h == null || h.alive == null || h.dead == null) yield break;

        var rt = h.alive.transform as RectTransform;
        var alive = h.aliveCg; var dead = h.deadCg;

        // pastikan keduanya aktif agar bisa cross-fade
        h.alive.SetActive(true); h.dead.SetActive(true);

        // mulai dari: alive alpha 1, dead alpha 0
        alive.DOKill(); dead.DOKill();
        alive.alpha = 1f; dead.alpha = 0f;

        // scale punch pada parent slot (biar halus)
        if (rt)
        {
            rt.DOKill();
            Vector3 orig = rt.localScale;
            rt.DOScale(orig * (1f + scalePunch), scaleDur * 0.5f).SetEase(scaleEase).SetUpdate(true)
              .OnComplete(() =>
              {
                  rt.DOScale(orig, scaleDur * 0.5f).SetEase(Ease.OutQuad).SetUpdate(true);
              });
        }

        // cross-fade
        float d = Mathf.Max(0.0001f, fadeDur);
        float t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            alive.alpha = 1f - k;
            dead.alpha = k;
            yield return null;
        }

        alive.alpha = 0f; dead.alpha = 1f;
        h.alive.SetActive(false); // selesai: hanya dead yang aktif
    }

    void SetLives(int lives, bool immediate)
    {
        shownLives = lives;

        for (int i = 0; i < maxLives; i++)
        {
            bool shouldAlive = (fillFromLeft)
                ? (i < lives)           // 0..(lives-1) hidup
                : (i >= maxLives - lives); // dari kanan

            var h = hearts[i];
            if (h == null) continue;

            if (h.alive) h.alive.SetActive(shouldAlive);
            if (h.dead) h.dead.SetActive(!shouldAlive);

            if (h.aliveCg) h.aliveCg.alpha = shouldAlive ? 1f : 0f;
            if (h.deadCg) h.deadCg.alpha = shouldAlive ? 0f : 1f;

            if (!immediate && shouldAlive)
            {
                // kecilkan dikit lalu pop-in biar ‘soft’
                var rt = (h.alive.transform as RectTransform);
                if (rt)
                {
                    rt.DOKill();
                    rt.localScale = Vector3.one * 0.98f;
                    rt.DOScale(1f, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true);
                }
            }
        }
    }
}
