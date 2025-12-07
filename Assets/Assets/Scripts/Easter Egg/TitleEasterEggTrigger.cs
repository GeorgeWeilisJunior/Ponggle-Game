using UnityEngine;
using UnityEngine.EventSystems;

public class TitleEasterEggTrigger : MonoBehaviour, IPointerClickHandler
{
    [Header("Easter Egg Settings")]
    [SerializeField] private int clicksToTrigger = 5;
    [SerializeField] private float maxIntervalBetweenClicks = 1.5f;
    [SerializeField] private string easterEggSceneName = "Easter Egg";

    private int clickCount = 0;
    private float lastClickTime = 0f;

    public void OnPointerClick(PointerEventData eventData)
    {
        float now = Time.unscaledTime;

        // kalau jeda klik terlalu lama, reset hitungan
        if (now - lastClickTime > maxIntervalBetweenClicks)
            clickCount = 0;

        lastClickTime = now;
        clickCount++;

        if (clickCount >= clicksToTrigger)
        {
            clickCount = 0;
            GoToEasterEgg();
        }
    }

    private void GoToEasterEgg()
    {
        // matikan musik menu dulu biar nggak tumpuk
        AudioManager.I?.StopMusic();

        // Pakai SceneTransition biar konsisten dengan project-mu
        SceneTransition.LoadScene(easterEggSceneName);

        // Jangan sentuh SaveManager di sini — biarkan progress tetap.
    }
}
