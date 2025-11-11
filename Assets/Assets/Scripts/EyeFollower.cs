using UnityEngine;

public class EyeFollower : MonoBehaviour
{
    [Header("Refs")]
    public Transform target;                // isi: Ball transform (assign runtime)
    public Transform pupil;                 // child "Pupil"
    public Transform white;                 // child "White" (opsional, buat blink)

    [Header("Look")]
    public float pupilMaxOffset = 0.08f;    // jarak max dari pusat (world units)
    public float followLerp = 12f;          // kejar halus

    [Header("Appear")]
    public bool startHidden = true;
    public float fadeSpeed = 10f;

    [Header("Blink")]
    public bool enableBlink = true;
    public Vector2 blinkInterval = new Vector2(3f, 7f);
    public float blinkDuration = 0.08f;     // tutup setengah & buka lagi

    float _alpha = 0f;
    float _blinkT = 0f;
    float _nextBlink = 0f;
    Vector3 _pupilHome;

    SpriteRenderer _pSR, _wSR;

    void Awake()
    {
        if (!pupil) pupil = transform.Find("Pupil");
        if (!white) white = transform.Find("White");
        _pSR = pupil ? pupil.GetComponent<SpriteRenderer>() : null;
        _wSR = white ? white.GetComponent<SpriteRenderer>() : null;
        _pupilHome = pupil ? pupil.localPosition : Vector3.zero;
        if (!startHidden) _alpha = 1f;
        ScheduleBlink();
        ApplyAlpha();
    }

    void ScheduleBlink() => _nextBlink = Time.time + Random.Range(blinkInterval.x, blinkInterval.y);

    void Update()
    {
        // follow
        if (target && pupil)
        {
            // baru — hitung arah di LOCAL SPACE agar rotasi/parent tidak bikin nyasar
            Vector2 dirW = (Vector2)(target.position - transform.position);
            Vector2 dirL = (dirW.sqrMagnitude > 0.0001f)
                ? (Vector2)transform.InverseTransformDirection(dirW).normalized
                : Vector2.zero;

            Vector3 goal = _pupilHome + (Vector3)(dirL * pupilMaxOffset);
            pupil.localPosition = Vector3.Lerp(
                pupil.localPosition, goal,
                1f - Mathf.Exp(-followLerp * Time.unscaledDeltaTime));

        }

        // blink
        if (enableBlink && white)
        {
            if (Time.time >= _nextBlink) _blinkT = blinkDuration * 2f; // tutup lalu buka
            if (_blinkT > 0f)
            {
                _blinkT -= Time.unscaledDeltaTime;
                float half = blinkDuration;
                float k = _blinkT > half ? 1f - (_blinkT - half) / half : (_blinkT / half);
                float sclY = Mathf.Lerp(1f, 0.15f, k);
                white.localScale = new Vector3(white.localScale.x, sclY, 1f);
                if (_blinkT <= 0f) { white.localScale = new Vector3(white.localScale.x, 1f, 1f); ScheduleBlink(); }
            }
        }

        // fade (muncul/hilang dikontrol eksternal via SetVisible)
        ApplyAlpha();
    }

    void ApplyAlpha()
    {
        if (_wSR) _wSR.color = new Color(1, 1, 1, _alpha);
        if (_pSR) _pSR.color = new Color(1, 1, 1, _alpha);
    }

    public void SetVisible(bool v)
    {
        float targetA = v ? 1f : 0f;
        _alpha = Mathf.MoveTowards(_alpha, targetA, fadeSpeed * Time.unscaledDeltaTime);
    }
}
