using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public static AudioManager I { get; private set; }
    public static AudioManager Instance => I;

    [System.Serializable]
    public class SoundDef
    {
        public string key;
        public AudioClip clip;
        [Range(0f, 2f)] public float volume = 1f;
        [Range(.1f, 3f)] public float pitch = 1f;
        public bool randomizePitch = false;
        [Range(0f, 1f)] public float pitchRange = .1f;
    }

    [Header("SFX Library (one-shots)")]
    [SerializeField] List<SoundDef> sfxSounds = new();

    [Header("Music Library (BGM)")]
    [SerializeField] List<SoundDef> musicTracks = new();

    readonly Dictionary<string, SoundDef> sfxDict = new();
    readonly Dictionary<string, SoundDef> bgmDict = new();

    [Header("SFX Output & Pool")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField, Min(1)] int poolSize = 16;
    [SerializeField] float sfxSpatialBlend3D = 1f;
    [SerializeField] float sfxMinDistance = 1f;
    [SerializeField] float sfxMaxDistance = 30f;

    AudioSource[] pool;
    int head;

    [Header("Music (BGM) Output")]
    [SerializeField] AudioMixerGroup musicGroup;
    [SerializeField, Range(0f, 1f)] float musicVolume = 1f;   // master BGM 0..1

    [Header("Master Volume")]
    [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;     // master SFX 0..1

    // Main = gameplay BGM, Overlay = inventory/tutorial BGM
    AudioSource mainMusic;
    AudioSource overlayMusic;

    public bool IsMusicPlaying => mainMusic && mainMusic.isPlaying;
    public bool IsMusicPaused => mainMusic && !mainMusic.isPlaying && mainMusic.clip && mainMusic.time > 0f;

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        BuildLibrary(sfxSounds, sfxDict);
        BuildLibrary(musicTracks, bgmDict);

        // SFX pool
        pool = new AudioSource[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.outputAudioMixerGroup = sfxGroup;
            src.spatialBlend = sfxSpatialBlend3D;
            src.minDistance = sfxMinDistance;
            src.maxDistance = sfxMaxDistance;
            pool[i] = src;
        }

        // Main gameplay music
        mainMusic = gameObject.AddComponent<AudioSource>();
        mainMusic.playOnAwake = false;
        mainMusic.loop = true;
        mainMusic.spatialBlend = 0f;
        mainMusic.outputAudioMixerGroup = musicGroup ? musicGroup : sfxGroup;
        mainMusic.volume = musicVolume;

        // Overlay music (untuk inventory / tutorial)
        overlayMusic = gameObject.AddComponent<AudioSource>();
        overlayMusic.playOnAwake = false;
        overlayMusic.loop = true;
        overlayMusic.spatialBlend = 0f;
        overlayMusic.outputAudioMixerGroup = musicGroup ? musicGroup : sfxGroup;
        overlayMusic.volume = musicVolume;
    }

    void BuildLibrary(List<SoundDef> list, Dictionary<string, SoundDef> dict)
    {
        dict.Clear();
        foreach (var s in list)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.key) || s.clip == null) continue;
            if (!dict.ContainsKey(s.key)) dict.Add(s.key, s);
            else Debug.LogWarning($"[AudioManager] duplicate key '{s.key}' skipped.", this);
        }
    }

    /*──────────────── helpers ────────────────*/
    float ResolvePerTrackVolume(AudioSource src)
    {
        if (src && src.clip)
        {
            foreach (var kv in bgmDict)
                if (kv.Value.clip == src.clip)
                    return Mathf.Clamp01(musicVolume) * (kv.Value.volume <= 0 ? 1f : kv.Value.volume);
        }
        return Mathf.Clamp01(musicVolume);
    }

    /*──────────────── SFX ────────────────*/
    public void Play(string key, Vector3 position, float volumeScale = 1f, bool ignorePause = false)
    {
        if (!sfxDict.TryGetValue(key, out var def) || def.clip == null) return;
        var src = GetFreeSfxSource(isUI: false);
        src.transform.position = position;
        src.outputAudioMixerGroup = sfxGroup;
        src.spatialBlend = sfxSpatialBlend3D;
        src.ignoreListenerPause = ignorePause;                    // <-- penting utk SFX saat pause
        src.clip = def.clip;
        src.volume = def.volume * sfxVolume * Mathf.Clamp01(volumeScale);
        src.pitch = def.randomizePitch ? Random.Range(def.pitch - def.pitchRange, def.pitch + def.pitchRange) : def.pitch;
        src.Play();
    }

    public void PlayUI(string key, float volumeScale = 1f, bool ignorePause = false)
    {
        if (!sfxDict.TryGetValue(key, out var def) || def.clip == null) return;
        var src = GetFreeSfxSource(isUI: true);
        src.transform.position = Vector3.zero;
        src.outputAudioMixerGroup = sfxGroup;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = ignorePause;                    // <-- penting utk UI saat pause
        src.clip = def.clip;
        src.volume = def.volume * sfxVolume * Mathf.Clamp01(volumeScale);
        src.pitch = def.randomizePitch ? Random.Range(def.pitch - def.pitchRange, def.pitch + def.pitchRange) : def.pitch;
        src.Play();
    }
    public void PlayUI(string key, bool ignorePause) => PlayUI(key, 1f, ignorePause);

    /*──────────────── MAIN (gameplay) MUSIC — no fade ────────────────*/
    public void PlayMusic(string key, bool loop = true)
    {
        if (!bgmDict.TryGetValue(key, out var def) || def.clip == null) return;
        mainMusic.Stop();
        mainMusic.clip = def.clip;
        mainMusic.loop = loop;
        mainMusic.pitch = 1f;
        mainMusic.volume = Mathf.Clamp01(musicVolume) * (def.volume <= 0 ? 1f : def.volume);
        mainMusic.Play();
    }
    public void StopMusic()
    {
        if (!mainMusic) return;
        mainMusic.Stop();
    }
    public void PauseMusic()
    {
        if (mainMusic && mainMusic.isPlaying) mainMusic.Pause();
        if (overlayMusic && overlayMusic.isPlaying) overlayMusic.Pause();
        // JANGAN pakai AudioListener.pause di sini
    }
    public void ResumeMusic()
    {
        if (overlayMusic && overlayMusic.clip) overlayMusic.UnPause();
        if (mainMusic && mainMusic.clip) mainMusic.UnPause();
    }

    /*── Volume Master (dipanggil SettingsPanel) ──*/
    public void SetSfxVolume01(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        // pool SFX akan pakai nilai ini saat Play()
    }

    public void SetMusicVolume01(float v)
    {
        musicVolume = Mathf.Clamp01(v);                 // simpan master
        if (mainMusic) mainMusic.volume = ResolvePerTrackVolume(mainMusic);
        if (overlayMusic) overlayMusic.volume = ResolvePerTrackVolume(overlayMusic);
    }

    /*──────────────── OVERLAY MUSIC — no fade ────────────────*/
    public void PlayOverlayMusic(string key, bool loop = true)
    {
        if (!bgmDict.TryGetValue(key, out var def) || def.clip == null) return;
        overlayMusic.Stop();
        overlayMusic.clip = def.clip;
        overlayMusic.loop = loop;
        overlayMusic.pitch = 1f;
        overlayMusic.volume = Mathf.Clamp01(musicVolume) * (def.volume <= 0 ? 1f : def.volume); // pakai per-track
        overlayMusic.Play();
    }
    public void StopOverlayMusic()
    {
        if (!overlayMusic) return;
        overlayMusic.Stop();
    }

    public bool IsCurrentMusic(string key)
    {
        return bgmDict.TryGetValue(key, out var def)
               && mainMusic != null
               && mainMusic.clip == def.clip
               && mainMusic.isPlaying;
    }

    public void PlayMusicIfChanged(string key, bool loop = true)
    {
        if (!bgmDict.TryGetValue(key, out var def) || def.clip == null) return;

        // jika sudah lagu yang sama dan lagi main, jangan restart
        if (mainMusic && mainMusic.isPlaying && mainMusic.clip == def.clip) return;

        if (!mainMusic) mainMusic = gameObject.AddComponent<AudioSource>();
        mainMusic.Stop();
        mainMusic.clip = def.clip;
        mainMusic.loop = loop;
        mainMusic.pitch = 1f;
        mainMusic.volume = Mathf.Clamp01(musicVolume) * (def.volume <= 0 ? 1f : def.volume);
        mainMusic.Play();
    }
    // Di dalam AudioManager.cs
    AudioSource GetFreeSfxSource(bool isUI)
    {
        // 1) Cari yang tidak sedang bermain (prioritas)
        for (int i = 0; i < pool.Length; i++)
        {
            int idx = (head + i) % pool.Length;
            if (!pool[idx].isPlaying)
            {
                head = (idx + 1) % pool.Length;
                return pool[idx];
            }
        }

        // 2) Semua penuh → pilih yang sisa waktunya paling sedikit (steal yang hampir selesai)
        int pick = 0;
        float leastRemaining = float.MaxValue;
        for (int i = 0; i < pool.Length; i++)
        {
            var src = pool[i];
            float remaining = (src && src.clip) ? Mathf.Max(0f, src.clip.length - src.time) : 0f;
            if (remaining < leastRemaining)
            {
                leastRemaining = remaining;
                pick = i;
            }
        }
        head = (pick + 1) % pool.Length;
        return pool[pick];
    }

}
