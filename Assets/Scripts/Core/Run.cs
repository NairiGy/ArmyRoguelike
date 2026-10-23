using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    /// <summary>A unit in the player's army between battles: its type, its item and run-long bonuses.</summary>
    public sealed class ArmyMember
    {
        public readonly UnitStats Stats;

        public Equipment Equipment { get; internal set; }

        /// <summary>Damage gained during the run, e.g. from Bloodthirst kills.</summary>
        public int BonusDamage { get; internal set; }

        /// <summary>Max HP gained during the run, e.g. from a shrine.</summary>
        public int BonusMaxHp { get; internal set; }

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

        public static readonly Encounter Goblins = new Encounter("Goblin Bombers",
            new[] { P(2, 3), P(5, 3), P(3, 5) },
            new[]
            {
                (UnitStats.GoblinBomber, P(1, 7)),
                (UnitStats.GoblinBomber, P(4, 7)),
                (UnitStats.GoblinBomber, P(6, 7)),
                (UnitStats.Goblin, P(2, 5)),
                (UnitStats.Goblin, P(5, 5)),
            });

        /// <summary>The ogre is 2x2; its anchor (3,5) covers (3,5) to (4,6).</summary>
        public static readonly Encounter Ogre = new Encounter("Ogre",
            new[] { P(1, 3), P(6, 3) },
            new[] { (UnitStats.Ogre, P(3, 5)) });

        /// <summary>The ordinary battles the map draws from. The ogre is always the final boss.</summary>
        public static readonly IReadOnlyList<Encounter> Regular = new[] { Wolves, Necromancer, Goblins };
    }

    public enum NodeKind
    {
        Battle,
        Shop,
        Event,
        Boss,
    }

    public enum EventKind
    {
        /// <summary>Recruit a sapper for free (or take gold if the army is full).</summary>
        WanderingSapper,

        /// <summary>A random free item for one unit.</summary>
        AbandonedCart,

        /// <summary>Pay gold for a 50% chance at more.</summary>
        Gambler,

        /// <summary>A surprise wolf fight that pays extra gold.</summary>
        Ambush,

        /// <summary>One unit gets +2 max HP for the run.</summary>
        Shrine,
    }

    /// <summary>One choice on the map.</summary>
    public sealed class MapNode
    {
        public readonly NodeKind Kind;

        /// <summary>For battle and boss nodes.</summary>
        public readonly Encounter Encounter;

        public MapNode(NodeKind kind, Encounter encounter = null)
        {
            Kind = kind;
            Encounter = encounter;
        }

        public string Label
        {
            get
            {
                switch (Kind)
                {
                    case NodeKind.Battle: return $"Battle: {Encounter.Name}";
                    case NodeKind.Boss: return $"Boss: {Encounter.Name}";
                    case NodeKind.Shop: return "Shop";
                    default: return "?";
                }
            }
        }
    }

    public enum RunState
    {
        /// <summary>Choosing the next node on the map.</summary>
        Map,
        Battle,
        Shop,
        Event,
        Won,
        Lost,
    }

    /// <summary>
    /// A run across a small branching map: at each step the player picks one of two nodes (battle,
    /// shop or ? event); the last step is always the ogre. Gold depends on how many units survived.
    /// Every battle starts with the whole army at full HP; equipment and run-long bonuses carry over.
    /// </summary>
    public sealed class Run
    {
        public const int BaseGold = 10;
        public const int GoldPerSurvivor = 5;
        public const int ShopSize = 3;
        public const int MaxArmySize = 4;
        public const int RecruitPrice = 15;
        public const int Steps = 6;
        public const int GambleCost = 10;
        public const int GambleWin = 25;
        public const int AmbushBonusGold = 5;
        public const int FullArmyGold = 15;
        public const int ShrineHp = 2;

        private readonly Random _rng;
        private readonly List<Equipment> _offers = new List<Equipment>();
        private readonly List<IReadOnlyList<MapNode>> _map = new List<IReadOnlyList<MapNode>>();
        private readonly List<int> _chosen = new List<int>();

        public readonly int Seed;
        public readonly List<ArmyMember> Army;

        public int Gold { get; private set; }

        /// <summary>The map step the player is on or choosing from (0-based).</summary>
        public int StepIndex { get; private set; }

        public RunState State { get; private set; } = RunState.Map;
        public Battle CurrentBattle { get; private set; }
        public Encounter CurrentEncounter { get; private set; }
        public MapNode CurrentNode { get; private set; }

        /// <summary>The current battle came from an ambush event, which pays extra gold.</summary>
        public bool IsAmbush { get; private set; }

        public EventKind CurrentEvent { get; private set; }

        /// <summary>The item an Abandoned Cart event offers.</summary>
        public Equipment CartItem { get; private set; }

        /// <summary>Text describing how the last event turned out, for the screen.</summary>
        public string EventOutcome { get; private set; }

        public IReadOnlyList<Equipment> Offers => _offers;
        public IReadOnlyList<IReadOnlyList<MapNode>> Map => _map;

        /// <summary>Index of the node chosen at each past step.</summary>
        public IReadOnlyList<int> Chosen => _chosen;

        public bool ShopHasRecruit { get; private set; }

        public Run(int seed)
        {
            Seed = seed;
            _rng = new Random(seed);
            Army = ArmyMember.StartingArmy();
            GenerateMap();
        }

        public IReadOnlyList<MapNode> CurrentChoices => _map[StepIndex];

        // ---------- Map ----------

        /// <summary>
        /// Step 1 offers two different battles. Steps 2 to 5 mix battles, shops and events, never two
        /// identical nodes side by side. Step 6 is the ogre.
        /// </summary>
        private void GenerateMap()
        {
            var first = Encounter.Regular.OrderBy(_ => _rng.Next()).Take(2).Select(e => new MapNode(NodeKind.Battle, e)).ToList();
            _map.Add(first);

            for (var step = 1; step < Steps - 1; step++)
            {
                var pair = new List<MapNode>();
                while (pair.Count < 2)
                {
                    var node = RandomNode();
                    if (pair.Any(n => n.Kind == node.Kind && n.Encounter == node.Encounter))
                        continue;
                    pair.Add(node);
                }

                _map.Add(pair);
            }

            _map.Add(new[] { new MapNode(NodeKind.Boss, Encounter.Ogre) });
        }

        private MapNode RandomNode()
        {
            var roll = _rng.Next(100);
            if (roll < 50)
                return new MapNode(NodeKind.Battle, Encounter.Regular[_rng.Next(Encounter.Regular.Count)]);
            return roll < 75 ? new MapNode(NodeKind.Shop) : new MapNode(NodeKind.Event);
        }

        /// <summary>Picks a node at the current step and enters it.</summary>
        public void ChooseNode(int index)
        {
            if (State != RunState.Map || index < 0 || index >= CurrentChoices.Count)
                throw new InvalidOperationException("Can't choose a node now.");
            _chosen.Add(index);
            CurrentNode = CurrentChoices[index];
            IsAmbush = false;
            switch (CurrentNode.Kind)
            {
                case NodeKind.Battle:
                case NodeKind.Boss:
                    CurrentEncounter = CurrentNode.Encounter;
                    State = RunState.Battle;
                    break;
                case NodeKind.Shop:
                    State = RunState.Shop;
                    RollOffers();
                    break;
                case NodeKind.Event:
                    State = RunState.Event;
                    RollEvent();
                    break;
            }
        }

        private void NextStep()
        {
            StepIndex++;
            State = RunState.Map;
            CurrentNode = null;
        }

        public bool IsFinalStep => StepIndex == Steps - 1;

        // ---------- Battles ----------

        public Battle StartBattle()
        {
            if (State != RunState.Battle)
                throw new InvalidOperationException($"Can't start a battle while in {State}.");
            CurrentBattle = CurrentEncounter.CreateBattle(Army);
            return CurrentBattle;
        }

        public static int GoldFor(int survivors) => BaseGold + GoldPerSurvivor * survivors;

        /// <summary>Gold a victory in the current battle would pay with this many survivors.</summary>
        public int RewardFor(int survivors) => GoldFor(survivors) + (IsAmbush ? AmbushBonusGold : 0);

        /// <summary>Call once the current battle is over. Returns the gold earned (0 for a defeat).</summary>
        public int FinishBattle()
        {
            var battle = CurrentBattle;
            if (State != RunState.Battle || battle == null || !battle.IsOver)
                throw new InvalidOperationException("The current battle isn't over.");

            // Run-long gains from this battle, such as Bloodthirst, stick to the army member.
            foreach (var unit in battle.Units.Where(u => u.Member != null))
                unit.Member.BonusDamage += unit.GainedDamage;

            if (battle.Phase == Phase.Defeat)
            {
                State = RunState.Lost;
                return 0;
            }

            var earned = RewardFor(battle.Living(Team.Player).Count());
            Gold += earned;
            if (IsFinalStep)
            {
                State = RunState.Won;
                return earned;
            }

            NextStep();
            return earned;
        }

        // ---------- Shop ----------

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

        public bool CanBuyRecruit => State == RunState.Shop && ShopHasRecruit && Gold >= RecruitPrice && Army.Count < MaxArmySize;

        public bool BuyRecruit()
        {
            if (!CanBuyRecruit)
                return false;
            Gold -= RecruitPrice;
            Army.Add(new ArmyMember(UnitStats.Sapper));
            ShopHasRecruit = false;
            return true;
        }

        public void LeaveShop()
        {
            if (State != RunState.Shop)
                return;
            _offers.Clear();
            ShopHasRecruit = false;
            NextStep();
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

            ShopHasRecruit = Army.Count < MaxArmySize;
        }

        // ---------- Events ----------

        private void RollEvent()
        {
            var kinds = (EventKind[])Enum.GetValues(typeof(EventKind));
            CurrentEvent = kinds[_rng.Next(kinds.Length)];
            CartItem = CurrentEvent == EventKind.AbandonedCart ? Equipment.Catalog[_rng.Next(Equipment.Catalog.Count)] : null;
            EventOutcome = null;
        }

        /// <summary>Wandering Sapper: joins the army, or pays gold if the army is full.</summary>
        public void AcceptRecruit()
        {
            if (State != RunState.Event || CurrentEvent != EventKind.WanderingSapper)
                return;
            if (Army.Count < MaxArmySize)
            {
                Army.Add(new ArmyMember(UnitStats.Sapper));
                EventOutcome = "The sapper joins your army.";
            }
            else
            {
                Gold += FullArmyGold;
                EventOutcome = $"Your army is full. The sapper pays you {FullArmyGold} gold for directions.";
            }

            NextStep();
        }

        /// <summary>Abandoned Cart: give the item to a member (replacing its item), or leave it with null.</summary>
        public bool TakeCartItem(ArmyMember member)
        {
            if (State != RunState.Event || CurrentEvent != EventKind.AbandonedCart)
                return false;
            if (member != null)
            {
                if (!CartItem.CanEquip(member.Stats) || !Army.Contains(member))
                    return false;
                member.Equipment = CartItem;
                EventOutcome = $"The {member.Stats.Name} takes the {CartItem.Name}.";
            }
            else
            {
                EventOutcome = "You leave the cart alone.";
            }

            NextStep();
            return true;
        }

        public bool CanGamble => State == RunState.Event && CurrentEvent == EventKind.Gambler && Gold >= GambleCost;

        /// <summary>Gambler: pay to play for a 50% chance at a bigger pot, or walk away. Returns true on a win.</summary>
        public bool Gamble(bool play)
        {
            if (State != RunState.Event || CurrentEvent != EventKind.Gambler)
                return false;
            var won = false;
            if (play && Gold >= GambleCost)
            {
                Gold -= GambleCost;
                won = _rng.Next(2) == 0;
                if (won)
                    Gold += GambleWin;
                EventOutcome = won ? $"You win {GambleWin} gold!" : "You lose.";
            }
            else
            {
                EventOutcome = "You walk away.";
            }

            NextStep();
            return won;
        }

        /// <summary>Ambush: straight into a wolf fight that pays extra.</summary>
        public void StartAmbush()
        {
            if (State != RunState.Event || CurrentEvent != EventKind.Ambush)
                return;
            CurrentEncounter = Encounter.Wolves;
            IsAmbush = true;
            EventOutcome = "Ambush!";
            State = RunState.Battle;
        }

        /// <summary>Shrine: one member gets +2 max HP for the rest of the run.</summary>
        public bool PrayAtShrine(ArmyMember member)
        {
            if (State != RunState.Event || CurrentEvent != EventKind.Shrine || !Army.Contains(member))
                return false;
            member.BonusMaxHp += ShrineHp;
            EventOutcome = $"The {member.Stats.Name} feels sturdier (+{ShrineHp} max HP).";
            NextStep();
            return true;
        }
    }
}
