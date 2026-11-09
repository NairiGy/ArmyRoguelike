using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    /// <summary>
    /// Counts how often each enemy faction's combos fire in a battle, from the battle's events, so "do they fight as one
    /// army?" is a number (see docs/faction-map.html). The battle log and the balance simulator feed it the same events,
    /// so both count the same way. Changes nothing in the battle.
    /// </summary>
    public sealed class ComboTally
    {
        /// <summary>Every counter in report order: key ("faction.name"), and what it counts. Keys ending in "Max" keep the highest value seen, the rest add up.</summary>
        public static readonly IReadOnlyList<(string Key, string Meaning)> Counters = new[]
        {
            ("wolves.howls", "Howls"),
            ("wolves.howledBites", "Howled wolves that bit the same turn"),
            ("wolves.bites", "Wolf bites"),
            ("wolves.chainBonus", "Extra damage from the bite chain (+1 per earlier bite that turn)"),
            ("wolves.chainMax", "Most wolf bites in one enemy turn"),
            ("wolves.surrounds", "Times one unit took a 3rd wolf bite in one turn"),
            ("goblins.flungGoblins", "Goblins flung"),
            ("goblins.flungGoblinHits", "Flung goblins that hit on landing"),
            ("goblins.flungBombs", "Bombs flung by goblins"),
            ("goblins.flungMines", "Your mines flung back by goblins"),
            ("goblins.ownBlastDamage", "Damage bombs and Powder Kegs did to their own side"),
            ("bandits.guards", "Guard triggers"),
            ("bandits.guardsForShooter", "Guard triggers that protected a crossbowman"),
            ("bandits.overwatchShots", "Overwatch shots"),
            ("bandits.caltropsStops", "Your units stopped by caltrops"),
            ("bandits.bullRushes", "Bull Rush pushes"),
            ("bandits.bullRushCollisions", "Bull Rush pushes that slammed into something"),
            ("bandits.bullRushIntoTrap", "Bull Rush pushes that ended on caltrops or in an Overwatch shot"),
            ("bandits.steals", "Gold steals"),
            ("bandits.escapes", "Cutpurses that escaped"),
            ("elementals.tremors", "Tremors"),
            ("elementals.tremorSlows", "Your units slowed by a tremor"),
            ("elementals.tremorPins", "Your units pinned by a tremor (already slowed)"),
            ("elementals.aftershockSlows", "Your units slowed by a dying shard (Aftershock)"),
            ("elementals.splits", "Elementals split"),
            ("elementals.shardHits", "Shard attacks"),
            ("demons.hellswaps", "Hellswaps"),
            ("demons.swapFollowUps", "Hellswaps followed by an enemy hit on the swapped unit the same turn"),
            ("demons.swapFollowUpDamage", "Damage to swapped units the turn they were swapped"),
            ("demons.furyFromImps", "Fury from Blood Offering (imp deaths)"),
            ("demons.furyFromBloodlust", "Fury from Bloodlust (deaths next to a Demon)"),
            ("demons.rendingBlows", "Rending Blows"),
            ("demons.contracts", "Soul Contracts signed"),
            ("demons.contractsBroken", "Contracts broken by hitting the Devil"),
            ("demons.contractsPaid", "Contracts paid"),
            ("demons.fireBreaths", "Fire Breaths"),
            ("demons.fireBreathMulti", "Fire Breaths that hit 2 of your units"),
        };

        private readonly Dictionary<string, int> _counts = Counters.ToDictionary(c => c.Key, _ => 0);

        // State for the turn being watched; reset when the turn number changes.
        private int _turn = int.MinValue;
        private readonly HashSet<Unit> _howled = new HashSet<Unit>();
        private int _bitesThisTurn;
        private readonly Dictionary<Unit, int> _bitesOn = new Dictionary<Unit, int>();
        private readonly HashSet<Unit> _swapped = new HashSet<Unit>();
        private readonly HashSet<Unit> _swapHit = new HashSet<Unit>();

        // Sequence state: what an action set up for the events that follow it. Cleared when the next action starts.
        private bool _explosiveFlung;
        private Unit _rushed;

        /// <summary>Events that start a unit's action; the ones after it (pushes, blasts, ticks) are its results.</summary>
        private static bool StartsAction(BattleEvent ev) =>
            ev is MoveEvent || ev is AttackEvent || ev is SmashEvent || ev is BoulderEvent || ev is BombThrowEvent ||
            ev is AreaAttackEvent || ev is FlingEvent || ev is HowlEvent || ev is SwapEvent || ev is TremorEvent ||
            ev is OverwatchEvent;

        public IReadOnlyDictionary<string, int> Counts => _counts;

        public int this[string key] => _counts.TryGetValue(key, out var n) ? n : 0;

        private void Add(string key, int amount = 1) => _counts[key] += amount;

        private void Max(string key, int value) => _counts[key] = Math.Max(_counts[key], value);

        /// <summary>Feeds one event, in the order the battle produced it. <paramref name="turn"/> is the battle's turn number.</summary>
        public void Observe(BattleEvent ev, int turn)
        {
            if (turn != _turn)
            {
                _turn = turn;
                _howled.Clear();
                _bitesThisTurn = 0;
                _bitesOn.Clear();
                _swapped.Clear();
                _swapHit.Clear();
            }

            var flung = _explosiveFlung;
            var rushed = _rushed;
            if (StartsAction(ev) || ev is ExplosionEvent)
                _explosiveFlung = false;
            if (StartsAction(ev))
            {
                _rushed = null;
            }

            switch (ev)
            {
                case HowlEvent howl:
                    Add("wolves.howls");
                    foreach (var wolf in howl.Wolves)
                        _howled.Add(wolf);
                    break;

                case AttackEvent attack:
                    if (attack.Attacker.Team == Team.Enemy && attack.Attacker.Has(Ability.WolfBite))
                        Bite(attack);
                    if (attack.Via == "overwatch")
                    {
                        Add("bandits.overwatchShots");
                        if (rushed == attack.Target)
                            Add("bandits.bullRushIntoTrap");
                    }
                    if (attack.Attacker.Team == Team.Enemy && attack.Attacker.Stats.Kind == UnitKind.Shard)
                        Add("elementals.shardHits");
                    EnemyHit(attack.Attacker, attack.Target, attack.Damage);
                    break;

                case SmashEvent smash:
                    foreach (var hit in smash.Hits)
                        EnemyHit(smash.Attacker, hit.Target, hit.Damage);
                    break;

                case AreaAttackEvent area:
                    if (area.Attacker.Team == Team.Enemy && area.Kind == "fire breath")
                    {
                        Add("demons.fireBreaths");
                        if (area.Hits.Count(h => h.Target.Team == Team.Player) >= 2)
                            Add("demons.fireBreathMulti");
                    }
                    foreach (var hit in area.Hits)
                        EnemyHit(area.Attacker, hit.Target, hit.Damage);
                    break;

                case FlingEvent fling:
                    if (fling.Thrown == null)
                        _explosiveFlung = true;
                    else
                    {
                        Add("goblins.flungGoblins");
                        if (fling.Hit.HasValue && fling.Hit.Value.Damage > 0)
                            Add("goblins.flungGoblinHits");
                    }
                    break;

                case ExplosionEvent blast:
                    if (flung)
                        Add(blast.Cause == "mine" ? "goblins.flungMines" : "goblins.flungBombs");
                    if (blast.Cause == "powder keg" || (blast.Cause == "bomb" && blast.Owner != null && blast.Owner.Team == Team.Enemy))
                        Add("goblins.ownBlastDamage", blast.Hits.Where(h => h.Target.Team == Team.Enemy).Sum(h => h.Damage));
                    break;

                case EffectEvent effect:
                    switch (effect.Kind)
                    {
                        case EffectKind.Guard when effect.Unit.Team == Team.Enemy:
                            Add("bandits.guards");
                            if (effect.Protected != null && effect.Protected.Has(Ability.BehindCover))
                                Add("bandits.guardsForShooter");
                            break;
                        case EffectKind.Steal:
                            Add("bandits.steals");
                            break;
                        case EffectKind.Fury:
                            Add(effect.Source != null && effect.Source != effect.Unit ? "demons.furyFromImps" : "demons.furyFromBloodlust", effect.Amount);
                            break;
                    }
                    break;

                case EscapeEvent _:
                    Add("bandits.escapes");
                    break;

                case TickEvent tick:
                    switch (tick.Cause)
                    {
                        case "caltrops":
                            Add("bandits.caltropsStops");
                            if (rushed == tick.Hit.Target)
                                Add("bandits.bullRushIntoTrap");
                            break;
                        case "blood price":
                            Add("demons.rendingBlows");
                            break;
                        case "contract":
                            Add("demons.contractsPaid");
                            break;
                    }
                    break;

                case PushEvent push:
                    if (push.Pusher != null && push.Pusher.Team == Team.Enemy && push.Pusher.Has(Ability.BullRush) && push.Unit.Team == Team.Player)
                    {
                        Add("bandits.bullRushes");
                        if (push.Collision.HasValue)
                            Add("bandits.bullRushCollisions");
                        _rushed = push.Unit;
                    }
                    break;

                case TremorEvent tremor:
                    Add("elementals.tremors");
                    Add("elementals.tremorSlows", tremor.Slowed.Count);
                    Add("elementals.tremorPins", tremor.Pinned.Count);
                    break;

                case SplitEvent _:
                    Add("elementals.splits");
                    break;

                case SwapEvent swap:
                    Add("demons.hellswaps");
                    _swapped.Add(swap.Target);
                    break;

                case StatusEvent status:
                    if (status.Kind == StatusKind.Contract)
                        Add("demons.contracts");
                    else if (status.Kind == StatusKind.ContractBroken)
                        Add("demons.contractsBroken");
                    else if (status.Kind == StatusKind.Slow && status.Source != null && status.Source.Has(Ability.Aftershock) && status.Target.Team == Team.Player)
                        Add("elementals.aftershockSlows");
                    break;
            }
        }

        /// <summary>Feeds a batch of events in order.</summary>
        public void Observe(IEnumerable<BattleEvent> events, int turn)
        {
            if (events == null)
                return;
            foreach (var ev in events)
                Observe(ev, turn);
        }

        /// <summary>Adds another battle's counts: sums, and the highest value for "Max" keys.</summary>
        public void Merge(ComboTally other)
        {
            foreach (var (key, _) in Counters)
            {
                if (key.EndsWith("Max", StringComparison.Ordinal))
                    Max(key, other[key]);
                else
                    Add(key, other[key]);
            }
        }

        private void Bite(AttackEvent bite)
        {
            Add("wolves.bites");
            Add("wolves.chainBonus", Math.Min(_bitesThisTurn, bite.Damage));
            _bitesThisTurn++;
            Max("wolves.chainMax", _bitesThisTurn);
            if (_howled.Remove(bite.Attacker))
                Add("wolves.howledBites");
            _bitesOn[bite.Target] = (_bitesOn.TryGetValue(bite.Target, out var n) ? n : 0) + 1;
            if (_bitesOn[bite.Target] == 3)
                Add("wolves.surrounds");
        }

        private void EnemyHit(Unit attacker, Unit target, int damage)
        {
            if (attacker == null || attacker.Team != Team.Enemy || !_swapped.Contains(target))
                return;
            if (_swapHit.Add(target))
                Add("demons.swapFollowUps");
            Add("demons.swapFollowUpDamage", damage);
        }
    }
}
