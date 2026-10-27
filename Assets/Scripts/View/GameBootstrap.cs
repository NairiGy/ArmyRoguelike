using System;
using System.Linq;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Builds and runs a whole run at runtime, so the scene only needs its camera and light:
    /// the map screen, battles, shops, ? events and the run-end screen.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static readonly Color Background = new Color(0.12f, 0.12f, 0.15f);
        private const float ReferenceHeight = 720f;

        private Run _run;
        private RunLogger _runLog;
        private GameObject _battleRoot;
        private BattleController _controller;
        private Equipment _selectedOffer;
        private string _lastNote;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindAnyObjectByType<GameBootstrap>() != null)
                return;
            new GameObject("Game").AddComponent<GameBootstrap>();
        }

        /// <summary>Throws away the current run and starts a fresh one.</summary>
        public static void Restart()
        {
            var current = FindAnyObjectByType<GameBootstrap>();
            if (current != null)
                Destroy(current.gameObject);
            new GameObject("Game").AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            _run = new Run(Environment.TickCount);
            _runLog = new RunLogger(_run);
        }

        private void OnDestroy() => _runLog?.Save();

        // Button presses change what the screens show, so they run in Update rather than mid-OnGUI,
        // where IMGUI would complain about the layout changing between its passes.
        private Action _pending;

        private void Do(Action action) => _pending = action;

        private void Update()
        {
            if (_pending == null)
                return;
            var action = _pending;
            _pending = null;
            action();
        }

        // ---------- Flow ----------

        private void ChooseNode(int index)
        {
            var other = _run.CurrentChoices.Count > 1 ? _run.CurrentChoices[1 - index] : null;
            _runLog.RecordChoice(_run.CurrentChoices[index], other);
            _run.ChooseNode(index);
            _selectedOffer = null;
            switch (_run.State)
            {
                case RunState.Battle:
                    StartBattle();
                    break;
                case RunState.Shop:
                    _runLog.OpenShop();
                    break;
                case RunState.Event:
                    _runLog.OpenEvent();
                    break;
                case RunState.Trainer:
                    _runLog.OpenTrainer();
                    break;
            }
        }

        // ---------- Draft ----------

        private readonly System.Collections.Generic.List<UnitStats> _draftPicks = new System.Collections.Generic.List<UnitStats>();

        private void ToggleDraftPick(UnitStats unit)
        {
            if (_draftPicks.Contains(unit))
                _draftPicks.Remove(unit);
            else if (_draftPicks.Count < Run.DraftSize)
                _draftPicks.Add(unit);
        }

        private void ConfirmDraft()
        {
            if (_run.Draft(_draftPicks.ToList()))
                _runLog.RecordDraft(_draftPicks);
        }

        // ---------- Trainer ----------

        private void Evolve(ArmyMember member, UnitStats into)
        {
            var from = member.Stats;
            var price = _run.NextEvolutionPrice;
            if (_run.Evolve(member, into))
                _runLog.RecordEvolution(from, into, price);
        }

        private void LeaveTrainer()
        {
            _runLog.CloseTrainer();
            _run.LeaveTrainer();
            _lastNote = null;
        }

        private void StartBattle()
        {
            var battle = _run.StartBattle();
            _battleRoot = new GameObject($"Battle step {_run.StepIndex + 1}");
            _battleRoot.transform.SetParent(transform, false);
            var board = BoardView.Create(battle, _battleRoot.transform);
            FrameCamera(Camera.main, board);
            BattleScenery.Apply(Camera.main, board, BattleScenery.MainPalette, false, BattleScenery.MainSetting);
            var context = new BattleContext(_run.StepIndex + 1, Run.Steps, _run.CurrentEncounter.Name, _run.Gold, _run.Seed,
                _run.IsAmbush, _run.CurrentEncounter.IsBoss, _run.CurrentEncounter.Layout, _run.CurrentEncounter.LayoutRule,
                elite: _run.CurrentEncounter.IsElite);
            _controller = _battleRoot.AddComponent<BattleController>();
            _controller.Init(board, Camera.main, context, () => Do(FinishBattle));
        }

        private void FinishBattle()
        {
            var battle = _run.CurrentBattle;
            var ambush = _run.IsAmbush;
            var earned = _run.FinishBattle();
            _runLog.RecordBattle(battle, earned, _controller.LogFile, ambush);
            var curse = battle.ScenarioLost ? _run.LastCurse : null;
            if (curse.HasValue)
                _runLog.RecordOmen(curse.Value, false);
            Destroy(_battleRoot);
            _battleRoot = null;
            _controller = null;
            _lastNote = curse.HasValue
                ? $"{(battle.SummoningComplete ? "The summoning completed" : "The shrine broke")}. {Omens.Name(curse.Value, false)}: {Omens.Describe(curse.Value, false)}"
                : battle.Phase != Phase.Victory ? null
                : _run.LastStolen > 0 ? $"Battle won: +{earned} gold, but cutpurses got away with {_run.LastStolen}."
                : $"Battle won: +{earned} gold.";
            if (_run.State == RunState.Won || _run.State == RunState.Lost)
                _runLog.Save();
        }

        /// <summary>After an event choice: log it, then follow wherever the run went (map or ambush battle).</summary>
        private void AfterEvent(string choice)
        {
            _runLog.RecordEvent(choice, _run.EventOutcome);
            _lastNote = _run.EventOutcome;
            if (_run.State == RunState.Battle)
                StartBattle();
        }

        // ---------- Test mode ----------
        //
        // Editor and dev builds only: any army, any encounter and any blessing or curse, straight into
        // the battle with no run around it. Afterwards it comes back to the setup screen.

        private static bool TestModeAvailable => Application.isEditor || Debug.isDebugBuild;

        private bool _testMode;
        private readonly System.Collections.Generic.List<UnitStats> _testArmy = new System.Collections.Generic.List<UnitStats>();
        private Encounter _testEncounter = Encounter.MoonShrine;
        private readonly System.Collections.Generic.List<Encounter> _testFought = new System.Collections.Generic.List<Encounter>();

        // Null for no omen; otherwise the omen and whether it's a blessing (true) or a curse.
        private (Omen omen, bool blessing)? _testOmen;

        private void EnterTestMode()
        {
            _testMode = true;
            // Test battles aren't part of a run, so no run log is written for this one.
            _runLog = null;
            if (_testArmy.Count == 0)
                _testArmy.AddRange(new[] { UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue });
        }

        private void StartTestBattle()
        {
            var seed = Environment.TickCount;
            var army = _testArmy.Select(s => new ArmyMember(s)).ToList();
            var battle = _testEncounter.CreateBattle(army, new System.Random(seed), _testFought.ToList());
            if (_testOmen.HasValue)
                battle.AddOmen(_testOmen.Value.omen, _testOmen.Value.blessing ? Team.Player : Team.Enemy);

            _battleRoot = new GameObject("Test battle");
            _battleRoot.transform.SetParent(transform, false);
            var board = BoardView.Create(battle, _battleRoot.transform);
            FrameCamera(Camera.main, board);
            BattleScenery.Apply(Camera.main, board, BattleScenery.MainPalette, false, BattleScenery.MainSetting);
            var context = new BattleContext(0, 0, _testEncounter.Name, 0, seed, false, _testEncounter.IsBoss,
                _testEncounter.Layout, _testEncounter.LayoutRule, test: true, elite: _testEncounter.IsElite);
            _controller = _battleRoot.AddComponent<BattleController>();
            _controller.Init(board, Camera.main, context, () => Do(FinishTestBattle));
        }

        private void FinishTestBattle()
        {
            Destroy(_battleRoot);
            _battleRoot = null;
            _controller = null;
        }

        private void DrawTestSetup(float screenWidth)
        {
            const float w = 860;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 10, w, 700), GUI.skin.box);
            GUILayout.Label("Test battle", Title);
            GUILayout.Label("For testing only: any army, any encounter, any blessing or curse. Normal battle rules; no run, gold or map.");
            GUILayout.BeginHorizontal();

            // Left: every unit type, click to add (up to the army limit).
            GUILayout.BeginVertical(GUILayout.Width(300));
            GUILayout.Label($"Add a unit ({_testArmy.Count}/{Run.MaxArmySize}):");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(560));
            foreach (var type in UnitStats.AllPlayerTypes)
            {
                GUI.enabled = _testArmy.Count < Run.MaxArmySize;
                var pick = type;
                if (GUILayout.Button($"{type.Name} (tier {type.Tier}, {type.Root.Name} line)"))
                    Do(() => _testArmy.Add(pick));
                GUI.enabled = true;
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Middle: the army and the encounter.
            GUILayout.BeginVertical(GUILayout.Width(260));
            GUILayout.Label("Army (click to remove):");
            for (var i = 0; i < _testArmy.Count; i++)
            {
                var index = i;
                if (GUILayout.Button($"x  {_testArmy[i].Name}"))
                    Do(() => _testArmy.RemoveAt(index));
            }

            GUILayout.Space(10);
            GUILayout.Label("Encounter:");
            foreach (var enc in Encounter.Regular.Concat(Encounter.Elites).Concat(Encounter.Bosses).Concat(new[] { Encounter.MoonShrine, Encounter.BreakRitual }))
            {
                var pick = enc;
                if (GUILayout.Button((enc == _testEncounter ? "> " : "") + enc.Name + (enc.IsBoss ? " (boss)" : "")))
                    Do(() => _testEncounter = pick);
            }

            if (_testEncounter == Encounter.Necromancer)
            {
                GUILayout.Label("Factions already beaten (he raises them):");
                foreach (var enc in Encounter.Regular)
                {
                    var faction = enc;
                    var beaten = _testFought.Contains(enc);
                    if (GUILayout.Button((beaten ? "[X] " : "[  ] ") + enc.Name))
                        Do(() =>
                        {
                            if (!_testFought.Remove(faction))
                                _testFought.Add(faction);
                        });
                }
            }

            GUILayout.EndVertical();

            // Right: blessing or curse.
            GUILayout.BeginVertical();
            GUILayout.Label("Blessing or curse:");
            if (GUILayout.Button((_testOmen == null ? "> " : "") + "None"))
                Do(() => _testOmen = null);
            foreach (var blessing in new[] { true, false })
            foreach (var omen in Omens.All)
            {
                var pick = (omen, blessing);
                var chosen = _testOmen.HasValue && _testOmen.Value.omen == omen && _testOmen.Value.blessing == blessing;
                if (GUILayout.Button(new GUIContent((chosen ? "> " : "") + Omens.Name(omen, blessing), Omens.Describe(omen, blessing))))
                    Do(() => _testOmen = pick);
            }

            GUILayout.Label(GUI.tooltip, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back to the draft", GUILayout.Height(32), GUILayout.Width(200)))
                Do(Restart);
            GUI.enabled = _testArmy.Count > 0;
            if (GUILayout.Button("Start test battle", GUILayout.Height(32)))
                Do(StartTestBattle);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void LeaveShop()
        {
            _runLog.CloseShop();
            _run.LeaveShop();
            _lastNote = null;
        }

        public static void FrameCamera(Camera cam, BoardView board)
        {
            if (cam == null)
                return;
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            cam.backgroundColor = Background;
            cam.clearFlags = CameraClearFlags.SolidColor;
            var c = board.Centre;
            cam.transform.position = new Vector3(c.x, c.y + 0.2f, -10f);
        }

        // ---------- Screens ----------

        private void OnGUI()
        {
            if (_run == null || _battleRoot != null)
                return;
            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;

            if (_testMode)
            {
                DrawTestSetup(width);
                return;
            }

            switch (_run.State)
            {
                case RunState.Draft:
                    DrawDraft(width);
                    break;
                case RunState.Trainer:
                    DrawTrainer(width);
                    break;
                case RunState.Map:
                    DrawMap(width);
                    break;
                case RunState.Shop:
                    DrawShop(width);
                    break;
                case RunState.Event:
                    DrawEvent(width);
                    break;
                case RunState.ItemReward:
                    DrawItemReward(width);
                    break;
                case RunState.Blessing:
                    DrawBlessing(width);
                    break;
                case RunState.Won:
                case RunState.Lost:
                    DrawRunEnd(width);
                    break;
            }
        }

        private static GUIStyle Title => new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        private void DrawArmy()
        {
            GUILayout.Label("Your army:");
            foreach (var m in _run.Army)
            {
                var item = m.Equipment != null ? $"{m.Equipment.Name} ({m.Equipment.Effect})" : "no item";
                var bonus = "";
                if (m.BonusDamage > 0)
                    bonus += $", +{m.BonusDamage} damage";
                if (m.BonusMaxHp > 0)
                    bonus += $", +{m.BonusMaxHp} max HP";
                GUILayout.Label($"  {m.Stats.Name} (tier {m.Stats.Tier}): {item}{bonus}");
            }
        }

        /// <summary>One line describing a unit type: stats, abilities and actions.</summary>
        private static string Describe(UnitStats s)
        {
            var text = $"{s.Name}  HP {s.MaxHp}  DMG {s.Damage}  RNG {s.Range}  AP {s.MaxAp}\n{s.Role}";
            foreach (var a in s.Abilities)
                text += $"\n  - {Abilities.Name(a)}: {Abilities.Describe(a)}";
            foreach (var a in s.Actions)
                text += $"\n  - {Abilities.Name(a)} ({Battle.ActionCost(a)} AP): {Abilities.Describe(a)}";
            return text;
        }

        private Vector2 _scroll;

        private void DrawDraft(float screenWidth)
        {
            const float w = 600;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 20, w, 680), GUI.skin.box);
            GUILayout.Label("Choose your army", Title);
            GUILayout.Label($"Pick {Run.DraftSize} different units. Each can evolve twice at Trainers along the road.");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(540));
            foreach (var unit in _run.DraftPool)
            {
                var picked = _draftPicks.Contains(unit);
                var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.UpperLeft, wordWrap = true };
                if (GUILayout.Button((picked ? "[X] " : "[  ] ") + Describe(unit), style))
                    Do(() => ToggleDraftPick(unit));
            }

            GUILayout.EndScrollView();
            GUI.enabled = _draftPicks.Count == Run.DraftSize;
            if (GUILayout.Button($"Start the run ({_draftPicks.Count}/{Run.DraftSize})", GUILayout.Height(34)))
                Do(ConfirmDraft);
            GUI.enabled = true;
            if (TestModeAvailable && GUILayout.Button("Test battle (testing only: any army, encounter and omen)"))
                Do(EnterTestMode);
            GUILayout.EndArea();
        }

        private void DrawTrainer(float screenWidth)
        {
            const float w = 700;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 20, w, 680), GUI.skin.box);
            GUILayout.Label("Trainer", Title);
            var price = _run.NextEvolutionPrice;
            GUILayout.Label($"Each unit may go up one tier per visit, into one of its paths. The first evolution is free, then {Run.SecondEvolutionPrice} gold, then {Run.LaterEvolutionPrice} each.");
            GUILayout.Label(price == 0 ? $"Next evolution: free.   Gold: {_run.Gold}" : $"Next evolution: {price} gold.   Gold: {_run.Gold}");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(560));
            var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            foreach (var member in _run.Army)
            {
                GUILayout.Label($"{member.Stats.Name} (tier {member.Stats.Tier})", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                if (_run.HasEvolvedThisVisit(member))
                {
                    GUILayout.Label("  Evolved this visit.");
                    continue;
                }

                if (member.Stats.Evolutions.Count == 0)
                {
                    GUILayout.Label("  Fully evolved.");
                    continue;
                }

                GUILayout.BeginHorizontal();
                foreach (var into in member.Stats.Evolutions)
                {
                    var m = member;
                    var target = into;
                    GUI.enabled = _run.CanEvolve(member, into);
                    var pressed = GUILayout.Button(Describe(into), style, GUILayout.Width((w - 40) / member.Stats.Evolutions.Count));
                    GUI.enabled = true;
                    if (pressed)
                        Do(() => Evolve(m, target));
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            if (GUILayout.Button("Leave the trainer", GUILayout.Height(32)))
                Do(LeaveTrainer);
            GUILayout.EndArea();
        }

        private void DrawMap(float screenWidth)
        {
            const float w = 560;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 20, w, 680), GUI.skin.box);
            GUILayout.Label("Act 1", Title);
            GUILayout.Label($"Gold: {_run.Gold}    Beaten: {(_run.Fought.Count > 0 ? string.Join(", ", _run.Fought.Select(f => f.Name)) : "nobody yet")}");
            if (_run.Blessing.HasValue)
                GUILayout.Label(Omens.Name(_run.Blessing.Value, true));
            foreach (var curse in _run.Curses)
                GUILayout.Label(Omens.Name(curse, false));
            if (!string.IsNullOrEmpty(_lastNote))
                GUILayout.Label(_lastNote);
            GUILayout.Space(6);

            // Furthest step at the top, like climbing a tower.
            for (var step = Run.Steps - 1; step >= 0; step--)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{step + 1}.", GUILayout.Width(24));
                var nodes = _run.Map[step];
                for (var i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    var past = step < _run.StepIndex;
                    var chosen = past && _run.Chosen[step] == i;
                    var current = step == _run.StepIndex;
                    var label = chosen ? $"> {node.Label}" : node.Label;
                    GUI.enabled = current;
                    var oldColor = GUI.color;
                    if (past && !chosen)
                        GUI.color = new Color(1f, 1f, 1f, 0.35f);
                    var index = i;
                    if (GUILayout.Button(label, GUILayout.Height(current ? 30 : 22)) && current)
                        Do(() => ChooseNode(index));
                    GUI.color = oldColor;
                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.Label("? = something unexpected. Shops sell items and a recruit. Trainers evolve every unit one tier, for free.");
            GUILayout.Label("Moon Shrine: defend it for 5 enemy turns to earn a blessing. If it breaks, you take a curse.");
            GUILayout.Label("Break the Ritual: smash 3 totems before enemy turn 4 to earn an item. If the summoning completes, you take a curse.");
            GUILayout.Label("Battle: a faction you haven't beaten (a rematch once you've met them all). Elite: a harder rematch of a faction you beat; pays an item too. Battles and elites show their faction when you reach their step.");
            DrawArmy();
            GUILayout.EndArea();
        }

        private void DrawShop(float screenWidth)
        {
            const float w = 560;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 30, w, 660), GUI.skin.box);
            GUILayout.Label("Shop", Title);
            GUILayout.Label($"Gold: {_run.Gold}");
            GUILayout.Label("Each unit has one slot. Buying replaces (and loses) the old item.");
            GUILayout.Space(8);

            GUILayout.Label("For sale (click one):");
            if (_run.Offers.Count == 0)
                GUILayout.Label("  Sold out.");
            foreach (var offer in _run.Offers.ToList())
            {
                var marker = offer == _selectedOffer ? "> " : "";
                GUI.enabled = _run.Gold >= offer.Price;
                if (GUILayout.Button($"{marker}{offer.Name}   {offer.Effect}   {offer.Price} gold"))
                    _selectedOffer = offer;
                GUI.enabled = true;
            }

            if (_run.ShopHasRecruit)
            {
                GUILayout.Space(6);
                GUI.enabled = _run.CanBuyRecruit;
                var recruit = _run.ShopRecruit;
                if (GUILayout.Button($"Recruit a {recruit.Name} ({recruit.Role})   {Run.RecruitPrice} gold"))
                    Do(() =>
                    {
                        if (_run.BuyRecruit())
                            _runLog.RecordRecruit(recruit.Name, Run.RecruitPrice);
                    });

                GUI.enabled = true;
            }

            GUILayout.Space(8);
            if (_selectedOffer != null && _run.Offers.Contains(_selectedOffer))
            {
                GUILayout.Label($"Give the {_selectedOffer.Name} to:");
                foreach (var member in _run.Army)
                {
                    var current = member.Equipment != null ? $"replaces {member.Equipment.Name}" : "empty slot";
                    var reason = !_selectedOffer.CanEquip(member.Stats) ? "  (can't use it)" : "";
                    GUI.enabled = _run.CanBuy(_selectedOffer, member);
                    if (GUILayout.Button($"{member.Stats.Name}   ({current}){reason}"))
                    {
                        var buyer = member;
                        var offer = _selectedOffer;
                        Do(() =>
                        {
                            var replaced = buyer.Equipment;
                            if (_run.Buy(offer, buyer))
                            {
                                _runLog.RecordPurchase(offer, buyer, replaced);
                                _selectedOffer = null;
                            }
                        });
                    }

                    GUI.enabled = true;
                }
            }
            else
            {
                DrawArmy();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Leave the shop", GUILayout.Height(32)))
                Do(LeaveShop);
            GUILayout.EndArea();
        }

        private void DrawEvent(float screenWidth)
        {
            const float w = 520;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 60, w, 560), GUI.skin.box);
            switch (_run.CurrentEvent)
            {
                case EventKind.WanderingRecruit:
                    GUILayout.Label($"A Wandering {_run.EventRecruit.Name}", Title);
                    GUILayout.Label("A dusty statue offers to join you.\n" + Describe(_run.EventRecruit));
                    if (_run.Army.Count < Run.MaxArmySize)
                    {
                        if (GUILayout.Button($"Welcome aboard (the {_run.EventRecruit.Name} joins)", GUILayout.Height(32)))
                            Do(() =>
                            {
                                _run.AcceptRecruit();
                                AfterEvent("recruit");
                            });
                    }
                    else if (GUILayout.Button($"Your army is full. Point the way (+{Run.FullArmyGold} gold)", GUILayout.Height(32)))
                    {
                        Do(() =>
                        {
                            _run.AcceptRecruit();
                            AfterEvent("gold");
                        });
                    }

                    break;

                case EventKind.AbandonedCart:
                    GUILayout.Label("An Abandoned Cart", Title);
                    GUILayout.Label($"Inside: {_run.CartItem.Name} ({_run.CartItem.Effect}).");
                    foreach (var member in _run.Army)
                    {
                        var current = member.Equipment != null ? $"replaces {member.Equipment.Name}" : "empty slot";
                        GUI.enabled = _run.CartItem.CanEquip(member.Stats);
                        if (GUILayout.Button($"Give it to the {member.Stats.Name} ({current})"))
                        {
                            var taker = member;
                            Do(() =>
                            {
                                var item = _run.CartItem.Name;
                                _run.TakeCartItem(taker);
                                AfterEvent($"{item} -> {taker.Stats.Name}");
                            });
                        }

                        GUI.enabled = true;
                    }

                    if (GUILayout.Button("Leave it"))
                        Do(() =>
                        {
                            _run.TakeCartItem(null);
                            AfterEvent("leave");
                        });

                    break;

                case EventKind.Gambler:
                    GUILayout.Label("The Gambler", Title);
                    GUILayout.Label($"\"Flip a coin? {Run.GambleCost} gold in, {Run.GambleWin} out if you win.\"   You have {_run.Gold} gold.");
                    GUI.enabled = _run.CanGamble;
                    if (GUILayout.Button($"Play ({Run.GambleCost} gold)", GUILayout.Height(32)))
                        Do(() =>
                        {
                            var won = _run.Gamble(true);
                            AfterEvent(won ? "play: won" : "play: lost");
                        });

                    GUI.enabled = true;
                    if (GUILayout.Button("Walk away"))
                        Do(() =>
                        {
                            _run.Gamble(false);
                            AfterEvent("walk away");
                        });

                    break;

                case EventKind.Ambush:
                    GUILayout.Label("Ambush!", Title);
                    GUILayout.Label(_run.AmbushEncounter != null
                        ? $"The {_run.AmbushEncounter.Name} jumps you on the road. Win and they drop an extra {Run.AmbushBonusGold} gold."
                        : "Someone was lying in wait, but they look at your army and think better of it.");
                    if (GUILayout.Button(_run.AmbushEncounter != null ? "Fight!" : $"Pick up the {Run.AmbushBonusGold * 2} gold", GUILayout.Height(32)))
                        Do(() =>
                        {
                            _run.StartAmbush();
                            AfterEvent("fight");
                        });

                    break;

                case EventKind.Shrine:
                    GUILayout.Label("A Quiet Shrine", Title);
                    GUILayout.Label($"One statue may kneel here and come away sturdier (+{Run.ShrineHp} max HP for the run).");
                    foreach (var member in _run.Army)
                    {
                        if (GUILayout.Button($"The {member.Stats.Name} kneels"))
                        {
                            var kneeler = member;
                            Do(() =>
                            {
                                _run.PrayAtShrine(kneeler);
                                AfterEvent(kneeler.Stats.Name);
                            });
                        }
                    }

                    break;
            }

            GUILayout.FlexibleSpace();
            DrawArmy();
            GUILayout.EndArea();
        }

        private void DrawBlessing(float screenWidth)
        {
            const float w = 520;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 60, w, 560), GUI.skin.box);
            GUILayout.Label("The Moon Shrine held", Title);
            if (!string.IsNullOrEmpty(_lastNote))
                GUILayout.Label(_lastNote);
            GUILayout.Label("Choose a blessing. It stays with your army for the rest of the run.");
            var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            foreach (var omen in _run.BlessingChoices)
            {
                var pick = omen;
                if (GUILayout.Button($"{Omens.Name(omen, true)}\n{Omens.Describe(omen, true)}", style, GUILayout.Height(70)))
                    Do(() =>
                    {
                        if (!_run.ChooseBlessing(pick))
                            return;
                        _runLog.RecordOmen(pick, true);
                        _lastNote = $"{Omens.Name(pick, true)}: {Omens.Describe(pick, true)}";
                    });
            }

            GUILayout.FlexibleSpace();
            DrawArmy();
            GUILayout.EndArea();
        }

        private Equipment _selectedReward;

        private void DrawItemReward(float screenWidth)
        {
            const float w = 560;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 40, w, 620), GUI.skin.box);
            var elite = _run.CurrentEncounter != null && _run.CurrentEncounter.IsElite;
            GUILayout.Label(elite ? $"{_run.CurrentEncounter.Name} beaten" : "The ritual is broken", Title);
            if (!string.IsNullOrEmpty(_lastNote))
                GUILayout.Label(_lastNote);
            GUILayout.Label((elite ? "They left their loot behind." : "The guards left their loot behind.") +
                            " Pick one item for one unit (it replaces that unit's item).");
            GUILayout.Space(6);
            foreach (var item in _run.RewardItems)
            {
                var marker = item == _selectedReward ? "> " : "";
                if (GUILayout.Button($"{marker}{item.Name}   {item.Effect}"))
                    _selectedReward = item;
            }

            GUILayout.Space(8);
            if (_selectedReward != null && _run.RewardItems.Contains(_selectedReward))
            {
                GUILayout.Label($"Give the {_selectedReward.Name} to:");
                foreach (var member in _run.Army)
                {
                    var current = member.Equipment != null ? $"replaces {member.Equipment.Name}" : "empty slot";
                    GUI.enabled = _run.CanTakeRewardItem(_selectedReward, member);
                    if (GUILayout.Button($"{member.Stats.Name}   ({current})"))
                    {
                        var taker = member;
                        var item = _selectedReward;
                        Do(() =>
                        {
                            var replaced = taker.Equipment;
                            if (!_run.TakeRewardItem(item, taker))
                                return;
                            _runLog.RecordRewardItem(item, taker, replaced);
                            _lastNote = $"The {taker.Stats.Name} takes the {item.Name}.";
                            _selectedReward = null;
                        });
                    }

                    GUI.enabled = true;
                }
            }
            else
            {
                DrawArmy();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Leave the items", GUILayout.Height(32)))
                Do(() =>
                {
                    _run.SkipRewardItem();
                    _runLog.RecordRewardItem(null, null, null);
                    _lastNote = null;
                    _selectedReward = null;
                });
            GUILayout.EndArea();
        }

        private void DrawRunEnd(float screenWidth)
        {
            const float w = 340;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 140, w, 220), GUI.skin.box);
            var centred = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label(_run.State == RunState.Won ? "Run won!" : "Run lost", Title);
            GUILayout.Label(_run.State == RunState.Won
                ? $"The ogre is rubble. Gold left: {_run.Gold}"
                : $"Defeated at step {_run.StepIndex + 1}: {_run.CurrentEncounter?.Name}", centred);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("New run", GUILayout.Height(32)))
                Do(Restart);
            GUILayout.EndArea();
        }
    }
}
