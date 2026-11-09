using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSAGame
{
    public enum Stat { Engineering, Navigation, Diplomacy, Survival }

    /// <summary>
    /// Everything that carries between scenes: stats, XP and level, the hidden Trust score and story flags.
    /// Created automatically before the first scene loads, so every scene can rely on GameState.Instance.
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public const int MaxStat = 10;
        public const int PointsPerLevel = 2;

        public static GameState Instance { get; private set; }

        [Header("Stats (starting values)")]
        [SerializeField] int engineering = 2;
        [SerializeField] int navigation = 2;
        [SerializeField] int diplomacy = 2;
        [SerializeField] int survival = 2;

        [Header("Progress")]
        [SerializeField] int level = 1;
        [SerializeField] int xp;
        [SerializeField] int unspentPoints;

        [Header("Hidden relationship score with the other pilot (0-100)")]
        [SerializeField, Range(0, 100)] int trust = 30;

        [Header("Ship fuel cells. Each gate jump burns one.")]
        [SerializeField] int fuel;

        readonly HashSet<string> flags = new();

        public event Action<int, string> XpGained;   // amount, reason
        public event Action<int> LeveledUp;          // new level
        public event Action<int> TrustChanged;       // new value
        public event Action<int> FuelChanged;        // new value

        public int Level => level;
        public int Xp => xp;
        public int XpToNextLevel => 100 * level;
        public int UnspentPoints => unspentPoints;
        public int Trust => trust;
        public int Fuel => fuel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[GameState]");
            go.AddComponent<GameState>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public int Get(Stat stat) => stat switch
        {
            Stat.Engineering => engineering,
            Stat.Navigation => navigation,
            Stat.Diplomacy => diplomacy,
            Stat.Survival => survival,
            _ => 0
        };

        /// <summary>True when the stat meets the requirement. Use this for stat-based puzzle solutions and dialogue checks.</summary>
        public bool Check(Stat stat, int required) => Get(stat) >= required;

        /// <summary>Spends one unspent point on a stat. Returns false if there are no points or the stat is maxed.</summary>
        public bool SpendPoint(Stat stat)
        {
            if (unspentPoints <= 0 || Get(stat) >= MaxStat) return false;
            Set(stat, Get(stat) + 1);
            unspentPoints--;
            return true;
        }

        public void AddXp(int amount, string reason)
        {
            if (amount <= 0) return;
            xp += amount;
            XpGained?.Invoke(amount, reason);

            while (xp >= XpToNextLevel)
            {
                xp -= XpToNextLevel;
                level++;
                unspentPoints += PointsPerLevel;
                LeveledUp?.Invoke(level);
            }
        }

        public void ChangeTrust(int delta)
        {
            trust = Mathf.Clamp(trust + delta, 0, 100);
            TrustChanged?.Invoke(trust);
        }

        public void ChangeFuel(int delta)
        {
            fuel = Mathf.Max(0, fuel + delta);
            FuelChanged?.Invoke(fuel);
        }

        public void SetFlag(string flag) => flags.Add(flag);
        public bool HasFlag(string flag) => flags.Contains(flag);

        /// <summary>How many flags start with the prefix (for example "record." for Architect records found).</summary>
        public int CountFlags(string prefix)
        {
            int count = 0;
            foreach (var flag in flags)
                if (flag.StartsWith(prefix)) count++;
            return count;
        }

        /// <summary>Back to a fresh game (used after an ending).</summary>
        public void ResetAll()
        {
            engineering = navigation = diplomacy = survival = 2;
            level = 1;
            xp = 0;
            unspentPoints = 0;
            trust = 30;
            flags.Clear();
            fuel = 0;
            FuelChanged?.Invoke(fuel);
        }

        void Set(Stat stat, int value)
        {
            value = Mathf.Clamp(value, 0, MaxStat);
            switch (stat)
            {
                case Stat.Engineering: engineering = value; break;
                case Stat.Navigation: navigation = value; break;
                case Stat.Diplomacy: diplomacy = value; break;
                case Stat.Survival: survival = value; break;
            }
        }
    }
}
