using System.Collections.Generic;

namespace MonsterTamer
{
    public class Party
    {
        private List<TamedMonster> _monsters;
        private const int MaxSlots = 6;

        public Party()
        {
            _monsters = new List<TamedMonster>();
        }

        public int Count => _monsters.Count;

        public TamedMonster this[int index] => _monsters[index];

        public bool AddMonster(TamedMonster monster)
        {
            if (_monsters.Count >= MaxSlots) return false;
            _monsters.Add(monster);
            return true;
        }

        public void RemoveMonster(TamedMonster monster)
        {
            _monsters.Remove(monster);
        }

        public TamedMonster? GetActiveMonster()
        {
            foreach (TamedMonster m in _monsters)
            {
                if (m.IsAlive) return m;
            }
            return null;
        }

        public bool SwapToFront(int index)
        {
            if (index <= 0 || index >= _monsters.Count) return false;
            TamedMonster selected = _monsters[index];
            _monsters.RemoveAt(index);
            _monsters.Insert(0, selected);
            return true;
        }

        public bool IsWiped()
        {
            foreach (TamedMonster m in _monsters)
            {
                if (m.IsAlive) return false;
            }
            return true;
        }
    }
}
