using System;
using System.Collections.Generic;
using System.Linq;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Records a whole run for playtest analysis: the generated map, which node was picked over which,
    /// each battle's outcome, every shop visit (offers, purchases, recruits) and every ? event choice,
    /// plus the final army. Each battle's details are in its own battle log, named in <c>battles[].logFile</c>.
    /// </summary>
    public sealed class RunLogger
    {
        private readonly Run _run;
        private readonly RunLog _log = new RunLog();
        private readonly DateTime _created = DateTime.Now;
        private ShopVisit _shop;
        private bool _saved;

        public RunLogger(Run run)
        {
            _run = run;
            _log.startedAt = _created.ToString("yyyy-MM-dd HH:mm:ss");
            _log.seed = run.Seed;
            for (var i = 0; i < run.Map.Count; i++)
                _log.map.Add(new MapStep { step = i + 1, nodes = run.Map[i].Select(n => n.Label).ToList() });
        }

        private int Step => _run.StepIndex + 1;

        public void RecordChoice(MapNode chosen, MapNode other)
        {
            _log.choices.Add(new Choice { step = Step, chosen = chosen.Label, other = other?.Label ?? "" });
        }

        public void RecordBattle(Battle battle, int goldEarned, string logFile, bool ambush)
        {
            _log.battles.Add(new BattleResult
            {
                // FinishBattle has already moved on, so the battle was on the previous step unless the run ended.
                step = _run.State == RunState.Map ? _run.StepIndex : _run.StepIndex + 1,
                encounter = battle == null ? "" : _run.CurrentEncounter?.Name,
                ambush = ambush,
                result = battle != null && battle.Phase == Phase.Victory ? "victory" : "defeat",
                turns = battle?.TurnNumber ?? 0,
                survivors = battle?.Living(Team.Player).Count() ?? 0,
                armySize = _run.Army.Count,
                goldEarned = goldEarned,
                goldAfter = _run.Gold,
                logFile = logFile ?? "",
            });
        }

        public void OpenShop()
        {
            _shop = new ShopVisit
            {
                step = Step,
                goldBefore = _run.Gold,
                recruitOffered = _run.ShopHasRecruit,
                offers = _run.Offers.Select(o => new Offer { item = o.Name, price = o.Price, effect = o.Effect }).ToList(),
            };
        }

        public void RecordPurchase(Equipment item, ArmyMember member, Equipment replaced)
        {
            _shop?.purchases.Add(new Purchase
            {
                item = item.Name, price = item.Price, unit = member.Stats.Name, replaced = replaced?.Name ?? "",
            });
        }

        public void RecordRecruit(string unit, int price)
        {
            _shop?.purchases.Add(new Purchase { item = "recruit", price = price, unit = unit, replaced = "" });
        }

        public void CloseShop()
        {
            if (_shop == null)
                return;
            _shop.goldAfter = _run.Gold;
            _log.shops.Add(_shop);
            _shop = null;
        }

        private EventVisit _event;

        public void OpenEvent()
        {
            _event = new EventVisit
            {
                step = Step, kind = _run.CurrentEvent.ToString(), goldBefore = _run.Gold,
                item = _run.CartItem?.Name ?? "",
            };
        }

        public void RecordEvent(string choice, string outcome)
        {
            if (_event == null)
                return;
            _event.choice = choice;
            _event.outcome = outcome ?? "";
            _event.goldAfter = _run.Gold;
            _log.events.Add(_event);
            _event = null;
        }

        /// <summary>Writes the file once. A run left before it ended is saved as "abandoned".</summary>
        public void Save()
        {
            if (_saved)
                return;
            _saved = true;
            CloseShop();
            _log.endedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _log.result = _run.State == RunState.Won ? "won" : _run.State == RunState.Lost ? "lost" : "abandoned";
            _log.stepReached = _run.StepIndex + 1;
            _log.goldLeft = _run.Gold;
            _log.finalArmy = _run.Army.Select(m => new ArmyEntry
            {
                unit = m.Stats.Name, equipment = m.Equipment?.Name ?? "", bonusDamage = m.BonusDamage, bonusMaxHp = m.BonusMaxHp,
            }).ToList();
            PlaytestFiles.Write($"run-{_created:yyyyMMdd-HHmmss}.json", JsonUtility.ToJson(_log, true));
        }

        // ---------- JSON shape ----------

        [Serializable]
        private sealed class RunLog
        {
            public string startedAt;
            public string endedAt;
            public int seed;
            public string result;
            public int stepReached;
            public int goldLeft;
            public List<MapStep> map = new List<MapStep>();
            public List<Choice> choices = new List<Choice>();
            public List<BattleResult> battles = new List<BattleResult>();
            public List<ShopVisit> shops = new List<ShopVisit>();
            public List<EventVisit> events = new List<EventVisit>();
            public List<ArmyEntry> finalArmy = new List<ArmyEntry>();
        }

        [Serializable]
        private sealed class MapStep
        {
            public int step;
            public List<string> nodes = new List<string>();
        }

        [Serializable]
        private sealed class Choice
        {
            public int step;
            public string chosen;
            public string other;
        }

        [Serializable]
        private sealed class BattleResult
        {
            public int step;
            public string encounter;
            public bool ambush;
            public string result;
            public int turns;
            public int survivors;
            public int armySize;
            public int goldEarned;
            public int goldAfter;
            public string logFile;
        }

        [Serializable]
        private sealed class ShopVisit
        {
            public int step;
            public int goldBefore;
            public int goldAfter;
            public bool recruitOffered;
            public List<Offer> offers = new List<Offer>();
            public List<Purchase> purchases = new List<Purchase>();
        }

        [Serializable]
        private sealed class Offer
        {
            public string item;
            public int price;
            public string effect;
        }

        [Serializable]
        private sealed class Purchase
        {
            public string item;
            public int price;
            public string unit;
            public string replaced;
        }

        [Serializable]
        private sealed class EventVisit
        {
            public int step;
            public string kind;
            public string item;
            public string choice;
            public string outcome;
            public int goldBefore;
            public int goldAfter;
        }

        [Serializable]
        private sealed class ArmyEntry
        {
            public string unit;
            public string equipment;
            public int bonusDamage;
            public int bonusMaxHp;
        }
    }
}
