using System;
using System.Collections.Generic;
using System.Linq;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Records a whole run for playtest analysis: each battle's outcome, every shop visit (what was
    /// offered, what was bought for whom, what got replaced) and the final army. Each battle's
    /// details are in its own battle log, named in <c>battles[].logFile</c>.
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
        }

        public void RecordBattle(Battle battle, int goldEarned, string logFile)
        {
            _log.battles.Add(new BattleResult
            {
                number = _run.BattleIndex + 1,
                encounter = _run.CurrentEncounter.Name,
                result = battle.Phase == Phase.Victory ? "victory" : "defeat",
                turns = battle.TurnNumber,
                survivors = battle.Living(Team.Player).Count(),
                goldEarned = goldEarned,
                goldAfter = _run.Gold,
                logFile = logFile ?? "",
            });
        }

        public void OpenShop()
        {
            _shop = new ShopVisit
            {
                afterBattle = _run.BattleIndex + 1,
                goldBefore = _run.Gold,
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

        public void CloseShop()
        {
            if (_shop == null)
                return;
            _shop.goldAfter = _run.Gold;
            _log.shops.Add(_shop);
            _shop = null;
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
            _log.battlesReached = _run.BattleIndex + 1;
            _log.goldLeft = _run.Gold;
            _log.finalArmy = _run.Army.Select(m => new ArmyEntry { unit = m.Stats.Name, equipment = m.Equipment?.Name ?? "" }).ToList();
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
            public int battlesReached;
            public int goldLeft;
            public List<BattleResult> battles = new List<BattleResult>();
            public List<ShopVisit> shops = new List<ShopVisit>();
            public List<ArmyEntry> finalArmy = new List<ArmyEntry>();
        }

        [Serializable]
        private sealed class BattleResult
        {
            public int number;
            public string encounter;
            public string result;
            public int turns;
            public int survivors;
            public int goldEarned;
            public int goldAfter;
            public string logFile;
        }

        [Serializable]
        private sealed class ShopVisit
        {
            public int afterBattle;
            public int goldBefore;
            public int goldAfter;
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
        private sealed class ArmyEntry
        {
            public string unit;
            public string equipment;
        }
    }
}
