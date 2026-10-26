using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    /// <summary>A unit in the player's army between battles: its type, its item and run-long bonuses.</summary>
    public sealed class ArmyMember
    {
        /// <summary>The unit's current type. Changes when it evolves at a Trainer.</summary>
        public UnitStats Stats { get; internal set; }

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

        /// <summary>A default army (warrior, archer, rogue) for tests and tools.</summary>
        public static List<ArmyMember> StartingArmy() => new List<ArmyMember>
        {
            new ArmyMember(UnitStats.Warrior),
            new ArmyMember(UnitStats.Archer),
            new ArmyMember(UnitStats.Rogue),
        };
    }

    public enum NodeKind
    {
        Battle,
        Shop,
        Event,
        Trainer,
        Boss,

        /// <summary>The Moon Shrine: defend it for a blessing; lose it and take a curse.</summary>
        Ritual,
    }

    public enum EventKind
    {
        /// <summary>A random base unit offers to join for free (or pays gold if the army is full).</summary>
        WanderingRecruit,

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
                    case NodeKind.Trainer: return "Trainer";
                    case NodeKind.Ritual: return Encounter.Name;
                    default: return "?";
                }
            }
        }
    }

    public enum RunState
    {
        /// <summary>Picking the starting army.</summary>
        Draft,

        /// <summary>Choosing the next node on the map.</summary>
        Map,
        Battle,
        Shop,
        Event,
        Trainer,

        /// <summary>The Moon Shrine held: picking a blessing.</summary>
        Blessing,
        Won,
        Lost,
    }

    /// <summary>
    /// A run: draft a starting army, then cross a branching map where each step offers two nodes
    /// (battle, shop, ? event or trainer); the last step is one of the bosses. Gold depends on how many
    /// units survived. Every battle starts with the whole army at full HP; evolutions, items and
    /// run-long bonuses carry over.
    /// </summary>
    public sealed class Run
    {
        public const int BaseGold = 10;
        public const int GoldPerSurvivor = 5;
        public const int ShopSize = 3;
        public const int DraftSize = 3;
        public const int MaxArmySize = 4;
        public const int RecruitPrice = 15;
        public const int Steps = 7;
        public const int GambleCost = 10;
        public const int GambleWin = 25;
        public const int AmbushBonusGold = 5;
        public const int FullArmyGold = 15;
        public const int ShrineHp = 2;

        /// <summary>Map steps (0-based) that always offer a Trainer as one of the two choices.</summary>
        public static readonly IReadOnlyList<int> TrainerSteps = new[] { 3, 5 };

        private readonly Random _rng;
        private readonly List<Equipment> _offers = new List<Equipment>();
        private readonly List<IReadOnlyList<MapNode>> _map = new List<IReadOnlyList<MapNode>>();
        private readonly List<int> _chosen = new List<int>();
        private readonly HashSet<ArmyMember> _evolvedThisVisit = new HashSet<ArmyMember>();

        public readonly int Seed;
        public readonly List<ArmyMember> Army = new List<ArmyMember>();

        public int Gold { get; private set; }

        /// <summary>The map step the player is on or choosing from (0-based).</summary>
        public int StepIndex { get; private set; }

        public RunState State { get; private set; } = RunState.Draft;
        public Battle CurrentBattle { get; private set; }
        public Encounter CurrentEncounter { get; private set; }
        public MapNode CurrentNode { get; private set; }

        /// <summary>The current battle came from an ambush event, which pays extra gold.</summary>
        public bool IsAmbush { get; private set; }

        public EventKind CurrentEvent { get; private set; }

        /// <summary>The item an Abandoned Cart event offers.</summary>
        public Equipment CartItem { get; private set; }

        /// <summary>The base unit a Wandering Recruit event offers.</summary>
        public UnitStats EventRecruit { get; private set; }

        /// <summary>The base unit for sale in the current shop, or null.</summary>
        public UnitStats ShopRecruit { get; private set; }

        /// <summary>Text describing how the last event turned out, for the screen.</summary>
        public string EventOutcome { get; private set; }

        public IReadOnlyList<Equipment> Offers => _offers;
        public IReadOnlyList<IReadOnlyList<MapNode>> Map => _map;

        /// <summary>Index of the node chosen at each past step.</summary>
        public IReadOnlyList<int> Chosen => _chosen;

        public bool ShopHasRecruit => ShopRecruit != null;

        public Run(int seed)
        {
            Seed = seed;
            _rng = new Random(seed);
            GenerateMap();
        }

        public IReadOnlyList<MapNode> CurrentChoices => _map[StepIndex];

        // ---------- Draft ----------

        public IReadOnlyList<UnitStats> DraftPool => UnitStats.BaseUnits;

        /// <summary>Starts the run with the chosen base units (3 different ones from the pool).</summary>
        public bool Draft(IReadOnlyList<UnitStats> picks)
        {
            if (State != RunState.Draft || picks.Count != DraftSize || picks.Distinct().Count() != DraftSize ||
                picks.Any(p => !DraftPool.Contains(p)))
                return false;
            Army.AddRange(picks.Select(p => new ArmyMember(p)));
            State = RunState.Map;
            return true;
        }

        public bool Draft(params UnitStats[] picks) => Draft((IReadOnlyList<UnitStats>)picks);

        // ---------- Map ----------

        /// <summary>
        /// Act 1, 7 steps. The four factions are shuffled and each appears on exactly one battle node, so
        /// no faction can come up twice: step 1 offers two of them, steps 3 and 5 one each. The Moon Shrine
        /// is the other choice at step 3 or 5. Shops, ? events and Trainers (steps 4 and 6) fill the rest,
        /// and step 7 is one of the two bosses.
        /// </summary>
        private void GenerateMap()
        {
            var factions = Encounter.Regular.OrderBy(_ => _rng.Next()).ToList();
            MapNode Fight(int i) => new MapNode(NodeKind.Battle, factions[i]);
            var shop = new MapNode(NodeKind.Shop);
            var unknown = new MapNode(NodeKind.Event);
            var trainer = new MapNode(NodeKind.Trainer);
            var ritual = new MapNode(NodeKind.Ritual, Encounter.MoonShrine);
            var ritualAtStep3 = _rng.Next(2) == 0;

            AddStep(Fight(0), Fight(1));
            AddStep(shop, unknown);
            AddStep(Fight(2), ritualAtStep3 ? ritual : unknown);
            AddStep(trainer, shop);
            AddStep(Fight(3), ritualAtStep3 ? unknown : ritual);
            AddStep(trainer, unknown);
            _map.Add(new[] { new MapNode(NodeKind.Boss, Encounter.Bosses[_rng.Next(Encounter.Bosses.Count)]) });
        }

        private void AddStep(MapNode a, MapNode b) => _map.Add(new[] { a, b }.OrderBy(_ => _rng.Next()).ToList());

        /// <summary>Factions beaten so far this act, in order. The Necromancer raises his army from them.</summary>
        public List<Encounter> Fought { get; } = new List<Encounter>();

        /// <summary>Factions not yet fought that don't appear on any map step still ahead (safe for an ambush).</summary>
        private List<Encounter> FreshFactions()
        {
            var ahead = _map.Skip(StepIndex + 1).SelectMany(s => s).Select(n => n.Encounter).Where(e => e != null);
            return Encounter.Regular.Where(e => !Fought.Contains(e) && !ahead.Contains(e)).ToList();
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
                case NodeKind.Ritual:
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
                case NodeKind.Trainer:
                    State = RunState.Trainer;
                    _evolvedThisVisit.Clear();
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
            CurrentBattle = CurrentEncounter.CreateBattle(Army, _rng, Fought);
            if (Blessing.HasValue)
                CurrentBattle.AddOmen(Blessing.Value, Team.Player);
            if (Curse.HasValue)
                CurrentBattle.AddOmen(Curse.Value, Team.Enemy);
            return CurrentBattle;
        }

        // ---------- Moon Shrine ----------

        /// <summary>The blessing chosen after holding the Moon Shrine, in play for every later battle.</summary>
        public Omen? Blessing { get; private set; }

        /// <summary>The curse taken after losing the Moon Shrine, in play for every later battle.</summary>
        public Omen? Curse { get; private set; }

        /// <summary>The blessings offered after holding the shrine (all of them, for now).</summary>
        public IReadOnlyList<Omen> BlessingChoices => Omens.All;

        /// <summary>Picks the blessing after holding the Moon Shrine and moves on.</summary>
        public bool ChooseBlessing(Omen omen)
        {
            if (State != RunState.Blessing || !BlessingChoices.Contains(omen))
                return false;
            Blessing = omen;
            NextStep();
            return true;
        }

        public static int GoldFor(int survivors) => BaseGold + GoldPerSurvivor * survivors;

        /// <summary>Gold lost to escaped cutpurses in the last battle.</summary>
        public int LastStolen { get; private set; }

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
                // Losing the Moon Shrine with the army still standing isn't the end: it costs a random curse.
                if (battle.ObjectiveLost)
                {
                    Curse = Omens.All[_rng.Next(Omens.All.Count)];
                    NextStep();
                    return 0;
                }

                State = RunState.Lost;
                return 0;
            }

            // Gold carried off by cutpurses that got away is lost first.
            LastStolen = Math.Min(Gold, battle.StolenGold);
            Gold -= LastStolen;
            var earned = RewardFor(battle.LivingArmy.Count());
            Gold += earned;
            if (Encounter.Regular.Contains(CurrentEncounter) && !Fought.Contains(CurrentEncounter))
                Fought.Add(CurrentEncounter);
            if (CurrentEncounter.IsRitual)
            {
                State = RunState.Blessing;
                return earned;
            }
            if (IsFinalStep)
            {
                State = RunState.Won;
                return earned;
            }

            NextStep();
            return earned;
        }

        // ---------- Trainer ----------

        /// <summary>Price of the next evolution this visit: the first is free, then 20, then 35 each.</summary>
        public const int SecondEvolutionPrice = 20;

        public const int LaterEvolutionPrice = 35;

        public int NextEvolutionPrice =>
            _evolvedThisVisit.Count == 0 ? 0 : _evolvedThisVisit.Count == 1 ? SecondEvolutionPrice : LaterEvolutionPrice;

        /// <summary>
        /// Each unit can go up one tier per Trainer visit, into one of its listed evolutions. The first
        /// evolution of a visit is free; more cost gold.
        /// </summary>
        public bool CanEvolve(ArmyMember member, UnitStats into) =>
            State == RunState.Trainer && Army.Contains(member) && !_evolvedThisVisit.Contains(member) &&
            member.Stats.Evolutions.Contains(into) && Gold >= NextEvolutionPrice;

        public bool HasEvolvedThisVisit(ArmyMember member) => _evolvedThisVisit.Contains(member);

        public bool Evolve(ArmyMember member, UnitStats into)
        {
            if (!CanEvolve(member, into))
                return false;
            Gold -= NextEvolutionPrice;
            member.Stats = into;
            _evolvedThisVisit.Add(member);
            return true;
        }

        public void LeaveTrainer()
        {
            if (State != RunState.Trainer)
                return;
            _evolvedThisVisit.Clear();
            NextStep();
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

        public bool CanBuyRecruit => State == RunState.Shop && ShopRecruit != null && Gold >= RecruitPrice && Army.Count < MaxArmySize;

        public bool BuyRecruit()
        {
            if (!CanBuyRecruit)
                return false;
            Gold -= RecruitPrice;
            Army.Add(new ArmyMember(ShopRecruit));
            ShopRecruit = null;
            return true;
        }

        public void LeaveShop()
        {
            if (State != RunState.Shop)
                return;
            _offers.Clear();
            ShopRecruit = null;
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

            ShopRecruit = Army.Count < MaxArmySize ? UnitStats.BaseUnits[_rng.Next(UnitStats.BaseUnits.Count)] : null;
        }

        // ---------- Events ----------

        private void RollEvent()
        {
            var kinds = (EventKind[])Enum.GetValues(typeof(EventKind));
            CurrentEvent = kinds[_rng.Next(kinds.Length)];
            CartItem = CurrentEvent == EventKind.AbandonedCart ? Equipment.Catalog[_rng.Next(Equipment.Catalog.Count)] : null;
            EventRecruit = CurrentEvent == EventKind.WanderingRecruit ? UnitStats.BaseUnits[_rng.Next(UnitStats.BaseUnits.Count)] : null;
            var fresh = FreshFactions();
            AmbushEncounter = CurrentEvent == EventKind.Ambush && fresh.Count > 0 ? fresh[_rng.Next(fresh.Count)] : null;
            EventOutcome = null;
        }

        /// <summary>Wandering Recruit: joins the army, or pays gold if the army is full.</summary>
        public void AcceptRecruit()
        {
            if (State != RunState.Event || CurrentEvent != EventKind.WanderingRecruit)
                return;
            if (Army.Count < MaxArmySize)
            {
                Army.Add(new ArmyMember(EventRecruit));
                EventOutcome = $"The {EventRecruit.Name} joins your army.";
            }
            else
            {
                Gold += FullArmyGold;
                EventOutcome = $"Your army is full. The {EventRecruit.Name} pays you {FullArmyGold} gold for directions.";
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
            if (AmbushEncounter == null)
            {
                // Every faction is already used up this act: the ambushers think better of it.
                Gold += AmbushBonusGold * 2;
                EventOutcome = $"The ambushers see your army and flee, dropping {AmbushBonusGold * 2} gold.";
                NextStep();
                return;
            }

            CurrentEncounter = AmbushEncounter;
            IsAmbush = true;
            EventOutcome = "Ambush!";
            State = RunState.Battle;
        }

        /// <summary>
        /// Who ambushes you: a faction you haven't fought that isn't waiting further down the map,
        /// so it never repeats. Null if there is none left.
        /// </summary>
        public Encounter AmbushEncounter { get; private set; }

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
