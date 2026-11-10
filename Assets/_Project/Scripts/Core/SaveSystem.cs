using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TSAGame
{
    /// <summary>
    /// Saving and loading. The game autosaves every time a chapter starts. The menu's Save button copies that
    /// autosave into the save slot, and Load continues from the slot (or from the autosave if nothing was saved).
    /// Saves are stored with PlayerPrefs, so there are no files to manage.
    /// </summary>
    public static class SaveSystem
    {
        const string AutoKey = "tsa.autosave";
        const string SlotKey = "tsa.save";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameState.Instance == null || scene.name == SceneFlow.MissionControl) return;
            if (Array.IndexOf(SceneFlow.All, scene.name) < 0) return;
            PlayerPrefs.SetString(AutoKey, JsonUtility.ToJson(GameState.Instance.Capture(scene.name)));
            PlayerPrefs.Save();
        }

        static GameState.Snapshot Read(string key)
        {
            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return null;
            var snapshot = JsonUtility.FromJson<GameState.Snapshot>(json);
            return snapshot != null && Array.IndexOf(SceneFlow.All, snapshot.scene) >= 0 ? snapshot : null;
        }

        public static bool HasProgress => Read(AutoKey) != null;
        public static bool HasSave => Read(SlotKey) != null || HasProgress;

        /// <summary>The chapter a Load would continue from, or null.</summary>
        public static string SavedChapter
        {
            get
            {
                var snapshot = Read(SlotKey) ?? Read(AutoKey);
                return snapshot == null ? null : SceneFlow.DisplayName(snapshot.scene);
            }
        }

        /// <summary>Keeps the latest progress in the save slot. Returns the chapter name, or null if there is nothing to save.</summary>
        public static string Save()
        {
            var snapshot = Read(AutoKey);
            if (snapshot == null) return null;
            PlayerPrefs.SetString(SlotKey, JsonUtility.ToJson(snapshot));
            PlayerPrefs.Save();
            return SceneFlow.DisplayName(snapshot.scene);
        }

        /// <summary>Restores the saved game and loads its chapter. False if there is no save.</summary>
        public static bool Load()
        {
            var snapshot = Read(SlotKey) ?? Read(AutoKey);
            if (snapshot == null) return false;
            GameState.Instance.Restore(snapshot);
            SceneFlow.Load(snapshot.scene, 1f);
            return true;
        }
    }
}
