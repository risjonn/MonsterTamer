using System;
using System.Collections.Generic;

namespace MonsterTamer
{
    public class Region
    {
        private string           _name;
        private List<WildMonster> _encounterTable;
        private List<AITrainer>  _trainers;
        private bool             _isCleared;
        private static Random    _rng = new Random();

        public Region(string name)
        {
            _name           = name;
            _encounterTable = new List<WildMonster>();
            _trainers       = new List<AITrainer>();
            _isCleared      = false;
        }

        public string Name      => _name;
        public bool   IsCleared => _isCleared;

        public void AddEncounter(WildMonster monster)
        {
            _encounterTable.Add(monster);
        }

        public void AddTrainer(AITrainer trainer)
        {
            _trainers.Add(trainer);
        }

        public WildMonster? GetRandomEncounter()
        {
            if (_encounterTable.Count == 0) return null;
            int index = _rng.Next(_encounterTable.Count);

            // Return a fresh instance via MonsterFactory based on name
            WildMonster template = _encounterTable[index];
            int randomLevel = Math.Max(1, template.Level + _rng.Next(-2, 3));
            return CreateFreshEncounter(template.Name, randomLevel);
        }

        private WildMonster? CreateFreshEncounter(string name, int level)
        {
            return name switch
            {
                "Kelabit"  => null, // Kelabit is a TamedMonster starter
                "Bekantan" => MonsterFactory.CreateBekantan(level),
                "Mawas"    => MonsterFactory.CreateMawas(level),
                "Rajah"    => MonsterFactory.CreateRajah(level),
                "Enggang"  => MonsterFactory.CreateEnggang(level),
                "Pelanduk" => MonsterFactory.CreatePelanduk(level),
                "Temaju"   => MonsterFactory.CreateTemaju(level),
                "Buaya"    => MonsterFactory.CreateBuaya(level),
                _          => null
            };
        }

        public AITrainer? GetNextTrainer()
        {
            foreach (AITrainer t in _trainers)
            {
                if (!t.Party.IsWiped()) return t;
            }
            return null;
        }

        public void Clear()
        {
            _isCleared = true;
        }

        public List<AITrainer> Trainers => _trainers;
    }
}
