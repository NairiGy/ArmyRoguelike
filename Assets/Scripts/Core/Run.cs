using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    /// <summary>A unit in the player's army between battles: its type and the item in its slot.</summary>
    public sealed class ArmyMember
    {
        public readonly UnitStats Stats;

        public Equipment Equipment { get; internal set; }

        public ArmyMember(UnitStats stats, Equipment equipment = null)
        {
            Stats = stats;
            Equipment = equipment;
        }

        public static List<ArmyMember> StartingArmy() => new List<ArmyMember>
        {
            new ArmyMember(UnitStats.Warrior),
            new ArmyMember(UnitStats.Archer),
            new ArmyMember(UnitStats.Rogue),
        };
    }

    /// <summary>One fixed battle: its board layout and enemy army.</summary>
    public sealed class Encounter
    {
        public readonly string Name;
        private readonly GridPos[] _obstacles;
        private readonly (UnitStats stats, GridPos pos)[] _enemies;

        public Encounter(string name, GridPos[] obstacles, (UnitStats, GridPos)[] enemies)
        {
            Name = name;
            _obstacles = obstacles;
            _enemies = enemies;
        }

        public Battle CreateBattle(IEnumerable<ArmyMember> army) => new Battle(new Board(8, 8, _obstacles), army, _enemies);

        private static GridPos P(int x, int y) => new GridPos(x, y);

        public static readonly Encounter Wolves = new Encounter("Wolf Pack",
            new[] { P(1, 3), P(2, 3), P(5, 4), P(6, 4) },
            new[]
            {
                (UnitStats.PackLeader, P(3, 7)),
                (UnitStats.Wolf, P(2, 6)),
                (UnitStats.Wolf, P(4, 6)),
                (UnitStats.Wolf, P(5, 7)),
            });

        public static readonly Encounter Necromancer = new Encounter("Necromancer",
            new[] { P(1, 4), P(4, 3), P(6, 5), P(3, 6) },
            new[]
            {
                (UnitStats.Necromancer, P(4, 7)),
                (UnitStats.Skeleton, P(1, 6)),
                (UnitStats.Skeleton, P(3, 5)),
                (UnitStats.Skeleton, P(5, 5)),
                (UnitStats.Skeleton, P(6, 6)),
            });

        /// <summary>The ogre is 2x2; its anchor (3,5) covers (3,5) to (4,6).</summary>
        public static readonly Encounter Ogre = new Encounter("Ogre",
            new[] { P(1, 3), P(6, 3) },
            new[] { (UnitStats.Ogre, P(3, 5)) });

        public static readonly IReadOnlyList<Encounter> MiniRun = new[] { Wolves, Necromancer, Ogre };
    }

    public enum RunState
    {
        Battle,
        Shop,
        Won,
        Lost,
    }

    /// <summary>
    /// A fixed chain of battles with a shop after each won battle. Gold depends on how many units
    /// survived. Every battle starts with the whole army at full HP; only equipment carries over.
    /// </summary>
    public sealed class Run
    {
        public const int BaseGold = 10;
        public const int GoldPerSurvivor = 5;
        public const int ShopSize = 3;

        private readonly Random _rng;
        private readonly List<Equipment> _offers = new List<Equipment>();

        public readonly int Seed;
        public readonly IReadOnlyList<Encounter> Encounters;
        public readonly List<ArmyMember> Army;

        public int Gold { get; private set; }
        public int BattleIndex { get; private set; }
        public RunState State { get; private set; } = RunState.Battle;
        public Battle CurrentBattle { get; private set; }

        public IReadOnlyList<Equipment> Offers => _offers;

        public Encounter CurrentEncounter => Encounters[BattleIndex];

        public Run(int seed, IReadOnlyList<Encounter> encounters = null)
        {
            Seed = seed;
            _rng = new Random(seed);
            Encounters = encounters ?? Encounter.MiniRun;
            Army = ArmyMember.StartingArmy();
        }

        public Battle StartBattle()
        {
            if (State != RunState.Battle)
                throw new InvalidOperationException($"Can't start a battle while in {State}.");
            CurrentBattle = CurrentEncounter.CreateBattle(Army);
            return CurrentBattle;
        }

        public static int GoldFor(int survivors) => BaseGold + GoldPerSurvivor * survivors;

        /// <summary>Call once the current battle is over. Returns the gold earned (0 for a defeat).</summary>
        public int FinishBattle()
        {
            var battle = CurrentBattle;
            if (State != RunState.Battle || battle == null || !battle.IsOver)
                throw new InvalidOperationException("The current battle isn't over.");

            if (battle.Phase == Phase.Defeat)
            {
                State = RunState.Lost;
                return 0;
            }

            var earned = GoldFor(battle.Living(Team.Player).Count());
            Gold += earned;
            if (BattleIndex == Encounters.Count - 1)
            {
                State = RunState.Won;
                return earned;
            }

            State = RunState.Shop;
            RollOffers();
            return earned;
        }

        public bool CanBuy(Equipment item, ArmyMember member) =>
            State == RunState.Shop && _offers.Contains(item) && Army.Contains(member) &&
            Gold >= item.Price && item.CanEquip(member.Stats);

        /// <summary>Buys an offer and puts it in the member's slot, replacing (and losing) its old item.</summary>
        public bool Buy(Equipment item, ArmyMember member)
        {
            if (!CanBuy(item, member))
                return false;
            Gold -= item.Price;
            member.Equipment = item;
            _offers.Remove(item);
            return true;
        }

        public void LeaveShop()
        {
            if (State != RunState.Shop)
                return;
            _offers.Clear();
            BattleIndex++;
            State = RunState.Battle;
        }

        private void RollOffers()
        {
            _offers.Clear();
            var pool = Equipment.Catalog.ToList();
            while (_offers.Count < ShopSize && pool.Count > 0)
            {
                var i = _rng.Next(pool.Count);
                _offers.Add(pool[i]);
                pool.RemoveAt(i);
            }
        }
    }
}
