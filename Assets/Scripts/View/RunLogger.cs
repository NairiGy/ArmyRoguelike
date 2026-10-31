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
            _log.boss = run.Map[run.Map.Count - 1][0].Encounter?.Name ?? "";
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
            // FinishBattle has already moved on, so the battle was on the previous step unless the run is still there.
            var step = _run.State == RunState.Map ? _run.StepIndex : _run.StepIndex + 1;
            var node = step - 1 < _run.Chosen.Count ? _run.Map[step - 1][_run.Chosen[step - 1]] : null;
            _log.battles.Add(new BattleResult
            {
                step = step,
                encounter = battle == null ? "" : _run.CurrentEncounter?.Name,
                ambush = ambush,
                elite = _run.CurrentEncounter != null && _run.CurrentEncounter.IsElite,
                rematch = !ambush && node != null && node.IsRematch,
                skipped = battle != null && battle.WasSkipped,
                result = battle != null && battle.Phase == Phase.Victory ? "victory" : "defeat",
                turns = battle?.TurnNumber ?? 0,
                survivors = battle?.LivingArmy.Count() ?? 0,
                armySize = _run.Army.Count,
                goldEarned = goldEarned,
                goldAfter = _run.Gold,
                logFile = logFile ?? "",
                stolenGold = _run.LastStolen,
                shrine = battle?.Objective == null ? "" : battle.Phase == Phase.Victory ? "held" : battle.ObjectiveLost ? "broken" : "army lost",
                shrineHpLeft = battle?.Objective?.Hp ?? 0,
                ritual = battle == null || battle.Totems.Count == 0 ? ""
                    : battle.TotemsBroken ? "broken" : battle.SummoningComplete ? "summoned" : battle.Phase == Phase.Victory ? "won" : "army lost",
                totemsLeft = battle?.Totems.Count(t => t.IsAlive) ?? 0,
                mercenary = _pendingMerc,
                loserGains = _run.LastLoserGains.Select(m => m.Stats.Name).ToList(),
            });
            _pendingMerc = null;
        }

        /// <summary>The blessing picked after holding the Moon Shrine, or a curse taken after losing a scenario fight.</summary>
        public void RecordOmen(Omen omen, bool blessing)
        {
            var step = _run.State == RunState.Map ? _run.StepIndex : _run.StepIndex + 1;
            if (blessing)
            {
                _log.blessing = Omens.Name(omen, true);
                _log.blessingStep = step;
            }
            else
            {
                _log.curses.Add(new CurseEntry { curse = Omens.Name(omen, false), step = step });
            }
        }

        /// <summary>
        /// A technique pick after a won fight (null technique: skipped for gold). Call after the pick, with the
        /// techniques that were offered.
        /// </summary>
        public void RecordReward(IEnumerable<Technique> offered, Technique technique, ArmyMember member, Banner? bannerOffered = null, bool tookBanner = false)
        {
            _log.techniquePicks.Add(new RewardPick
            {
                // The pick has already moved the run on to the next step, so the fight was on the previous one.
                step = _run.StepIndex,
                encounter = _run.CurrentEncounter?.Name ?? "",
                offered = offered.Select(o => o.Name).ToList(),
                offeredRare = offered.Count(o => o.Rarity == TechniqueRarity.Rare),
                technique = tookBanner ? "banner" : technique?.Name ?? "skipped",
                bannerOffered = bannerOffered.HasValue ? Banners.Name(bannerOffered.Value) : "",
                unit = member?.Stats.Name ?? "",
                levelAfter = member?.Level ?? 0,
                goldAfter = _run.Gold,
            });
        }

        /// <summary>A banner taken: at the start ("start", with the ones offered), bought ("shop") or from an elite ("elite").</summary>
        public void RecordBanner(Banner banner, string source, IEnumerable<Banner> offered = null)
        {
            var step = source == "start" ? 0 : source == "elite" ? _run.StepIndex : Step;
            _log.banners.Add(new BannerEntry
            {
                banner = Banners.Name(banner), source = source, step = step, goldAfter = _run.Gold,
                offered = offered?.Select(Banners.Name).ToList() ?? new List<string>(),
            });
            if (source == "shop")
                _shop?.purchases.Add(new Purchase { kind = "banner", item = Banners.Name(banner), price = Banners.Price });
        }

        /// <summary>Units sent away to fit the army limit (Third Wheel). Call before <see cref="Run.Dismiss"/>.</summary>
        public void RecordDismiss(IEnumerable<ArmyMember> kept)
        {
            _log.dismissals.Add(new Dismissal
            {
                step = Step,
                kept = kept.Select(m => m.Stats.Name).ToList(),
                dismissed = _run.Army.Where(m => !kept.Contains(m)).Select(m => $"{m.Stats.Name} (level {m.Level})").ToList(),
            });
        }

        /// <summary>The mercenary decision before a fight: the units offered and the one hired (null: none).</summary>
        public void RecordMerc(IEnumerable<UnitStats> offered, UnitStats hired)
        {
            _pendingMerc = new MercHire { offered = offered.Select(o => o.Name).ToList(), hired = hired?.Name ?? "" };
        }

        private MercHire _pendingMerc;

        public void OpenShop()
        {
            _shop = new ShopVisit
            {
                step = Step,
                goldBefore = _run.Gold,
                recruitOffered = _run.ShopHasRecruit,
                bannerOffered = _run.ShopBanner.HasValue ? Banners.Name(_run.ShopBanner.Value) : "",
                offers = _run.Offers.Select(o => new Offer { technique = o.Name, price = o.Price, rarity = o.Rarity.ToString() }).ToList(),
                itemOffers = _run.ItemOffers.Select(o => new ItemOffer { item = o.Name, price = o.Price, effect = o.Effect }).ToList(),
            };
        }

        public void RecordPurchase(Technique technique, ArmyMember member)
        {
            _shop?.purchases.Add(new Purchase { kind = "technique", item = technique.Name, price = technique.Price, unit = member.Stats.Name, levelAfter = member.Level });
        }

        public void RecordItemPurchase(Equipment item, ArmyMember member, Equipment replaced)
        {
            _shop?.purchases.Add(new Purchase { kind = "item", item = item.Name, price = item.Price, unit = member.Stats.Name, levelAfter = member.Level, replaced = replaced?.Name ?? "" });
        }

        public void RecordRecruit(string unit, int price)
        {
            _shop?.purchases.Add(new Purchase { kind = "recruit", item = unit, price = price, unit = unit });
        }

        /// <summary>A technique forgotten at the shop (techniques that needed it went too).</summary>
        public void RecordForget(Technique technique, ArmyMember member)
        {
            _shop?.forgets.Add(new Forget { technique = technique.Name, price = Run.ForgetPrice, unit = member.Stats.Name });
        }

        public void CloseShop()
        {
            if (_shop == null)
                return;
            _shop.goldAfter = _run.Gold;
            _log.shops.Add(_shop);
            _shop = null;
        }

        public void RecordDraft(IEnumerable<UnitStats> picks)
        {
            _log.draft = picks.Select(p => p.Name).ToList();
        }

        private EventVisit _event;

        public void OpenEvent()
        {
            _event = new EventVisit
            {
                step = Step, kind = _run.CurrentEvent.ToString(), goldBefore = _run.Gold,
                technique = _run.CartTechnique?.Name ?? "",
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
            _log.fought = _run.Fought.Select(f => f.Name).ToList();
            _log.finalArmy = _run.Army.Select(m => new ArmyEntry
            {
                unit = m.Stats.Name, level = m.Level, techniques = m.Techniques.Select(t => t.Name).ToList(), equipment = m.Equipment?.Name ?? "",
                levelHp = m.LevelHp, levelDamage = m.LevelDamage, bonusDamage = m.BonusDamage, bonusMaxHp = m.BonusMaxHp,
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
            public string boss;
            public List<string> fought = new List<string>();
            public string result;
            public int stepReached;
            public int goldLeft;
            public List<string> draft = new List<string>();

            /// <summary>From the Moon Shrine: the blessing picked (empty if none), and at which step.</summary>
            public string blessing = "";
            public int blessingStep;

            /// <summary>Curses taken by losing scenario fights, in order.</summary>
            public List<CurseEntry> curses = new List<CurseEntry>();

            /// <summary>Banners taken, in order, and units sent away to fit Third Wheel's limit.</summary>
            public List<BannerEntry> banners = new List<BannerEntry>();
            public List<Dismissal> dismissals = new List<Dismissal>();

            /// <summary>Technique picks after won fights, in order.</summary>
            public List<RewardPick> techniquePicks = new List<RewardPick>();
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

            /// <summary>An elite (harder rematch), or a normal-strength rematch of a faction already beaten.</summary>
            public bool elite;
            public bool rematch;
            public bool skipped;
            public string result;
            public int turns;
            public int survivors;
            public int armySize;
            public int goldEarned;
            public int goldAfter;
            public string logFile;
            public int stolenGold;

            /// <summary>Moon Shrine fights only: "held", "broken" or "army lost", and its HP at the end.</summary>
            public string shrine;
            public int shrineHpLeft;

            /// <summary>Break the Ritual only: "broken", "summoned" or "army lost", and totems still standing at the end.</summary>
            public string ritual;
            public int totemsLeft;

            /// <summary>Mercenaries banner: who was for hire and who was hired (empty: none). Loser banner: units that fell and grew.</summary>
            public MercHire mercenary;
            public List<string> loserGains = new List<string>();
        }

        [Serializable]
        private sealed class CurseEntry
        {
            public string curse;
            public int step;
        }

        [Serializable]
        private sealed class RewardPick
        {
            public int step;
            public string encounter;
            public List<string> offered = new List<string>();

            /// <summary>How many of the offered techniques were rare.</summary>
            public int offeredRare;

            /// <summary>The technique taken, "skipped", or "banner" when the elite's banner was taken instead.</summary>
            public string technique;

            /// <summary>After an elite: the banner offered instead of a technique (empty if none).</summary>
            public string bannerOffered;
            public string unit;
            public int levelAfter;
            public int goldAfter;
        }

        [Serializable]
        private sealed class ShopVisit
        {
            public int step;
            public int goldBefore;
            public int goldAfter;
            public bool recruitOffered;
            public string bannerOffered;
            public List<Offer> offers = new List<Offer>();
            public List<ItemOffer> itemOffers = new List<ItemOffer>();
            public List<Purchase> purchases = new List<Purchase>();
            public List<Forget> forgets = new List<Forget>();
        }

        [Serializable]
        private sealed class Offer
        {
            public string technique;
            public int price;
            public string rarity;
        }

        [Serializable]
        private sealed class ItemOffer
        {
            public string item;
            public int price;
            public string effect;
        }

        [Serializable]
        private sealed class Purchase
        {
            /// <summary>"technique", "item", "recruit" or "banner".</summary>
            public string kind;

            /// <summary>The technique, item or recruited unit.</summary>
            public string item;
            public int price;
            public string unit;
            public int levelAfter;

            /// <summary>Items only: the item it replaced.</summary>
            public string replaced = "";
        }

        [Serializable]
        private sealed class Forget
        {
            public string technique;
            public int price;
            public string unit;
        }

        [Serializable]
        private sealed class EventVisit
        {
            public int step;
            public string kind;

            /// <summary>The Abandoned Cart's technique or item (one of them is empty).</summary>
            public string technique;
            public string item;
            public string choice;
            public string outcome;
            public int goldBefore;
            public int goldAfter;
        }

        [Serializable]
        private sealed class BannerEntry
        {
            public string banner;

            /// <summary>"start", "shop" or "elite".</summary>
            public string source;
            public int step;
            public int goldAfter;

            /// <summary>Start only: the banners offered.</summary>
            public List<string> offered = new List<string>();
        }

        [Serializable]
        private sealed class Dismissal
        {
            public int step;
            public List<string> kept = new List<string>();
            public List<string> dismissed = new List<string>();
        }

        [Serializable]
        private sealed class MercHire
        {
            public List<string> offered = new List<string>();
            public string hired = "";
        }

        [Serializable]
        private sealed class ArmyEntry
        {
            public string unit;
            public int level;
            public List<string> techniques = new List<string>();
            public string equipment;
            public int levelHp;
            public int levelDamage;
            public int bonusDamage;
            public int bonusMaxHp;
        }
    }
}
