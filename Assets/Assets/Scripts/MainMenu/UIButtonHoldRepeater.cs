using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[RequireComponent(typeof(UnityEngine.UI.Button))]
public class UIButtonHoldRepeater : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("Event yang dipanggil tiap 'ulang'. Kaitkan ke NameEntryUI.UI_Up atau UI_Down.")]
    public UnityEvent onRepeat;

    [Tooltip("Jeda awal sebelum mulai repeating (detik).")]
    public float initialDelay = 0.35f;

    [Tooltip("Interval antar repeat (detik).")]
    public float repeatInterval = 0.06f;

    bool held;
    float timer;
    bool started;

    void Update()
    {
        if (!held) return;

        if (!started)
        {
            timer += Time.unscaledDeltaTime;
            if (timer >= initialDelay)
            {
                started = true;
                timer = 0f;
                onRepeat?.Invoke();
            }
        }
        else
        {
            timer += Time.unscaledDeltaTime;
            if (timer >= repeatInterval)
            {
                timer = 0f;
                onRepeat?.Invoke();
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        held = true;
        started = false;
        timer = 0f;
        onRepeat?.Invoke(); // sekali langsung eksekusi juga
    }

    public void OnPointerUp(PointerEventData eventData) { held = false; }
    public void OnPointerExit(PointerEventData eventData) { held = false; }
}
