using UnityEngine;

public class UIFloatBob : MonoBehaviour
{
    [SerializeField] float amplitude = 24f;   // px
    [SerializeField] float period = 1.8f;     // detik (naik-turun lengkap)
    [SerializeField] bool useUnscaledTime = true;

    RectTransform rt;
    Vector2 basePos;
    float t;

    void Awake() { rt = (RectTransform)transform; basePos = rt.anchoredPosition; }
    void OnEnable() { t = 0f; }

    void Update()
    {
        t += (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        float y = Mathf.Sin(t * (Mathf.PI * 2f / period)) * amplitude;
        rt.anchoredPosition = basePos + new Vector2(0f, y);
    }
}
