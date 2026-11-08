using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    /// <summary>One statue on the board during a battle.</summary>
    public sealed class Unit
    {
        public readonly int Id;
        public readonly Team Team;
        public readonly UnitStats Stats;

        /// <summary>The army member this unit was made from (player units only), for run-long bonuses.</summary>
        public readonly ArmyMember Member;

        /// <summary>For summons: the unit that built it.</summary>
        public readonly Unit Summoner;

        public int Hp { get; internal set; }
        public int Ap { get; internal set; }

        /// <summary>Tiles the unit can still walk this turn. Moving doesn't cost AP.</summary>
        public int Moves { get; internal set; }

        /// <summary>The tile the unit stands on. For a 2x2 unit, the corner with the lowest X and Y.</summary>
        public GridPos Pos { get; internal set; }

        public bool IsPlaced { get; internal set; }

        /// <summary>True once the unit has finished its turn in the current phase.</summary>
        public bool IsDone { get; internal set; }

        // ---------- Per-turn flags ----------

        /// <summary>Moved at least one tile this turn (for Steady Aim).</summary>
        public bool MovedThisTurn { get; internal set; }

        /// <summary>Spent AP this turn (attack or action), so it can't move again until its next turn.</summary>
        public bool ActedThisTurn { get; internal set; }

        /// <summary>Has attacked this turn.</summary>
        public bool AttackedThisTurn { get; internal set; }

        // Own turns left before each action is ready again (missing = ready).
        private readonly Dictionary<ActionKind, int> _cooldowns = new Dictionary<ActionKind, int>();

        /// <summary>Own turns until the action is ready again; 0 = ready now.</summary>
        public int CooldownLeft(ActionKind action) => _cooldowns.TryGetValue(action, out var left) ? left : 0;

        internal void StartCooldown(ActionKind action, int turns)
        {
            if (turns > 0)
                _cooldowns[action] = turns;
        }

        // How often each special action was used this battle (for the playtest log).
        private readonly Dictionary<ActionKind, int> _uses = new Dictionary<ActionKind, int>();

        /// <summary>Times the unit used this special action in this battle.</summary>
        public int TimesUsed(ActionKind action) => _uses.TryGetValue(action, out var n) ? n : 0;

        internal void CountUse(ActionKind action) => _uses[action] = TimesUsed(action) + 1;

        /// <summary>Every action ready again (tests).</summary>
        internal void ClearCooldowns() => _cooldowns.Clear();

        /// <summary>Start of its own turn: every cooldown counts down by one.</summary>
        internal void TickCooldowns()
        {
            foreach (var action in new List<ActionKind>(_cooldowns.Keys))
                if (--_cooldowns[action] <= 0)
                    _cooldowns.Remove(action);
        }

        // Ids of the units this one has attacked this battle (for the Deadeye's opening shot).
        private readonly HashSet<int> _struck = new HashSet<int>();

        /// <summary>Has attacked this target before in this battle.</summary>
        public bool HasStruck(Unit target) => _struck.Contains(target.Id);

        internal void MarkStruck(Unit target) => _struck.Add(target.Id);

        /// <summary>Riposte already used this enemy turn.</summary>
        public bool CounterUsed { get; internal set; }

        /// <summary>Dodge already used this enemy turn.</summary>
        public bool DodgeUsed { get; internal set; }

        // ---------- Statuses ----------

        /// <summary>Vanished (rogue): enemy turns left before it shows again. Its next attack reveals it.</summary>
        public int HiddenTurns { get; internal set; }

        /// <summary>Invisible: enemies can't target or attack it, though area blasts still hit it.</summary>
        public bool IsHidden => HiddenTurns > 0;

        /// <summary>Pinned: can't move on its next turn.</summary>
        public int SnareTurns { get; internal set; }

        /// <summary>Can't move this turn (a snare took effect).</summary>
        public bool RootedThisTurn { get; internal set; }

        /// <summary>Chilled: can't move on its next turn.</summary>
        public int SlowNext { get; internal set; }

        /// <summary>Can't move this turn because of Chill.</summary>
        public bool SlowedThisTurn { get; internal set; }

        /// <summary>Poison ticks left; each deals poison damage at the start of the unit's turn.</summary>
        public int PoisonTurns { get; internal set; }

        /// <summary>Who poisoned it last, for the playtest log's credit. Not a source for rules: ticks have none.</summary>
        public Unit PoisonSource { get; internal set; }

        /// <summary>Ward shield: absorbs this much damage before HP.</summary>
        public int Shield { get; internal set; }

        /// <summary>Moon omen shield: absorbs damage before HP, gone at the start of the next player turn.</summary>
        public int MoonShield { get; internal set; }

        /// <summary>The Lucky Charm has already saved this unit in this battle.</summary>
        public bool LastStandUsed { get; internal set; }

        /// <summary>Gold a cutpurse is carrying after a hit. It flees with it; killing it gets it back.</summary>
        public int CarryingLoot { get; internal set; }

        /// <summary>Shield Up: enemy turns left of taking less damage.</summary>
        public int ShieldUpTurns { get; internal set; }

        /// <summary>Taunt: enemy turns left of forcing nearby enemies to target it.</summary>
        public int TauntTurns { get; internal set; }

        public bool IsTaunting => TauntTurns > 0;

        /// <summary>Battlecry: own turns left of its extra move (the one it was shouted in counts).</summary>
        public int BattlecryTurns { get; internal set; }

        /// <summary>Battlecry's temporary HP: lost before anything else, gone when the battlecry ends.</summary>
        public int TempHp { get; internal set; }

        /// <summary>Throw Shield was used in this battle: more damage and a move for good.</summary>
        public bool ShieldThrown { get; internal set; }

        /// <summary>The zone-of-control attack was already used this round.</summary>
        public bool ZocStrikeUsed { get; internal set; }

        /// <summary>Hired for this battle only (Mercenaries banner): fights, but doesn't count as a survivor or keep the fight going alone.</summary>
        public bool IsMercenary { get; internal set; }

        /// <summary>Left the board (a cutpurse that got away). No longer in the battle.</summary>
        public bool Escaped { get; internal set; }

        /// <summary>Fell off the board (Edge of the World). Dead, with no bone pile.</summary>
        public bool Fell { get; internal set; }

        /// <summary>Fortune's Favored: deals double damage until the next player turn.</summary>
        public bool Favored { get; internal set; }

        // ---------- Demons ----------

        /// <summary>Fury (demons): +1 damage per point for the rest of the battle, from Blood Offering and Bloodlust.</summary>
        public int Fury { get; internal set; }

        /// <summary>Soul Contract: the Devil that signed one on this unit; it comes due at the start of the next enemy turn. Null = none.</summary>
        public Unit ContractBy { get; internal set; }

        public Unit(int id, Team team, UnitStats stats, ArmyMember member = null, Unit summoner = null)
        {
            Id = id;
            Team = team;
            Stats = stats;
            Member = member;
            Summoner = summoner;
            Hp = MaxHp;
        }

        /// <summary>
        /// A copy for a copied battle. Other units it points to come from <paramref name="map"/>, which must already hold
        /// its summoner (summoners always come first in the battle's list); call <see cref="CopyLinks"/> once all exist.
        /// </summary>
        internal Unit CopyFor(IReadOnlyDictionary<Unit, Unit> map)
        {
            var c = new Unit(Id, Team, Stats, Member, Summoner == null ? null : map[Summoner])
            {
                Hp = Hp, Ap = Ap, Moves = Moves, Pos = Pos, IsPlaced = IsPlaced, IsDone = IsDone,
                MovedThisTurn = MovedThisTurn, ActedThisTurn = ActedThisTurn, AttackedThisTurn = AttackedThisTurn,
                CounterUsed = CounterUsed, DodgeUsed = DodgeUsed, HiddenTurns = HiddenTurns, SnareTurns = SnareTurns,
                RootedThisTurn = RootedThisTurn, SlowNext = SlowNext, SlowedThisTurn = SlowedThisTurn, PoisonTurns = PoisonTurns,
                Shield = Shield, MoonShield = MoonShield, LastStandUsed = LastStandUsed, CarryingLoot = CarryingLoot,
                IsMercenary = IsMercenary, Escaped = Escaped, Fell = Fell, Favored = Favored, PackHp = PackHp,
                BannerAp = BannerAp, BannerMoves = BannerMoves, LeftPile = LeftPile, PileGone = PileGone,
                ShieldUpTurns = ShieldUpTurns, TauntTurns = TauntTurns, BattlecryTurns = BattlecryTurns, TempHp = TempHp,
                ShieldThrown = ShieldThrown, ZocStrikeUsed = ZocStrikeUsed, Fury = Fury,
            };
            foreach (var cooldown in _cooldowns)
                c._cooldowns.Add(cooldown.Key, cooldown.Value);
            foreach (var use in _uses)
                c._uses.Add(use.Key, use.Value);
            c._struck.UnionWith(_struck);
            return c;
        }

        /// <summary>Second step of <see cref="CopyFor"/>: points the copy at the copied units.</summary>
        internal void CopyLinks(Unit original, IReadOnlyDictionary<Unit, Unit> map)
        {
            PoisonSource = original.PoisonSource == null ? null : map[original.PoisonSource];
            ContractBy = original.ContractBy == null ? null : map[original.ContractBy];
        }

        /// <summary>The item in this unit's single slot, or null.</summary>
        public Equipment Equipment => Member?.Equipment;

        // Stats after run-long bonuses.
        public int MaxHp => Math.Max(1, Stats.MaxHp + (Member?.BonusMaxHp ?? 0) + (Member?.LevelHp ?? 0) + PackHp);

        /// <summary>Max HP from a Pack Leader's Pack Bond: +1 while one lives, -1 once the last one dies.</summary>
        public int PackHp { get; internal set; }
        public int BaseDamage => Math.Max(1, Stats.Damage + (Member?.BonusDamage ?? 0) + (Member?.LevelDamage ?? 0) + (ShieldThrown ? Battle.ThrowShieldDamage : 0));

        /// <summary>Attack range in steps. Long Reach makes it 2 (still melee).</summary>
        public int Range => Math.Max(Has(Ability.LongReach) ? 2 : 1, Stats.Range);
        public int MaxAp => Math.Max(1, Stats.MaxAp) + (InWrath ? 1 : 0) + BannerAp;

        /// <summary>AP and moves from the army's banners (Third Wheel), set when the battle starts.</summary>
        public int BannerAp { get; internal set; }

        public int BannerMoves { get; internal set; }

        /// <summary>Wrath (the Giant): at half HP or less it gets one more AP each turn.</summary>
        public bool InWrath => Has(Ability.Wrath) && IsAlive && Hp * 2 <= MaxHp;

        /// <summary>Tiles per turn: the type's moves (1, or 2 with Swift), plus 1 for Spring Boots and 1 for Third Wheel.</summary>
        public int MaxMoves => Stats.Moves + (Stats.Moves > 0 ? (Has(Ability.SpringBoots) ? 1 : 0) + BannerMoves + (BattlecryTurns > 0 ? Battle.BattlecryMoves : 0) + (ShieldThrown ? Battle.ThrowShieldMoves : 0) : 0);

        public int Size => Stats.Size;

        /// <summary>A ranged attack needs line of sight and can't be countered. Long Reach is still melee.</summary>
        public bool IsRanged => Range > 1 && !Has(Ability.LongReach);

        public bool IsAlive => Hp > 0;

        public bool IsSummon => Stats.IsSummon;

        public bool IsObjective => Stats.IsObjective;

        public bool IsWolf => Stats.Kind == UnitKind.Wolf;

        public bool IsPackLeader => Stats.Kind == UnitKind.PackLeader;

        /// <summary>One of the demon faction (they all have Hellborn), raised copies included.</summary>
        public bool IsDemon => Stats.Has(Ability.Hellborn);

        public bool IsSnared => SnareTurns > 0 || RootedThisTurn;

        public bool IsSlowed => SlowNext > 0 || SlowedThisTurn;

        /// <summary>A dead unit that left a bone pile behind.</summary>
        public bool IsRemains => IsPlaced && !IsAlive && (Stats.LeavesRemains || LeftPile) && !PileGone;

        /// <summary>One of your units that died while a Necromancer with Soul Harvest was on the board: it left a bone pile.</summary>
        public bool LeftPile { get; internal set; }

        /// <summary>Its bone pile is used up (raised as a new unit by Soul Harvest, or blown up by Corpse Blast).</summary>
        public bool PileGone { get; internal set; }

        /// <summary>Has the ability, from its type, its item or a technique.</summary>
        public bool Has(Ability ability) =>
            ability != Ability.None && (Stats.Has(ability) || Equipment?.Ability == ability || (Member?.HasTechniqueAbility(ability) ?? false));

        /// <summary>Its special actions: its type's, then those from techniques.</summary>
        public IEnumerable<ActionKind> Actions =>
            Member == null ? Stats.Actions : Stats.Actions.Concat(Member.TechniqueActions).Distinct();

        public bool HasAction(ActionKind action) => Actions.Contains(action);

        public override string ToString() => $"{Stats.Name}#{Id} {Pos} hp {Hp} ap {Ap} mv {Moves}";
    }
}
