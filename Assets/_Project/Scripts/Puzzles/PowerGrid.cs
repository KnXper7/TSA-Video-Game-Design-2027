using System;
using System.Collections.Generic;
using System.Linq;

namespace TSAGame
{
    [Serializable]
    public class PowerSystem
    {
        public string name;
        public int required = 1;
        [NonSerialized] public int allocated;

        public bool Online => allocated >= required;
    }

    /// <summary>
    /// Puzzle rules for routing a limited number of power cells between ship systems.
    /// Pure logic, no Unity objects, so it is easy to test and to reuse for later power puzzles.
    /// </summary>
    public class PowerGrid
    {
        public const int MaxPerSystem = 3;

        readonly List<PowerSystem> systems;

        public PowerGrid(IEnumerable<PowerSystem> systems, int capacity)
        {
            this.systems = systems.Select(s => new PowerSystem { name = s.name, required = s.required }).ToList();
            Capacity = capacity;
        }

        public IReadOnlyList<PowerSystem> Systems => systems;
        public int Capacity { get; set; }
        public int Used => systems.Sum(s => s.allocated);
        public int Free => Capacity - Used;

        public event Action Changed;

        public bool CanAdd(int index) => Free > 0 && systems[index].allocated < MaxPerSystem;
        public bool CanRemove(int index) => systems[index].allocated > 0;

        public bool Add(int index)
        {
            if (!CanAdd(index)) return false;
            systems[index].allocated++;
            Changed?.Invoke();
            return true;
        }

        public bool Remove(int index)
        {
            if (!CanRemove(index)) return false;
            systems[index].allocated--;
            Changed?.Invoke();
            return true;
        }

        public bool IsOnline(string systemName) =>
            systems.Any(s => s.name == systemName && s.Online);

        public bool AreOnline(params string[] systemNames) => systemNames.All(IsOnline);
    }
}
