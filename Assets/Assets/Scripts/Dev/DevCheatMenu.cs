using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;             // <-- for Canvas/Image blocker
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DevCheatMenu : MonoBehaviour
{
    [Header("General")]
    public bool restrictToDevBuild = true;
    public KeyCode toggleKey = KeyCode.F1;
    public KeyCode quickKey = KeyCode.BackQuote;

    [Header("Shortcuts")]
    public KeyCode winKey = KeyCode.F7;
    public KeyCode loseKey = KeyCode.F8;
    public KeyCode nextLevelKey = KeyCode.F9;
    public KeyCode prevLevelKey = KeyCode.F6;

    [Header("Card IDs (opsional)")]
    public string[] knownCardIds =
    {
        "DoubleBucket","FreePower","TinySplit","ElementRecharge",
        "FlameInfusion","WaterInfusion","WindInfusion","EarthInfusion",
        "BetterKillZone","MinusOrange","FinalGambit","ElementaryMastery","Overdrive"
    };

    [Header("UI")]
    [Range(0.5f, 2f)] public float uiScale = 1f;
    Rect quickRect = new Rect(8, 8, 360, 92);
    Rect mainRect = new Rect(12, 110, 520, 560);

    bool _showFull, _showQuick;
    Vector2 _scroll;
    string _gotoLevelText = "";
    int _stageForStart = 0;

    // overlay blocker (uGUI) agar klik tidak nembus
    GameObject blockerGO;
    Image blockerImage;

    // --- runtime confirm state (untuk build) ---
    bool _confirmResetArmed = false;
    float _confirmDeadline = 0f;

    LevelManager LM => LevelManager.Instance;
    SaveManager SM => SaveManager.I;
    CardInventory INV => CardInventory.I;
    GameManager GM => FindObjectOfType<GameManager>(true);

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        if (restrictToDevBuild && !IsAllowed()) enabled = false;
    }

    bool IsAllowed()
    {
#if UNITY_EDITOR
        return true;
#else
        return Debug.isDebugBuild || PlayerPrefs.GetInt("DEV_MODE", 0) == 1;
#endif
    }

    void Update()
    {
        if (!enabled) return;

        if (Input.GetKeyDown(toggleKey)) _showFull = !_showFull;
        if (Input.GetKeyDown(quickKey)) _showQuick = !_showQuick;
        if (Input.GetKeyDown(KeyCode.F2)) KeepOnlyOneOrangePeg();       // contoh hotkey
        if (Input.GetKeyDown(KeyCode.F3)) ActivateCurrentCharacterPower();


        if (Input.GetKeyDown(winKey)) ForceWin();
        if (Input.GetKeyDown(loseKey)) ForceLose();
        if (Input.GetKeyDown(nextLevelKey)) GoNext();
        if (Input.GetKeyDown(prevLevelKey)) GoPrev();

        // kunci input launcher & aktifkan blocker uGUI saat panel tampil
        ApplyInputLock();
        SetBlockerActive(_showFull || _showQuick);
    }

    /* ---------------- Level actions ---------------- */

    void GoNext()
    {
        if (!LM) return;
        LM.LoadNext();
        RequestFixStageMusicOnNextScene();
    }

    void GoPrev()
    {
        if (!LM) return;
        LM.LoadLevelIndex(Mathf.Max(0, LM.CurrentIndex - 1));
        RequestFixStageMusicOnNextScene();
    }

    void Reload()
    {
        if (!LM) return;
        LM.Restart();
        RequestFixStageMusicOnNextScene();
    }

    void ForceWin()
    {
        // Tandai mode dev (opsional – kalau CardDropPanel kamu membacanya)
        PlayerPrefs.SetInt("DEV_FORCE_CARD_DROP", 1);
        PlayerPrefs.Save();

        // Pastikan EndLevelPanel aktif supaya tahap Summary muncul lebih dulu
        var panel = FindObjectOfType<EndLevelPanel>(true);
        if (panel)
        {
            panel.gameObject.SetActive(true); // aktifkan kalau sempat nonaktif
        }

        // Trigger win via GameManager (ini akan memanggil EndLevelPanel.Show(...) lewat event)
        if (GM) GM.SendMessage("EndLevelSuccess", SendMessageOptions.DontRequireReceiver);
        else GoNext(); // fallback kalau GM tidak ada
    }


    void ForceLose()
    {
        if (GM) GM.SendMessage("TriggerLose", SendMessageOptions.DontRequireReceiver);
        else Reload();
    }

    void ApplyInputLock()
    {
        var L = Launcher.Instance;
        if (!L) return;
        if (_showFull || _showQuick) L.LockInput();
        else L.UnlockInput();
    }

    void GotoLevelIndex(int absoluteIndex)
    {
        if (LM == null) return;

        // 1) Load scene level target
        LM.LoadLevelIndex(Mathf.Max(0, absoluteIndex));
        RequestFixStageMusicOnNextScene();
        var key = LM ? LM.GetCurrentStageBgmKey() : null;
        if (!string.IsNullOrEmpty(key)) AudioManager.I?.PlayMusicIfChanged(key, true);

        // 2) Sinkron save agar flow CardManagement/Next membaca level yang benar
        if (SM != null)
        {
            SM.Data.levelIndex = absoluteIndex;                                      // current
            SM.Data.stageIndex = Mathf.Max(0, LM.GetStageFromLevelIndex(absoluteIndex) - 1);
            SM.RequestedContinue = false;                                            // jangan override lagi
            SM.SaveToDisk();
        }
    }

    // --- helper: tebak apakah sebuah peg adalah ORANGE ---
    // --- helper: cek ORANGE berdasarkan PegController.Type ---
    bool IsOrangePeg(object peg)
    {
        if (peg == null) return false;

        // Langsung cek kalau memang PegController
        var pc = peg as PegController;
        if (pc != null)
            return pc.State != PegController.PegState.Cleared && pc.Type == PegType.Orange;

        // Fallback refleksi: cari property "Type" yang bertipe PegType dan == Orange
        var t = peg.GetType();
        var pType = t.GetProperty("Type", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (pType != null && pType.PropertyType.IsEnum && pType.PropertyType.Name == nameof(PegType))
        {
            var val = pType.GetValue(peg);
            if (val != null && val.ToString() == nameof(PegType.Orange)) return true;
        }

        // Fallback terakhir (aman tapi tidak diandalkan)
        var goProp = t.GetProperty("gameObject");
        if (goProp != null)
        {
            var go = goProp.GetValue(peg) as GameObject;
            if (go)
            {
                if (go.CompareTag("OrangePeg")) return true;
                if (!string.IsNullOrEmpty(go.name) && go.name.ToLower().Contains("orange")) return true;
            }
        }
        return false;
    }


    // --- helper: clear peg instan (pakai pesan/metode yang ada; fallback: disable GO) ---
    void TryClearPegImmediate(object peg)
    {
        if (peg == null) return;
        var t = peg.GetType();

        // metode yang mungkin ada (urutkan dari paling “benar”)
        string[] methods =
        {
        "ForceClearImmediate","ClearImmediate","DevForceClear","ForceClear",
        "ClearNow","ClearInstant","OnDevClear"
    };
        foreach (var m in methods)
        {
            var mi = t.GetMethod(m, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (mi != null && mi.GetParameters().Length == 0)
            {
                mi.Invoke(peg, null);
                return;
            }
        }

        // fallback: SendMessage
        try { (peg as Component)?.SendMessage("ForceClearImmediate", SendMessageOptions.DontRequireReceiver); } catch { }
        try { (peg as Component)?.SendMessage("ClearImmediate", SendMessageOptions.DontRequireReceiver); } catch { }
        try { (peg as Component)?.SendMessage("ClearNow", SendMessageOptions.DontRequireReceiver); } catch { }

        // fallback terakhir: nonaktifkan GameObject
        var goProp = t.GetProperty("gameObject");
        if (goProp != null)
        {
            var go = goProp.GetValue(peg) as GameObject;
            if (go) go.SetActive(false);
        }
    }

    void KeepOnlyOneOrangePeg()
    {
        // kumpulkan semua PegController (atau kelas lain yang memuat peg)
        var all = GameObject.FindObjectsOfType<Component>(true);
        var alivePegs = new List<object>();
        foreach (var c in all)
            if (c && c.GetType().Name.Contains("PegController") && IsPegCurrentlyInPlay(c))
                alivePegs.Add(c);

        if (alivePegs.Count == 0)
        {
            Debug.LogWarning("[DEV] Tidak ada peg yang masih hidup di papan.");
            return;
        }

        // cari 1 orange yang MASIH HIDUP untuk disisakan
        object keep = null;
        foreach (var p in alivePegs) if (IsOrangePeg(p)) { keep = p; break; }

        // kalau belum ada orange hidup, pilih satu yang hidup lalu jadikan orange
        if (keep == null) keep = alivePegs[0];
        if (!IsOrangePeg(keep))
        {
            var t = keep.GetType();
            var f1 = t.GetField("isOrange", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f1 != null && f1.FieldType == typeof(bool)) f1.SetValue(keep, true);
            var p1 = t.GetProperty("IsOrange", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p1 != null && p1.PropertyType == typeof(bool) && p1.CanWrite) p1.SetValue(keep, true);

            var go = (t.GetProperty("gameObject")?.GetValue(keep)) as GameObject;
            if (go) go.name = go.name + "_ORANGE_DEV";
        }

        // clear semua peg HIDUP lainnya, biarkan yang sudah hancur tetap diabaikan
        int cleared = 0;
        foreach (var p in alivePegs)
        {
            if (ReferenceEquals(p, keep)) continue;
            TryClearPegImmediate(p);
            cleared++;
        }

        // ⛔️ JANGAN isi Fever di sini — biarkan last-orange dipicu oleh gameplay
        // (hapus / jangan panggil AddOrangeHitInstant)

        Debug.Log($"[DEV] KeepOnlyOneOrangePeg → clearedAlive={cleared}, keep={(keep as Component)?.gameObject?.name}");
        AudioManager.I?.PlayUI("MainMenuClick");
    }


    void ActivateCurrentCharacterPower()
    {
        // --- 1) Kalau CharacterPowerManager ada, gunakan langsung ---
        var cpmType = System.Type.GetType("CharacterPowerManager");
        // aman: coba cari via scene juga
        Component cpm = null;
        foreach (var c in GameObject.FindObjectsOfType<Component>(true))
            if (c && c.GetType().Name == "CharacterPowerManager") { cpm = c; break; }

        if (cpm != null)
        {
            // Paksa kondisi siap: cooldown = 0, ready = true, meter penuh jika ada
            var t = cpm.GetType();
            var fReady = t.GetField("ready", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (fReady != null && fReady.FieldType == typeof(bool)) fReady.SetValue(cpm, true);

            var fCd = t.GetField("cooldown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (fCd != null && (fCd.FieldType == typeof(float) || fCd.FieldType == typeof(int))) fCd.SetValue(cpm, 0);

            var fCharge = t.GetField("charge", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var fChargeMax = t.GetField("chargeMax", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (fCharge != null && fChargeMax != null && fCharge.FieldType == typeof(int) && fChargeMax.FieldType == typeof(int))
                fCharge.SetValue(cpm, (int)fChargeMax.GetValue(cpm));

            // Panggil TryActivatePower() (tanpa parameter)
            var mi = t.GetMethod("TryActivatePower", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (mi != null && mi.GetParameters().Length == 0)
            {
                mi.Invoke(cpm, null);
                Debug.Log("[DEV] Power activated via CharacterPowerManager.TryActivatePower()");
                AudioManager.I?.PlayUI("MainMenuClick");
                return;
            }
        }

        // --- 2) Fallback (scan semua komponen terkait power/ability/character) ---
        var targets = new List<Component>();
        foreach (var c in GameObject.FindObjectsOfType<Component>(true))
        {
            if (!c) continue;
            var n = c.GetType().Name;
            if (n.Contains("Power") || n.Contains("Ability") || n.Contains("Character"))
                targets.Add(c);
        }
        if (GameManager.Instance) targets.Add(GameManager.Instance);

        string[] methods = { "TryActivatePower", "ActivatePower", "ActivateCurrentPower", "UsePower", "TriggerPower", "ActivateCharacterPower" };

        bool ForceReady(object obj)
        {
            var t = obj.GetType();
            var ok = false;
            var fReady = t.GetField("ready", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (fReady != null && fReady.FieldType == typeof(bool)) { fReady.SetValue(obj, true); ok = true; }
            var pReady = t.GetProperty("Ready"); if (pReady != null && pReady.PropertyType == typeof(bool) && pReady.CanWrite) { pReady.SetValue(obj, true); ok = true; }

            var fCd = t.GetField("cooldown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (fCd != null && (fCd.FieldType == typeof(float) || fCd.FieldType == typeof(int))) { fCd.SetValue(obj, 0); ok = true; }

            var fCharge = t.GetField("charge", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var fChargeMax = t.GetField("chargeMax", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (fCharge != null && fChargeMax != null && fCharge.FieldType == typeof(int) && fChargeMax.FieldType == typeof(int))
            { fCharge.SetValue(obj, (int)fChargeMax.GetValue(obj)); ok = true; }
            return ok;
        }

        foreach (var tgt in targets)
        {
            if (!tgt) continue;
            var t = tgt.GetType();
            foreach (var m in methods)
            {
                var mi = t.GetMethod(m, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (mi != null && mi.GetParameters().Length == 0)
                {
                    ForceReady(tgt);
                    mi.Invoke(tgt, null);
                    Debug.Log($"[DEV] Power activated via {t.Name}.{m}()");
                    AudioManager.I?.PlayUI("MainMenuClick");
                    return;
                }
            }
        }

        // --- 3) Fallback terakhir ---
        try { BroadcastMessage("OnPowerPressed", SendMessageOptions.DontRequireReceiver); Debug.Log("[DEV] Broadcast OnPowerPressed"); }
        catch { Debug.LogWarning("[DEV] Tidak menemukan target power yang cocok."); }
    }

    // ===== Fast Forward Dev Toggle =====
    void SetFastForwardUnlocked(bool on)
    {
        // 1) persist ke save
        if (SM != null)
        {
            SM.Data.fastForwardUnlocked = on;
            SM.SaveToDisk();
        }

        // 2) coba refresh komponen-komponen yang mungkin mengendalikan FF
        //    a) broadcast event generik agar listener lokal bisa ikut
        try { BroadcastMessage("OnFastForwardUnlockChanged", on, SendMessageOptions.DontRequireReceiver); } catch { }

        //    b) cari komponen bernuansa FastForward/Speed/TimeScale dan panggil method umum
        var comps = GameObject.FindObjectsOfType<Component>(true);
        string[] methodsBool = { "SetFastForwardUnlocked", "SetUnlocked", "EnableFastForward", "SetFastForwardEnabled" };
        foreach (var c in comps)
        {
            if (!c) continue;
            var n = c.GetType().Name;
            if (!(n.Contains("FastForward") || n.Contains("Speed") || n.Contains("TimeScale"))) continue;

            var t = c.GetType();
            foreach (var m in methodsBool)
            {
                var mi = t.GetMethod(m, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (mi != null)
                {
                    var ps = mi.GetParameters();
                    if (ps.Length == 1 && (ps[0].ParameterType == typeof(bool)))
                    {
                        mi.Invoke(c, new object[] { on });
#if UNITY_EDITOR
                        Debug.Log($"[DEV] {t.Name}.{m}({on})");
#endif
                        break;
                    }
                }
            }
        }

        //    c) fallback: aktifkan tombol yang namanya mengandung "FastForward" / "FF"
        foreach (var btn in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>())
        {
            if (!btn) continue;
            var name = btn.gameObject.name;
            if (name.IndexOf("FastForward", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name == "FF" || name.Contains("FFwd"))
            {
                btn.gameObject.SetActive(on);
                btn.interactable = on;
            }
        }

        Debug.Log("[DEV] FastForwardUnlocked = " + on);
        AudioManager.I?.PlayUI("MainMenuClick");
    }


    // Peg dianggap "in play" kalau GO aktif & tidak punya flag cleared/consumed/destroyed.
    bool IsPegCurrentlyInPlay(object peg)
    {
        if (peg == null) return false;

        // aktif di hierarchy?
        var comp = peg as Component;
        if (!comp || !comp.gameObject.activeInHierarchy) return false;

        // kalau dia Behaviour, pastikan enabled
        var bh = peg as Behaviour;
        if (bh && !bh.isActiveAndEnabled) return false;

        // cek beberapa flag umum via refleksi
        var t = peg.GetType();
        string[] clearedNames = { "isCleared", "cleared", "IsCleared", "consumed", "isConsumed", "IsConsumed", "gone", "isGone", "IsGone", "destroyed", "isDestroyed", "IsDestroyed" };
        foreach (var name in clearedNames)
        {
            var f = t.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(peg)) return false;

            var p = t.GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(bool) && (bool)p.GetValue(peg)) return false;
        }
        return true;
    }


    void ResetSaveDataInMemoryToDefaults()
    {
        if (SaveManager.I == null) return;
        // Penting: false → JANGAN bikin file save baru
        SaveManager.I.ResetInMemoryToDefaults(false);

        var sel = FindObjectOfType<CharacterSelectController>(true);
        sel?.SendMessage("RefreshUnlockFlags", SendMessageOptions.DontRequireReceiver);
        sel?.SendMessage("ApplySelection", true, SendMessageOptions.DontRequireReceiver);
    }

    void GotoLevelParsed()
    {
        if (LM == null) return;
        var txt = (_gotoLevelText ?? "").Trim();
        if (string.IsNullOrEmpty(txt)) return;

        int idx = -1;
        if (txt.Contains("-"))
        {
            var parts = txt.Split('-');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var st1) &&
                int.TryParse(parts[1], out var lv1))
            {
                int start = LM.GetStageStartIndex(st1); // stage input 1-based
                idx = start + Mathf.Max(0, lv1 - 1);
            }
        }
        else if (int.TryParse(txt, out var flat)) idx = flat;

        if (idx >= 0) GotoLevelIndex(idx);
    }

    /* ---------------- Cards & Characters ---------------- */

    void SaveOwnedToDisk()
    {
        if (SM == null || INV == null) return;
        var list = new List<string>();
        foreach (var oc in INV.Owned)
        {
            if (oc == null || !oc.data) continue;
            int n = Mathf.Max(1, oc.stacks);
            for (int i = 0; i < n; i++) list.Add(oc.data.id);
        }
        SM.Data.ownedCards = list;
        SM.Data.energyLimit = INV.EnergyLimit;
        SM.SaveToDisk();
    }

    void AddAllCards()
    {
        if (INV == null) return;
        var all = Resources.LoadAll<CardData>("Cards");
        foreach (var c in all) INV.AddCard(c);
        SaveOwnedToDisk();
        Debug.Log("[DEV] Added all cards (x1 each).");
        var ui = FindObjectOfType<CardManagementUI>(true);
        ui?.RefreshAllNow();
    }

    void ClearCards()
    {
        if (INV == null) return;
        INV.ClearAll();
        SaveOwnedToDisk();
        Debug.Log("[DEV] Cleared all cards.");
        var ui = FindObjectOfType<CardManagementUI>(true);
        ui?.RefreshAllNow();
    }

    void AddSpecific(string id)
    {
        if (INV == null) return;
        var c = CardLibrary.GetById(id);
        if (c)
        {
            INV.AddCard(c);
            SaveOwnedToDisk();
            Debug.Log($"[DEV] +1 {id}");
        }
        else Debug.LogWarning($"[DEV] Card id not found: {id}");
        var ui = FindObjectOfType<CardManagementUI>(true);
        ui?.RefreshAllNow();
    }

    void UnlockAllCharacters()
    {
        if (SM == null) return;
        SM.Data.nesoNesaNesdaUnlocked = true;
        SM.Data.tuKaLaUnlocked = true;
        SM.Data.aposdaUnlocked = true;
        SM.Data.porkyUnlocked = true;
        SM.Data.gameClearedOnce = true; // jaga-jaga
        SM.SaveToDisk();

        // Refresh UI kalau sedang di Character Select
        var sel = FindObjectOfType<CharacterSelectController>(true);
        if (sel)
        {
            sel.SendMessage("RefreshUnlockFlags", SendMessageOptions.DontRequireReceiver);
            sel.SendMessage("ApplySelection", true, SendMessageOptions.DontRequireReceiver);
        }

        Debug.Log("[DEV] All characters unlocked & UI refreshed.");
    }

    void SetCharacter(string id)
    {
        if (SM == null) return;
        SM.SetChosenCharacterKey(id);
    }

    void RefillFever()
    {
        var f = FindObjectOfType<FeverMeterController>(true);
        if (!f) return;
        StartCoroutine(FillFeverTo(1f));
    }

    System.Collections.IEnumerator FillFeverTo(float target)
    {
        var f = FindObjectOfType<FeverMeterController>(true);
        if (!f) yield break;

        int guard = 0;
        while (f.FillPercent + 0.001f < target && guard++ < 300)
        {
            f.AddOrangeHitInstant();
            yield return null;
        }
    }

    void AddBalls(int n)
    {
        if (GM) GM.GainBall(Mathf.Max(1, n));
    }

    void ToggleEasyMode(bool on)
    {
        if (SM == null) return;
        PlayerPrefs.SetInt("set_easymode", on ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log("[DEV] EasyMode PlayerPrefs = " + on);
    }

    void ResetRunToStageStart()
    {
        if (SM == null || LM == null) return;
        int startIdx = LM.GetStageStartIndex(_stageForStart + 1); // _stageForStart=0 → Stage1
        SM.Data.levelIndex = startIdx;
        SM.Data.stageIndex = _stageForStart;
        SM.Data.lives = 3;
        SM.Data.totalScore = 0;
        SM.SaveToDisk();
        LM.LoadLevelIndex(startIdx);
        RequestFixStageMusicOnNextScene();
    }

    /* ---------------- IMGUI ---------------- */

    void OnGUI()
    {
        if (!enabled) return;

        // scale IMGUI
        var prev = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                                   new Vector3(uiScale, uiScale, 1f));

        var style = new GUIStyle(GUI.skin.label) { richText = true };

        if (_showQuick) DrawQuickBar(style);
        if (_showFull) DrawMain(style);

        GUI.matrix = prev;
    }

    void DrawQuickBar(GUIStyle style)
    {
        GUILayout.BeginArea(new Rect(8, 8, 360, 92), "[ DEV QUICK ]", GUI.skin.window);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<< Prev", GUILayout.Height(30))) GoPrev();
        if (GUILayout.Button("Win", GUILayout.Height(30))) ForceWin();
        if (GUILayout.Button("Lose", GUILayout.Height(30))) ForceLose();
        if (GUILayout.Button("Next >>", GUILayout.Height(30))) GoNext();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        _gotoLevelText = GUILayout.TextField(_gotoLevelText, GUILayout.Width(140));
        if (GUILayout.Button("Go (1-3 or 13)", GUILayout.Height(24))) GotoLevelParsed();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    bool ConfirmFullReset()
    {
#if UNITY_EDITOR
        return EditorUtility.DisplayDialog(
            "Full Data Reset",
            "Ini akan menghapus SAVE, PlayerPrefs, dan leaderboard lokal (jika ada). Lanjut?",
            "YA, Hapus Semua", "Batal");
#else
    // Runtime: klik 2x dalam 3 detik untuk konfirmasi
    if (!_confirmResetArmed || Time.unscaledTime > _confirmDeadline)
    {
        _confirmResetArmed = true;
        _confirmDeadline = Time.unscaledTime + 3f;
        Debug.Log("[DEV] Tekan lagi dalam 3 detik untuk mengonfirmasi FULL DATA RESET.");
        return false;
    }
    _confirmResetArmed = false;
    return true;
#endif
    }

    void DrawMain(GUIStyle style)
    {
        GUILayout.BeginArea(new Rect(12, 110, 520, 560), "[ DEVELOPER MENU ]", GUI.skin.window);
        _scroll = GUILayout.BeginScrollView(_scroll);

        // UI Scale
        GUILayout.BeginHorizontal();
        GUILayout.Label("UI Scale", GUILayout.Width(70));
        uiScale = GUILayout.HorizontalSlider(uiScale, 0.75f, 1.75f, GUILayout.Width(150));
        GUILayout.Label(uiScale.ToString("0.00"), GUILayout.Width(40));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);

        GUILayout.Label($"<b>Scene:</b> {SceneManager.GetActiveScene().name}", style);
        GUILayout.Label($"<b>Level:</b> {(LM ? LM.CurrentIndex : -1)}", style);
        GUILayout.Space(6);

        GUILayout.Label("<b>Level Control</b>", style);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<< Prev", GUILayout.Height(28))) GoPrev();
        if (GUILayout.Button("Reload", GUILayout.Height(28))) Reload();
        if (GUILayout.Button("Next >>", GUILayout.Height(28))) GoNext();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Win (F7)", GUILayout.Height(28))) ForceWin();
        if (GUILayout.Button("Force Lose (F8)", GUILayout.Height(28))) ForceLose();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Go to:", GUILayout.Width(60));
        _gotoLevelText = GUILayout.TextField(_gotoLevelText, GUILayout.Width(120));
        if (GUILayout.Button("GO", GUILayout.Width(60))) GotoLevelParsed();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("<b>Run Reset</b>", style);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Stage start:", GUILayout.Width(90));
        _stageForStart = Mathf.Clamp(EditorIntField(_stageForStart), 0, 4);
        if (GUILayout.Button("Reset to Stage-Start", GUILayout.Height(24))) ResetRunToStageStart();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("<b>Cards</b>", style);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add ALL", GUILayout.Height(26))) AddAllCards();
        if (GUILayout.Button("CLEAR All", GUILayout.Height(26))) ClearCards();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        foreach (var id in knownCardIds)
            if (GUILayout.Button(id, GUILayout.Height(24))) AddSpecific(id);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("<b>Character</b>", style);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Unlock ALL", GUILayout.Height(24))) UnlockAllCharacters();
        if (GUILayout.Button("Use NesoNesaNesda")) SetCharacter("NesoNesaNesda");
        if (GUILayout.Button("Use TuKaLa")) SetCharacter("TuKaLa");
        if (GUILayout.Button("Use Aposda")) SetCharacter("Aposda");
        if (GUILayout.Button("Use Porky")) SetCharacter("Porky");
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("<b>Gameplay Tweaks</b>", style);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+3 Balls")) AddBalls(3);
        if (GUILayout.Button("Refill Fever")) RefillFever();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("<b>QoL</b>", style);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("FF Unlock", GUILayout.Height(24))) SetFastForwardUnlocked(true);
        if (GUILayout.Button("FF Lock", GUILayout.Height(24))) SetFastForwardUnlocked(false);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("<b>Fever & Power Test</b>", style);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Keep 1 Orange Peg", GUILayout.Height(26))) KeepOnlyOneOrangePeg();
        if (GUILayout.Button("Activate Power", GUILayout.Height(26))) ActivateCurrentCharacterPower();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("<b>Next Ball Element</b>", style);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Neutral", GUILayout.Height(24))) SetNextBallElement(ElementType.Neutral);
        if (GUILayout.Button("Fire", GUILayout.Height(24))) SetNextBallElement(ElementType.Fire);
        if (GUILayout.Button("Water", GUILayout.Height(24))) SetNextBallElement(ElementType.Water);
        if (GUILayout.Button("Wind", GUILayout.Height(24))) SetNextBallElement(ElementType.Wind);
        if (GUILayout.Button("Earth", GUILayout.Height(24))) SetNextBallElement(ElementType.Earth);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("<b>Add Score (Instant)</b>", style);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+1,000", GUILayout.Height(24))) AddScoreInstant(1_000);
        if (GUILayout.Button("+10,000", GUILayout.Height(24))) AddScoreInstant(10_000);
        if (GUILayout.Button("+100,000", GUILayout.Height(24))) AddScoreInstant(100_000);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("<b style='color:#f66'>DANGER ZONE</b>", style);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("FULL DATA RESET (save + prefs + leaderboard)", GUILayout.Height(28)))
        {
            if (ConfirmFullReset())
                FullDataReset();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("EasyMode ON")) ToggleEasyMode(true);
        if (GUILayout.Button("EasyMode OFF")) ToggleEasyMode(false);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("<b>Misc</b>", style);
        if (GUILayout.Button("Open Save Folder")) OpenSaveFolder();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    /* --------- BGM fixer setelah lompat level --------- */
    void RequestFixStageMusicOnNextScene()
    {
        SceneManager.sceneLoaded -= _FixMusicOnLoaded;
        SceneManager.sceneLoaded += _FixMusicOnLoaded;
    }
    void _FixMusicOnLoaded(Scene s, LoadSceneMode m)
    {
        SceneManager.sceneLoaded -= _FixMusicOnLoaded;
        StartCoroutine(FixMusicAfterOneFrame());
    }
    System.Collections.IEnumerator FixMusicAfterOneFrame()
    {
        yield return null; // tunggu GameManager.StartLevel jalan
        var lm = LevelManager.Instance;
        string key = lm ? lm.GetCurrentStageBgmKey() : null;
        if (!string.IsNullOrEmpty(key))
            AudioManager.I?.PlayMusicIfChanged(key, true);
    }

    /* --------- Blocker Canvas (uGUI) --------- */
    void EnsureBlocker()
    {
        if (blockerGO) return;

        blockerGO = new GameObject("__DevBlockerCanvas");
        DontDestroyOnLoad(blockerGO);
        var canvas = blockerGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767; // di atas semua
        blockerGO.AddComponent<GraphicRaycaster>();

        var child = new GameObject("Blocker");
        child.transform.SetParent(blockerGO.transform, false);
        blockerImage = child.AddComponent<Image>();
        blockerImage.color = new Color(0, 0, 0, 0.001f); // nyaris transparan
        blockerImage.raycastTarget = true;

        var rt = blockerImage.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        blockerGO.SetActive(false);
    }

    void SetBlockerActive(bool on)
    {
        EnsureBlocker();
        if (blockerGO.activeSelf != on) blockerGO.SetActive(on);
    }

    /* ---------------- New Dev Cheats ---------------- */

    // 1) Set elemen untuk tembakan BERIKUTNYA (bukan bola yang sedang terbang)
    void SetNextBallElement(ElementType e)
    {
        try
        {
            ElementSystem.SetNext(e);
            Debug.Log("[DEV] NextBall element = " + e);
            AudioManager.I?.PlayUI("MainMenuClick"); // sfx kecil biar kerasa
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[DEV] SetNextBallElement failed: " + ex.Message);
        }
    }

    // 2) Tambah skor instan (langsung ke HUD, ikut global multiplier)
    void AddScoreInstant(int pts)
    {
        try
        {
            ScoreManager.AddDirectBonus(Mathf.Max(0, pts));
            Debug.Log("[DEV] +Score " + pts);
            AudioManager.I?.PlayUI("MainMenuClick");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[DEV] AddScoreInstant failed: " + ex.Message);
        }
    }

    // 3) Full data reset: hapus save + bersihkan leaderboard lokal + PlayerPrefs
    void FullDataReset()
    {
        try
        {
            // a) Stop musik supaya tidak nyangkut saat scene reload
            AudioManager.I?.StopMusic();

            // b) Hapus save JSON
            SaveManager.I?.DeleteSave();

            // c) Bersihkan PlayerPrefs (flag2 dev/easy mode dsb)
            PlayerPrefs.DeleteAll(); PlayerPrefs.Save();
            ResetSaveDataInMemoryToDefaults();

            // d) Coba bersihkan Local Leaderboard (nama metode bisa beda2 — pakai refleksi agar aman)
            var llm = LocalLeaderboardManager.I;
            if (llm != null)
            {
                var t = llm.GetType();
                var mClearAll = t.GetMethod("ClearAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var mClear = t.GetMethod("Clear", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var mReset = t.GetMethod("ResetAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (mClearAll != null) mClearAll.Invoke(llm, null);
                else if (mReset != null) mReset.Invoke(llm, null);
                else if (mClear != null)
                {
                    // coba hapus papan default kamu (dipakai SaveManager.SubmitLeaderboardWin)
                    // kalau punya key lain, duplikasi baris ini sesuai kebutuhan
                    mClear.Invoke(llm, new object[] { "Adventure_All25" });
                }
                Debug.Log("[DEV] LocalLeaderboard reset via reflection.");
            }
            else
            {
                Debug.Log("[DEV] LocalLeaderboardManager.I not found — lewati pembersihan leaderboard.");
            }

            // e) Reload scene menu (atau scene aktif sebagai fallback)
            try
            {
                var menuScene = "MainMenu"; // ganti nama jika project-mu beda
                if (Application.CanStreamedLevelBeLoaded(menuScene))
                    SceneManager.LoadScene(menuScene);
                else
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            catch { /* aman diabaikan */ }

            Debug.Log("[DEV] FULL DATA RESET complete.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[DEV] FullDataReset failed: " + ex.Message);
        }
    }


    int EditorIntField(int val)
    {
#if UNITY_EDITOR
        string s = GUILayout.TextField((val + 1).ToString(), GUILayout.Width(40));
        if (int.TryParse(s, out var typed)) return Mathf.Max(0, typed - 1);
        return val;
#else
        return val;
#endif
    }

    void OpenSaveFolder()
    {
#if UNITY_EDITOR
        string path = Application.persistentDataPath;
        EditorUtility.RevealInFinder(path);
#endif
    }
}
