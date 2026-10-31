using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    /// <summary>A unit in the player's army between battles: its type, its item and run-long bonuses.</summary>
    public sealed class ArmyMember
    {
        /// <summary>The unit's type: one of the base units.</summary>
        public UnitStats Stats { get; internal set; }

        public Equipment Equipment { get; internal set; }

        /// <summary>Damage gained during the run from sources other than levels (none for now).</summary>
        public int BonusDamage { get; internal set; }

        /// <summary>Max HP gained during the run, e.g. from a shrine.</summary>
        public int BonusMaxHp { get; internal set; }

        private readonly List<Technique> _techniques = new List<Technique>();

        /// <summary>The techniques it knows, in the order learned.</summary>
        public IReadOnlyList<Technique> Techniques => _techniques;

        /// <summary>One per technique ever learned; forgetting one keeps the level.</summary>
        public int Level { get; private set; }

        public bool Knows(Technique technique) => _techniques.Contains(technique);

        /// <summary>Learns a technique and goes up a level. False if it can't learn it (line, requirement, known).</summary>
        public bool Learn(Technique technique)
        {
            if (!technique.CanLearn(this))
                return false;
            _techniques.Add(technique);
            Level++;
            return true;
        }

        /// <summary>Forgets a technique; the level stays. Techniques that needed it are forgotten too.</summary>
        public bool Forget(Technique technique)
        {
            if (!_techniques.Remove(technique))
                return false;
            foreach (var dependent in _techniques.Where(t => t.Requires == technique).ToList())
                _techniques.Remove(dependent);
            return true;
        }

        /// <summary>Max HP from levels.</summary>
        public int LevelHp => Growth.Hp(Stats, Level);

        /// <summary>Damage from levels.</summary>
        public int LevelDamage => Growth.Damage(Stats, Level);

        /// <summary>Extra mines on the board from levels.</summary>
        public int LevelMines => Growth.Mines(Stats, Level);

        public bool HasTechniqueAbility(Ability ability) => _techniques.Any(t => t.Ability == ability);

        public IEnumerable<ActionKind> TechniqueActions => _techniques.Where(t => t.Action.HasValue).Select(t => t.Action.Value);

        public ArmyMember(UnitStats stats, Equipment equipment = null, params Technique[] techniques)
        {
            Stats = stats;
            Equipment = equipment;
            foreach (var t in techniques)
                Learn(t);
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
        Boss,

        /// <summary>A scenario fight (Moon Shrine or Break the Ritual): win for a reward; lose it and take a curse.</summary>
        Ritual,

        /// <summary>A harder rematch of a faction you've beaten: high risk; its technique pick has a rare one.</summary>
        Elite,
    }

    public enum EventKind
    {
        /// <summary>A random base unit offers to join for free (or pays gold if the army is full).</summary>
        WanderingRecruit,

        /// <summary>A random free technique or item for one unit.</summary>
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

        /// <summary>
        /// For fight nodes. Battle and elite nodes get theirs when the run reaches their step, because it
        /// depends on which factions have been beaten by then; until then it is null.
        /// </summary>
        public Encounter Encounter { get; internal set; }

        /// <summary>A battle against a faction already beaten (every faction has been met), at normal strength.</summary>
        public bool IsRematch { get; internal set; }

        /// <summary>The fight's battlefield rule, known from the start of the run. Null for scenario fights and other nodes.</summary>
        public Battlefield? Field { get; internal set; }

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
                    case NodeKind.Battle:
                        return Encounter == null ? "Battle" : $"Battle: {Encounter.Name}{(IsRematch ? " (rematch)" : "")}";
                    case NodeKind.Elite:
                        return Encounter == null ? "Elite" : $"Elite: {Encounter.Base.Name}";
                    case NodeKind.Boss: return $"Boss: {Encounter.Name}";
                    case NodeKind.Shop: return "Shop";
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

        /// <summary>The Moon Shrine held: picking a blessing.</summary>
        Blessing,

        /// <summary>A fight won: picking a technique for one unit, or skipping it for gold.</summary>
        Reward,

        /// <summary>The army is over its limit (Third Wheel just taken): choosing who stays.</summary>
        Dismiss,
        Won,
        Lost,
    }

    /// <summary>
    /// A run: draft a starting army, then cross a branching map where each step offers two nodes
    /// (battle, shop or ? event); the last step is one of the bosses. Gold depends on how many units
    /// survived, and every won fight offers a technique pick. Every battle starts with the whole army at
    /// full HP; techniques, levels and run-long bonuses carry over.
    /// </summary>
    public sealed class Run
    {
        public const int BaseGold = 10;
        public const int GoldPerSurvivor = 5;
        /// <summary>Techniques for sale in a shop.</summary>
        public const int ShopSize = 3;

        /// <summary>Items for sale in a shop, next to the techniques.</summary>
        public const int ShopItemCount = 3;
        public const int DraftSize = 3;
        /// <summary>Most units in the army without banners; see <see cref="ArmyLimit"/>.</summary>
        public const int MaxArmySize = 4;
        public const int RecruitPrice = 15;
        public const int Steps = 12;
        public const int GambleCost = 10;
        public const int GambleWin = 25;
        public const int AmbushBonusGold = 5;
        public const int FullArmyGold = 15;
        public const int ShrineHp = 2;

        /// <summary>Techniques offered after a won fight.</summary>
        public const int RewardCount = 3;

        /// <summary>Gold for skipping a technique pick.</summary>
        public const int SkipGold = 8;

        /// <summary>The shop's fee to make a unit forget a technique (its level stays).</summary>
        public const int ForgetPrice = 8;

        /// <summary>Map steps (0-based) where both choices are fights, so every run has at least this many before the boss.</summary>
        public static readonly IReadOnlyList<int> FightSteps = new[] { 0, 3, 5, 7, 9 };

        private readonly Random _rng;
        private readonly List<Technique> _offers = new List<Technique>();
        private readonly List<Equipment> _itemOffers = new List<Equipment>();
        private readonly List<IReadOnlyList<MapNode>> _map = new List<IReadOnlyList<MapNode>>();
        private readonly List<int> _chosen = new List<int>();

        public readonly int Seed;
        public readonly List<ArmyMember> Army = new List<ArmyMember>();

        public int Gold { get; internal set; }

        /// <summary>The map step the player is on or choosing from (0-based).</summary>
        public int StepIndex { get; private set; }

        public RunState State { get; private set; } = RunState.Draft;
        public Battle CurrentBattle { get; private set; }
        public Encounter CurrentEncounter { get; private set; }
        public MapNode CurrentNode { get; private set; }

        /// <summary>The current battle came from an ambush event, which pays extra gold.</summary>
        public bool IsAmbush { get; private set; }

        public EventKind CurrentEvent { get; private set; }

        /// <summary>The technique an Abandoned Cart event offers, or null when it holds an item.</summary>
        public Technique CartTechnique { get; private set; }

        /// <summary>The item an Abandoned Cart event offers, or null when it holds a technique.</summary>
        public Equipment CartItem { get; private set; }

        /// <summary>The base unit a Wandering Recruit event offers.</summary>
        public UnitStats EventRecruit { get; private set; }

        /// <summary>The base unit for sale in the current shop, or null.</summary>
        public UnitStats ShopRecruit { get; private set; }

        /// <summary>Text describing how the last event turned out, for the screen.</summary>
        public string EventOutcome { get; private set; }

        /// <summary>The techniques for sale in the current shop.</summary>
        public IReadOnlyList<Technique> Offers => _offers;

        /// <summary>The items for sale in the current shop.</summary>
        public IReadOnlyList<Equipment> ItemOffers => _itemOffers;
        public IReadOnlyList<IReadOnlyList<MapNode>> Map => _map;

        /// <summary>Index of the node chosen at each past step.</summary>
        public IReadOnlyList<int> Chosen => _chosen;

        public bool ShopHasRecruit => ShopRecruit != null;

        public Run(int seed)
        {
            Seed = seed;
            _rng = new Random(seed);
            GenerateMap();
            ResolveStep();
            // Its own random source, so the map and events a seed gives stay the same.
            var bannerRng = new Random(seed ^ 0x5bd1e995);
            _startBanners.AddRange(Core.Banners.All.OrderBy(_ => bannerRng.Next()).Take(Core.Banners.StartChoices));
            _fieldRng = new Random(seed ^ 0x2545f491);
            AssignFields();
        }

        // ---------- Battlefields ----------

        // Its own random source too, so adding battlefields didn't change the map a seed gives.
        private readonly Random _fieldRng;

        /// <summary>The battlefield of the fight being entered or fought, or null.</summary>
        public Battlefield? CurrentField { get; private set; }

        /// <summary>
        /// Every battle, elite and boss node gets a random battlefield (scenario fights have their own rules). A step's
        /// fights never repeat a battlefield from the step before, nor share one.
        /// </summary>
        private void AssignFields()
        {
            var last = new List<Battlefield>();
            foreach (var step in _map)
            {
                var used = new List<Battlefield>();
                foreach (var node in step.Where(n => n.Kind == NodeKind.Battle || n.Kind == NodeKind.Elite || n.Kind == NodeKind.Boss))
                {
                    var pool = Battlefields.All.Where(f => !last.Contains(f) && !used.Contains(f)).ToList();
                    node.Field = pool[_fieldRng.Next(pool.Count)];
                    used.Add(node.Field.Value);
                }

                if (used.Count > 0)
                    last = used;
            }
        }

        // ---------- Banners ----------

        private readonly List<Banner> _banners = new List<Banner>();
        private readonly List<Banner> _startBanners = new List<Banner>();

        /// <summary>The army's banners, in the order taken.</summary>
        public IReadOnlyList<Banner> Banners => _banners;

        public bool HasBanner(Banner banner) => _banners.Contains(banner);

        /// <summary>The banners offered at the start of the run, before the draft.</summary>
        public IReadOnlyList<Banner> StartBanners => _startBanners;

        /// <summary>The starting banner has been picked (or there was nothing to pick).</summary>
        public bool StartBannerChosen => _banners.Count > 0 || _startBanners.Count == 0;

        /// <summary>Most units the army can hold: 4, or 2 with Third Wheel.</summary>
        public int ArmyLimit => HasBanner(Banner.ThirdWheel) ? Core.Banners.ThirdWheelArmySize : MaxArmySize;

        /// <summary>Units picked in the draft: 3, or fewer if the army limit is lower.</summary>
        public int DraftCount => Math.Min(DraftSize, ArmyLimit);

        /// <summary>Picks the starting banner, before the draft.</summary>
        public bool ChooseStartBanner(Banner banner)
        {
            if (State != RunState.Draft || StartBannerChosen || !_startBanners.Contains(banner))
                return false;
            _banners.Add(banner);
            return true;
        }

        /// <summary>Banners the army doesn't hold yet, in random order.</summary>
        private List<Banner> UnownedBanners() => Core.Banners.All.Where(b => !HasBanner(b)).OrderBy(_ => _rng.Next()).ToList();

        // Where the run goes after the army is cut down to its limit.
        private RunState _afterDismiss;

        /// <summary>
        /// Takes a banner mid-run. If the army is now over its limit (Third Wheel), the run waits in
        /// <see cref="RunState.Dismiss"/> for the player to choose who stays, then goes on to <paramref name="next"/>.
        /// </summary>
        private void GainBanner(Banner banner, RunState next)
        {
            _banners.Add(banner);
            if (Army.Count > ArmyLimit)
            {
                _afterDismiss = next;
                State = RunState.Dismiss;
            }
            else
            {
                State = next;
            }
        }

        /// <summary>Keeps exactly <see cref="ArmyLimit"/> members of the army; the rest leave.</summary>
        public bool Dismiss(IReadOnlyList<ArmyMember> keep)
        {
            if (State != RunState.Dismiss || keep.Count != ArmyLimit || keep.Distinct().Count() != keep.Count || keep.Any(m => !Army.Contains(m)))
                return false;
            Army.RemoveAll(m => !keep.Contains(m));
            State = _afterDismiss;
            return true;
        }

        // ---------- Mercenaries ----------

        private readonly List<UnitStats> _mercOffers = new List<UnitStats>();

        /// <summary>Base units for hire before the coming fight (Mercenaries banner). Empty once decided.</summary>
        public IReadOnlyList<UnitStats> MercOffers => _mercOffers;

        /// <summary>A fight is about to start and the hire hasn't been decided yet.</summary>
        public bool MercPending => State == RunState.Battle && CurrentBattle == null && _mercOffers.Count > 0;

        /// <summary>The mercenary hired for the coming or current fight, or null.</summary>
        public ArmyMember Merc { get; private set; }

        public bool CanHire(UnitStats unit) => MercPending && _mercOffers.Contains(unit) && Gold >= Core.Banners.MercPrice;

        /// <summary>Hires one of the offered units for the coming fight.</summary>
        public bool HireMerc(UnitStats unit)
        {
            if (!CanHire(unit))
                return false;
            Gold -= Core.Banners.MercPrice;
            Merc = new ArmyMember(unit);
            _mercOffers.Clear();
            return true;
        }

        /// <summary>Fights without a mercenary.</summary>
        public void DeclineMerc() => _mercOffers.Clear();

        /// <summary>Entering a fight: with Mercenaries, rolls who is for hire.</summary>
        private void EnterBattle()
        {
            State = RunState.Battle;
            CurrentBattle = null;
            Merc = null;
            _mercOffers.Clear();
            if (HasBanner(Banner.Mercenaries))
                _mercOffers.AddRange(UnitStats.BaseUnits.OrderBy(_ => _rng.Next()).Take(Core.Banners.MercOffers));
        }

        public IReadOnlyList<MapNode> CurrentChoices => _map[StepIndex];

        // ---------- Draft ----------

        public IReadOnlyList<UnitStats> DraftPool => UnitStats.BaseUnits;

        /// <summary>Starts the run with the chosen base units (<see cref="DraftCount"/> different ones from the pool).</summary>
        public bool Draft(IReadOnlyList<UnitStats> picks)
        {
            if (State != RunState.Draft || picks.Count != DraftCount || picks.Distinct().Count() != DraftCount ||
                picks.Any(p => !DraftPool.Contains(p)))
                return false;
            Army.AddRange(picks.Select(p => new ArmyMember(p)));
            State = RunState.Map;
            return true;
        }

        public bool Draft(params UnitStats[] picks) => Draft((IReadOnlyList<UnitStats>)picks);

        // ---------- Map ----------

        /// <summary>
        /// Act 1, 12 steps. Steps 1, 4, 6, 8 and 10 offer two fights, so every run has at least five before the
        /// boss. A battle brings a faction you haven't beaten yet (a normal-strength rematch once you've met them
        /// all); an elite is a harder rematch of one you have beaten. Battle and elite nodes are filled in when
        /// the run reaches their step (see <see cref="ResolveStep"/>). The two scenario fights sit at steps 4
        /// and 8 in random order; step 9 offers one more optional battle; shops and ? events fill the rest;
        /// step 12 is a boss.
        /// </summary>
        private void GenerateMap()
        {
            MapNode Battle() => new MapNode(NodeKind.Battle);
            MapNode Elite() => new MapNode(NodeKind.Elite);
            var shop = new MapNode(NodeKind.Shop);
            var unknown = new MapNode(NodeKind.Event);
            var scenarios = new[] { Encounter.MoonShrine, Encounter.BreakRitual }.OrderBy(_ => _rng.Next())
                .Select(e => new MapNode(NodeKind.Ritual, e)).ToList();

            AddStep(Battle(), Battle());
            AddStep(shop, unknown);
            AddStep(Battle(), unknown);
            AddStep(scenarios[0], Battle());
            AddStep(shop, unknown);
            AddStep(Elite(), Battle());
            AddStep(shop, unknown);
            AddStep(scenarios[1], Battle());
            AddStep(unknown, Battle());
            AddStep(Elite(), Battle());
            AddStep(shop, unknown);
            _map.Add(new[] { new MapNode(NodeKind.Boss, Encounter.Bosses[_rng.Next(Encounter.Bosses.Count)]) });
        }

        /// <summary>
        /// Picks the factions for the current step's battle and elite nodes, now that it's known who has been
        /// beaten. A battle takes a faction not beaten yet (a normal-strength rematch if every faction has been
        /// met); an elite takes a beaten one. The two nodes of a step never share a faction.
        /// </summary>
        private void ResolveStep()
        {
            if (StepIndex >= _map.Count)
                return;
            var taken = _map[StepIndex].Where(n => n.Encounter != null).Select(n => n.Encounter.Base ?? n.Encounter).ToList();
            foreach (var node in _map[StepIndex].Where(n => n.Encounter == null))
            {
                Encounter faction;
                if (node.Kind == NodeKind.Elite)
                {
                    var beaten = Fought.Where(f => !taken.Contains(f)).ToList();
                    var pool = beaten.Count > 0 ? beaten : Encounter.Regular.Where(f => !taken.Contains(f)).ToList();
                    faction = pool[_rng.Next(pool.Count)];
                    node.Encounter = Encounter.EliteOf(faction);
                }
                else if (node.Kind == NodeKind.Battle)
                {
                    var fresh = Encounter.Regular.Where(f => !Fought.Contains(f) && !taken.Contains(f)).ToList();
                    node.IsRematch = fresh.Count == 0;
                    var pool = fresh.Count > 0 ? fresh : Encounter.Regular.Where(f => !taken.Contains(f)).ToList();
                    faction = pool[_rng.Next(pool.Count)];
                    node.Encounter = faction;
                }
                else
                {
                    continue;
                }

                taken.Add(faction);
            }
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
                case NodeKind.Elite:
                    CurrentEncounter = CurrentNode.Encounter;
                    CurrentField = CurrentNode.Field;
                    EnterBattle();
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
            ResolveStep();
        }

        public bool IsFinalStep => StepIndex == Steps - 1;

        // ---------- Battles ----------

        public Battle StartBattle()
        {
            if (State != RunState.Battle)
                throw new InvalidOperationException($"Can't start a battle while in {State}.");
            // Starting without deciding on a mercenary passes on the hire.
            _mercOffers.Clear();
            var fighters = Merc != null ? Army.Concat(new[] { Merc }).ToList() : Army;
            CurrentBattle = CurrentEncounter.CreateBattle(fighters, _rng, Fought);
            if (Merc != null)
                CurrentBattle.MarkMercenary(Merc);
            if (Blessing.HasValue)
                CurrentBattle.AddOmen(Blessing.Value, Team.Player);
            foreach (var curse in _curses)
                CurrentBattle.AddOmen(curse, Team.Enemy);
            foreach (var banner in _banners)
                CurrentBattle.AddBanner(banner);
            CurrentBattle.SetField(CurrentField);
            return CurrentBattle;
        }

        // ---------- Scenarios: Moon Shrine and Break the Ritual ----------

        /// <summary>The blessing chosen after holding the Moon Shrine, in play for every later battle.</summary>
        public Omen? Blessing { get; private set; }

        private readonly List<Omen> _curses = new List<Omen>();

        /// <summary>
        /// Curses taken by losing a scenario fight, in play for every later battle. Losing both gives two
        /// different curses.
        /// </summary>
        public IReadOnlyList<Omen> Curses => _curses;

        /// <summary>The curse from the last lost scenario fight, for the screen.</summary>
        public Omen? LastCurse => _curses.Count > 0 ? _curses[_curses.Count - 1] : (Omen?)null;

        private void TakeCurse()
        {
            var pool = Omens.All.Where(o => !_curses.Contains(o)).ToList();
            if (pool.Count > 0)
                _curses.Add(pool[_rng.Next(pool.Count)]);
        }

        // ---------- Technique picks ----------

        /// <summary>After a won fight: the techniques on offer.</summary>
        public IReadOnlyList<Technique> RewardOffers => _rewardOffers;

        private readonly List<Technique> _rewardOffers = new List<Technique>();

        /// <summary>
        /// Up to <paramref name="count"/> different techniques that at least one unit of the army can learn now.
        /// With <paramref name="rare"/>, one of them is rare whenever a rare one is learnable.
        /// </summary>
        private List<Technique> RollTechniques(int count, bool rare)
        {
            var pool = Technique.All.Where(t => Army.Any(t.CanLearn)).OrderBy(_ => _rng.Next()).ToList();
            var picks = pool.Take(count).ToList();
            if (rare && picks.Count > 0 && picks.All(t => t.Rarity != TechniqueRarity.Rare))
            {
                var rares = pool.Where(t => t.Rarity == TechniqueRarity.Rare).ToList();
                if (rares.Count > 0)
                    picks[picks.Count - 1] = rares[_rng.Next(rares.Count)];
            }

            return picks;
        }

        public bool CanTakeReward(Technique technique, ArmyMember member) =>
            State == RunState.Reward && _rewardOffers.Contains(technique) && Army.Contains(member) && technique.CanLearn(member);

        /// <summary>Teaches one of the offered techniques to a member and moves on.</summary>
        public bool TakeReward(Technique technique, ArmyMember member)
        {
            if (!CanTakeReward(technique, member))
                return false;
            member.Learn(technique);
            _rewardOffers.Clear();
            RewardBanner = null;
            NextStep();
            return true;
        }

        /// <summary>Skips the pick for gold and moves on.</summary>
        public void SkipReward()
        {
            if (State != RunState.Reward)
                return;
            Gold += SkipGold;
            _rewardOffers.Clear();
            RewardBanner = null;
            NextStep();
        }

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

            // The mercenary leaves after the fight, win or lose.
            Merc = null;

            // Loser: every army unit that fell grows for the rest of the run.
            _lastLoserGains.Clear();
            if (HasBanner(Banner.Loser))
            {
                foreach (var fallen in battle.Units.Where(u => u.Team == Team.Player && !u.IsAlive && u.Member != null && Army.Contains(u.Member)))
                {
                    fallen.Member.BonusMaxHp += Core.Banners.LoserHp;
                    fallen.Member.BonusDamage += Core.Banners.LoserDamage;
                    _lastLoserGains.Add(fallen.Member);
                }
            }

            if (battle.Phase == Phase.Defeat)
            {
                // Losing a scenario fight with the army still standing isn't the end: it costs a random curse.
                if (battle.ScenarioLost)
                {
                    TakeCurse();
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
            if (CurrentEncounter == Encounter.MoonShrine)
            {
                State = RunState.Blessing;
                return earned;
            }

            if (IsFinalStep)
            {
                State = RunState.Won;
                return earned;
            }

            // Every other won fight offers a technique pick on top of the gold; an elite's has a rare one,
            // and a banner the army doesn't hold yet that can be taken instead.
            _rewardOffers.Clear();
            _rewardOffers.AddRange(RollTechniques(RewardCount, CurrentEncounter.IsElite));
            RewardBanner = CurrentEncounter.IsElite ? UnownedBanners().Cast<Banner?>().FirstOrDefault() : null;
            if (_rewardOffers.Count > 0 || RewardBanner.HasValue)
                State = RunState.Reward;
            else
                NextStep();
            return earned;
        }

        private readonly List<ArmyMember> _lastLoserGains = new List<ArmyMember>();

        /// <summary>Army members that fell in the last battle and grew from the Loser banner.</summary>
        public IReadOnlyList<ArmyMember> LastLoserGains => _lastLoserGains;

        /// <summary>After an elite: a banner that can be taken instead of a technique, or null.</summary>
        public Banner? RewardBanner { get; internal set; }

        /// <summary>Takes the elite's banner instead of a technique and moves on (via choosing who stays, if needed).</summary>
        public bool TakeRewardBanner()
        {
            if (State != RunState.Reward || !RewardBanner.HasValue)
                return false;
            var banner = RewardBanner.Value;
            _rewardOffers.Clear();
            RewardBanner = null;
            NextStep();
            GainBanner(banner, RunState.Map);
            return true;
        }

        // ---------- Shop ----------

        public bool CanBuy(Technique technique, ArmyMember member) =>
            State == RunState.Shop && _offers.Contains(technique) && Army.Contains(member) &&
            Gold >= technique.Price && technique.CanLearn(member);

        /// <summary>Buys an offered technique for a member, who learns it (and goes up a level).</summary>
        public bool Buy(Technique technique, ArmyMember member)
        {
            if (!CanBuy(technique, member))
                return false;
            Gold -= technique.Price;
            member.Learn(technique);
            _offers.Remove(technique);
            return true;
        }

        public bool CanForget(ArmyMember member, Technique technique) =>
            State == RunState.Shop && Army.Contains(member) && member.Knows(technique) && Gold >= ForgetPrice;

        /// <summary>Pays the fee to make a member forget a technique (and any that needed it). Its level stays.</summary>
        public bool Forget(ArmyMember member, Technique technique)
        {
            if (!CanForget(member, technique))
                return false;
            Gold -= ForgetPrice;
            member.Forget(technique);
            return true;
        }

        public bool CanBuyItem(Equipment item, ArmyMember member) =>
            State == RunState.Shop && _itemOffers.Contains(item) && Army.Contains(member) &&
            Gold >= item.Price && item.CanEquip(member.Stats);

        /// <summary>Buys an item and puts it in the member's slot, replacing (and losing) its old item.</summary>
        public bool BuyItem(Equipment item, ArmyMember member)
        {
            if (!CanBuyItem(item, member))
                return false;
            Gold -= item.Price;
            member.Equipment = item;
            _itemOffers.Remove(item);
            return true;
        }

        public bool CanBuyRecruit => State == RunState.Shop && ShopRecruit != null && Gold >= RecruitPrice && Army.Count < ArmyLimit;

        public bool BuyRecruit()
        {
            if (!CanBuyRecruit)
                return false;
            Gold -= RecruitPrice;
            Army.Add(new ArmyMember(ShopRecruit));
            ShopRecruit = null;
            return true;
        }

        /// <summary>The banner for sale in the current shop (one the army doesn't hold), or null.</summary>
        public Banner? ShopBanner { get; internal set; }

        public bool CanBuyBanner => State == RunState.Shop && ShopBanner.HasValue && Gold >= Core.Banners.Price;

        /// <summary>Buys the shop's banner. With Third Wheel and too many units, choosing who stays comes first.</summary>
        public bool BuyBanner()
        {
            if (!CanBuyBanner)
                return false;
            Gold -= Core.Banners.Price;
            var banner = ShopBanner.Value;
            ShopBanner = null;
            GainBanner(banner, RunState.Shop);
            if (Army.Count >= ArmyLimit)
                ShopRecruit = null;
            return true;
        }

        public void LeaveShop()
        {
            if (State != RunState.Shop)
                return;
            _offers.Clear();
            _itemOffers.Clear();
            ShopRecruit = null;
            ShopBanner = null;
            NextStep();
        }

        private void RollOffers()
        {
            _offers.Clear();
            _offers.AddRange(RollTechniques(ShopSize, false));
            _itemOffers.Clear();
            _itemOffers.AddRange(Equipment.Catalog.OrderBy(_ => _rng.Next()).Take(ShopItemCount));
            ShopRecruit = Army.Count < ArmyLimit ? UnitStats.BaseUnits[_rng.Next(UnitStats.BaseUnits.Count)] : null;
            ShopBanner = UnownedBanners().Cast<Banner?>().FirstOrDefault();
        }

        // ---------- Events ----------

        private void RollEvent()
        {
            var kinds = (EventKind[])Enum.GetValues(typeof(EventKind));
            CurrentEvent = kinds[_rng.Next(kinds.Length)];
            CartTechnique = null;
            CartItem = null;
            if (CurrentEvent == EventKind.AbandonedCart)
            {
                // Half the time a technique (if any unit can still learn one), otherwise an item.
                var technique = _rng.Next(2) == 0 ? RollTechniques(1, false).FirstOrDefault() : null;
                if (technique != null)
                    CartTechnique = technique;
                else
                    CartItem = Equipment.Catalog[_rng.Next(Equipment.Catalog.Count)];
            }

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
            if (Army.Count < ArmyLimit)
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

        /// <summary>
        /// Abandoned Cart: a member learns its technique or takes its item (replacing its old item), or leave it
        /// with null.
        /// </summary>
        public bool TakeCart(ArmyMember member)
        {
            if (State != RunState.Event || CurrentEvent != EventKind.AbandonedCart)
                return false;
            if (member != null)
            {
                if (!Army.Contains(member))
                    return false;
                if (CartTechnique != null)
                {
                    if (!member.Learn(CartTechnique))
                        return false;
                    EventOutcome = $"The {member.Stats.Name} learns {CartTechnique.Name}.";
                }
                else
                {
                    if (!CartItem.CanEquip(member.Stats))
                        return false;
                    member.Equipment = CartItem;
                    EventOutcome = $"The {member.Stats.Name} takes the {CartItem.Name}.";
                }
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
            CurrentField = Battlefields.All[_fieldRng.Next(Battlefields.All.Count)];
            IsAmbush = true;
            EventOutcome = "Ambush!";
            EnterBattle();
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
