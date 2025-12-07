using UnityEngine;

public class EasterEggController : MonoBehaviour
{
    [SerializeField] private string musicKey = "Music_EasterEgg";

    private void Start()
    {
        // Stop overlay kalau ada, lalu mainkan musik easter egg
        AudioManager.I?.StopOverlayMusic();
        if (!string.IsNullOrEmpty(musicKey))
        {
            AudioManager.I?.PlayMusic(musicKey);
        }
    }

    // Opsional: dipanggil dari tombol "Back"
    public void BackToMainMenu()
    {
        AudioManager.I?.StopMusic();
        SceneTransition.LoadScene("Main Menu");
    }
}
