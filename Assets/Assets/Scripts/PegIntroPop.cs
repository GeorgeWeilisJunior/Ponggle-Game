using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class PegIntroPop : MonoBehaviour
{
    public static bool IsIntroPlaying { get; private set; } = false;

    // --- Guard: intro hanya sekali per level (reset dari GameManager) ---
    static bool _playedThisScene = false;
    public static void ResetFlags()
    {
        IsIntroPlaying = false;
        _playedThisScene = false;
    }

    public enum OrderMode { ByYAscending, Random, ByDistanceFromCenter }

    [Header("Target Filter")]
    [Tooltip("Kosong = ambil semua PegController aktif & belum Cleared.")]
    public List<PegController> explicitTargets;

    [Header("Ordering")]
    public OrderMode orderMode = OrderMode.ByYAscending;
    [Tooltip("Dipakai bila OrderMode = ByDistanceFromCenter.")]
    public Transform orderCenter;
    public Vector2 orderCenterFallback = Vector2.zero;

    [Header("Stagger & Timing")]
    [Range(0f, 1.5f)] public float maxStagger = 0.45f;
    [Range(0.1f, 0.6f)] public float popDuration = 0.25f;
    [Range(0.05f, 0.6f)] public float fadeDuration = 0.18f;

    [Header("Scale Pop")]
    [Range(0.2f, 1f)] public float startScale = 0.6f;
    [Range(1.0f, 2.5f)] public float overshoot = 1.6f;

    [Header("Fade")]
    public bool fadeSprites = true;
    [Range(0f, 1f)] public float startAlpha = 0f;

    [Header("Safety & Pause")]
    public bool lockLauncherWhilePlaying = true;
    public bool disableCollidersDuringIntro = true;
    [Tooltip("Matikan sementara Rigidbody2D dan komponen bergerak/rotasi.")]
    public bool pauseMotionDuringIntro = true;
    [Tooltip("Nama komponen yang akan dinonaktifkan sementara bila ditemukan.")]
    public string[] behaviourNameContains = new[] { "Rotate", "Mover", "Orbit" };

    [Header("Orchestration")]
    [Tooltip("Tunggu PegRandomizer selesai dulu sebelum memulai intro.")]
    public bool waitForRandomizer = true;

    [Header("Playback")]
    [Tooltip("Kalau ON, intro auto-play sekali saat Start bila belum pernah dimainkan.")]
    public bool autoPlayOnStart = false; // default OFF — GameManager yang memanggil

    [Header("Audio")]
    public string sfxPlopKey = "PegPop";
    [Range(1, 10)] public int sfxEveryN = 4;
    public bool sfxAs3D = false;

    IEnumerator Start()
    {
        if (!autoPlayOnStart) yield break;
        if (_playedThisScene || IsIntroPlaying) yield break;

        if (waitForRandomizer)
        {
            while (PegRandomizer.IsRandomizing) yield return null;

            if (!PegRandomizer.HasRunThisScene)
            {
                bool done = false;
                System.Action cb = () => done = true;
                PegRandomizer.OnRandomizeDone += cb;
                int guard = 0;
                while (!done && guard < 60) { guard++; yield return null; }
                PegRandomizer.OnRandomizeDone -= cb;
            }
        }

        yield return Play();
    }

    public IEnumerator Play()
    {
        if (IsIntroPlaying || _playedThisScene) yield break; // guard
        IsIntroPlaying = true;

        IEnumerable<PegController> pegsEnum =
            (explicitTargets != null && explicitTargets.Count > 0)
            ? explicitTargets.Where(p => p && p.gameObject.activeInHierarchy && p.State != PegController.PegState.Cleared)
            : FindObjectsOfType<PegController>()
                .Where(p => p && p.gameObject.activeInHierarchy && p.State != PegController.PegState.Cleared);

        var list = pegsEnum.ToList();
        if (list.Count == 0)
        {
            _playedThisScene = true;
            IsIntroPlaying = false;
            yield break;
        }

        switch (orderMode)
        {
            case OrderMode.Random:
                list = list.OrderBy(_ => Random.value).ToList();
                break;
            case OrderMode.ByDistanceFromCenter:
                Vector2 c = orderCenter ? (Vector2)orderCenter.position : orderCenterFallback;
                list = list.OrderBy(p => Vector2.SqrMagnitude((Vector2)p.transform.position - c)).ToList();
                break;
            default:
                list = list.OrderBy(p => p.transform.position.y).ToList();
                break;
        }

        if (lockLauncherWhilePlaying) Launcher.Instance?.LockInput();

        var originals = new Dictionary<Transform, Vector3>(list.Count);
        var spriteGroups = new Dictionary<Transform, SpriteRenderer[]>(list.Count);
        var rbs = new Dictionary<Transform, List<Rigidbody2D>>(list.Count);
        var pausedBehaviours = new Dictionary<Transform, List<Behaviour>>(list.Count);

        foreach (var peg in list.ToList())
        {
            if (!peg) { list.Remove(peg); continue; }
            var tf = peg.transform;
            if (!tf) { list.Remove(peg); continue; }

            originals[tf] = tf.localScale;
            tf.localScale = originals[tf] * startScale;

            if (fadeSprites)
            {
                var srs = tf.GetComponentsInChildren<SpriteRenderer>(true);
                spriteGroups[tf] = srs;
                foreach (var sr in srs)
                {
                    if (!sr) continue;
                    var c = sr.color; c.a = startAlpha; sr.color = c;
                }
            }

            if (disableCollidersDuringIntro)
                foreach (var col in tf.GetComponentsInChildren<Collider2D>(true))
                    if (col) col.enabled = false;

            if (pauseMotionDuringIntro)
            {
                var myRBs = tf.GetComponentsInChildren<Rigidbody2D>(true).ToList();
                rbs[tf] = myRBs;
                foreach (var rb in myRBs) if (rb) rb.simulated = false;

                var toPause = new List<Behaviour>();
                if (behaviourNameContains != null && behaviourNameContains.Length > 0)
                {
                    var allBehaviours = tf.GetComponentsInChildren<Behaviour>(true);
                    foreach (var b in allBehaviours)
                    {
                        if (!b) continue;
                        string n = b.GetType().Name;
                        if (behaviourNameContains.Any(key => n.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            if (b.enabled) { b.enabled = false; toPause.Add(b); }
                        }
                    }
                }
                pausedBehaviours[tf] = toPause;
            }
        }

        float maxDelay = Mathf.Max(0f, maxStagger);
        int index = 0;

        foreach (var peg in list)
        {
            if (!peg) continue;
            var tf = peg.transform;
            if (!tf) continue;

            float delay = maxDelay > 0f ? Random.Range(0f, maxDelay) : 0f;

            tf.DOScale(originals[tf], popDuration)
              .SetEase(Ease.OutBack, overshoot)
              .SetDelay(delay)
              .OnStart(() =>
              {
                  if (index % Mathf.Max(1, sfxEveryN) == 0 && AudioManager.Instance && !string.IsNullOrEmpty(sfxPlopKey))
                  {
                      if (sfxAs3D) AudioManager.Instance.Play(sfxPlopKey, tf.position);
                      else AudioManager.Instance.PlayUI(sfxPlopKey);
                  }
                  index++;
              });

            if (fadeSprites && spriteGroups.TryGetValue(tf, out var srs))
                foreach (var sr in srs) if (sr) sr.DOFade(1f, fadeDuration).SetDelay(delay * 0.9f);
        }

        float wait = maxDelay + Mathf.Max(popDuration, fadeDuration) + 0.06f;
        yield return new WaitForSeconds(wait);

        foreach (var peg in list)
        {
            if (!peg) continue;
            var tf = peg.transform;
            if (!tf) continue;

            if (disableCollidersDuringIntro)
                foreach (var col in tf.GetComponentsInChildren<Collider2D>(true))
                    if (col) col.enabled = true;

            if (pauseMotionDuringIntro)
            {
                if (rbs.TryGetValue(tf, out var myRBs))
                    foreach (var rb in myRBs) if (rb) rb.simulated = true;

                if (pausedBehaviours.TryGetValue(tf, out var behs))
                    foreach (var b in behs) if (b) b.enabled = true;
            }
        }

        if (lockLauncherWhilePlaying) Launcher.Instance?.UnlockInput();
        _playedThisScene = true;
        IsIntroPlaying = false;
    }
}
