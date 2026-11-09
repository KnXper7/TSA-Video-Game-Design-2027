using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TSAGame
{
    /// <summary>
    /// The one place that decides whether the player is "in a menu". Any panel or dialogue choice that needs the
    /// mouse calls Push(this) when it opens and Release(this) when it closes. While anything holds a lock the
    /// cursor is free and the player can't look, walk or interact; when the last lock is released the cursor is
    /// locked again. Locks are cleared on every scene load, so a panel left open can never strand the player.
    /// </summary>
    public static class InputLock
    {
        static readonly HashSet<object> owners = new();

        public static bool IsLocked => owners.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            owners.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            owners.Clear();
            ApplyCursor();
        }

        public static void Push(object owner)
        {
            owners.Add(owner);
            ApplyCursor();
        }

        public static void Release(object owner)
        {
            owners.Remove(owner);
            ApplyCursor();
        }

        /// <summary>Locks the cursor for play, or frees it for menus. Only scenes with a player lock it.</summary>
        public static void ApplyCursor()
        {
            bool play = !IsLocked && Object.FindAnyObjectByType<FirstPersonController>() != null;
            Cursor.lockState = play ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !play;
        }
    }
}
