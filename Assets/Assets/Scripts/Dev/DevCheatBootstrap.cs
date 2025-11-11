using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bootstrap super-kecil untuk build: mendengar "secret unlock"
/// lalu menyalakan DEV_MODE dan memunculkan DevCheatMenu.
/// </summary>
public class DevCheatBootstrap : MonoBehaviour
{
    [Header("Unlock via typed code")]
    [Tooltip("Ketik kata ini (tanpa spasi, case-insensitive) di mana saja untuk unlock.")]
    public string secretCode = "ponggle";

    [Header("Unlock via long-press")]
    public KeyCode holdKey = KeyCode.F12;
    public float holdSeconds = 2f;

    string _typed = "";
    float _holdUntil = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        // Pastikan satu bootstrap selalu ada di build
        var go = new GameObject("__DevBoot");
        DontDestroyOnLoad(go);
        go.AddComponent<DevCheatBootstrap>();
    }

    void Update()
    {
        // 1) Unlock via ketik kode
        foreach (char ch in Input.inputString)
        {
            if (char.IsControl(ch)) continue;
            _typed += char.ToLowerInvariant(ch);
            if (_typed.Length > secretCode.Length)
                _typed = _typed.Substring(_typed.Length - secretCode.Length);

            if (_typed.EndsWith(secretCode.ToLowerInvariant()))
            {
                EnableDevMode("typed-code");
            }
        }

        // 2) Unlock via tahan tombol (default F12)
        if (Input.GetKey(holdKey))
        {
            if (_holdUntil < 0f) _holdUntil = Time.unscaledTime + holdSeconds;
            if (Time.unscaledTime >= _holdUntil)
            {
                EnableDevMode("long-press");
            }
        }
        else _holdUntil = -1f;

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.D))
        {
            PlayerPrefs.SetInt("DEV_MODE", 1);
            PlayerPrefs.Save();
            SpawnMenuIfMissing();
        }
    }

    void EnableDevMode(string how)
    {
        if (PlayerPrefs.GetInt("DEV_MODE", 0) == 1) { SpawnMenuIfMissing(); return; }

        PlayerPrefs.SetInt("DEV_MODE", 1);
        PlayerPrefs.Save();
        Debug.Log($"[DEV] DEV_MODE enabled via {how}.");

        SpawnMenuIfMissing();

        // opsional: bunyikan musik/sfx kecil biar ada feedback
        try { AudioManager.I?.PlayUI("MainMenuClick"); } catch { }

        // bootstrap tidak diperlukan lagi
        Destroy(gameObject);
    }

    void SpawnMenuIfMissing()
    {
        var menu = FindObjectOfType<DevCheatMenu>(true);
        if (!menu)
        {
            var goNew = new GameObject("__DevMenu");
            DontDestroyOnLoad(goNew);
            menu = goNew.AddComponent<DevCheatMenu>();
        }

        // Pastikan instance ini aktif untuk build
        menu.restrictToDevBuild = false;           // jangan batasi ke Development build
        menu.enabled = true;                       // paksa hidup
        DontDestroyOnLoad(menu.gameObject);        // jaga tetap hidup lintas scene

        Debug.Log("[DEV] DevCheatMenu forced enabled in build.");
    }


}
