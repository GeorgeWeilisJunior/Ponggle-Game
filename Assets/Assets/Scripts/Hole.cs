using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hole : MonoBehaviour
{
    [SerializeField] int holeIndex = 0;

    [Header("SFX (via AudioManager)")]
    [SerializeField] string sfxKey = "";          // isi: "BucketEnter" utk normal, "Fireworks1" utk fever
    [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;
    [SerializeField] bool ignorePause = true;     // biar tetap bunyi saat game ter-pause di akhir turn
    [SerializeField, Min(0f)] float minInterval = 0.03f; // anti dobel-trigger cepat

    [Header("Fever Popup (visual only)")]
    [SerializeField] PegScorePopup feverPopupPrefab;
    [SerializeField] Transform popupParent;       // drag: GameCanvas / popup parent yang sama dgn peg popup
    [SerializeField] Vector3 popupOffset = new Vector3(0f, 0.25f, 0f);
    [SerializeField] Color normalColor = new Color(1f, 0.95f, 0.6f);  // gold-ish
    [SerializeField] Color extremeColor = new Color(1f, 0.85f, 0.2f); // lebih “emas”
    [SerializeField, Min(0.1f)] float popupDuration = 0.7f;

    float _lastPlayTime = -999f;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;

        // MAIN: kirim event ke bucket controller (logika skor, anim, dsb)
        var bucket = BucketController.Instance;
        if (bucket != null)
            bucket.OnBallEnteredHole(holeIndex, other.gameObject);

        // SFX lewat AudioManager (UI/2D)
        if (!string.IsNullOrEmpty(sfxKey) && Time.unscaledTime - _lastPlayTime >= minInterval)
        {
            AudioManager.I?.PlayUI(sfxKey, sfxVolume, ignorePause);
            _lastPlayTime = Time.unscaledTime;
        }

        // === Fever Hole Score Popup (visual) ===
        if (feverPopupPrefab)
        {
            int amount = 0;

            if (BucketController.Instance != null)
            {
                amount = BucketController.Instance.GetActiveHoleScore(holeIndex);
            }

            if (amount <= 0) amount = 10000; // fallback aman

            bool isExtreme = BucketController.Instance != null && BucketController.Instance.IsExtremeFever;

            var pop = Instantiate(feverPopupPrefab,
                                  transform.position + popupOffset,
                                  Quaternion.identity,
                                  popupParent);

            string txt = "+" + amount.ToString("N0");
            pop.Show(txt, isExtreme ? extremeColor : normalColor, popupDuration);
        }

    }
}
