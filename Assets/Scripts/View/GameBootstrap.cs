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
        private Technique _selectedOffer;
        private Equipment _selectedItem;
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
            _selectedItem = null;
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
                case RunState.Map:
                    DrawMap(width);
                    break;
                case RunState.Shop:
                    DrawShop(width);
                    break;
                case RunState.Event:
                    DrawEvent(width);
                    break;
                case RunState.Reward:
                    DrawReward(width);
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
                var techniques = m.Techniques.Count > 0 ? string.Join(", ", m.Techniques.Select(t => t.Name)) : "no techniques";
                var bonus = "";
                if (m.LevelHp + m.BonusMaxHp > 0)
                    bonus += $", +{m.LevelHp + m.BonusMaxHp} max HP";
                if (m.LevelDamage + m.BonusDamage > 0)
                    bonus += $", +{m.LevelDamage + m.BonusDamage} damage";
                if (m.LevelMines > 0)
                    bonus += $", +{m.LevelMines} mine";
                var item = m.Equipment != null ? $" [{m.Equipment.Name}]" : "";
                GUILayout.Label($"  {m.Stats.Name} (level {m.Level}){item}: {techniques}{bonus}");
            }
        }

        /// <summary>One button's text for a technique: name, rarity, line, requirement and rule.</summary>
        private static string Describe(Technique t, bool price = false)
        {
            var tags = new System.Collections.Generic.List<string> { t.Rarity == TechniqueRarity.Rare ? "rare" : "common", t.IsShared ? "any unit" : t.Line.Name };
            if (t.Requires != null)
                tags.Add($"needs {t.Requires.Name}");
            if (t.Action.HasValue)
                tags.Add($"action, cooldown {Battle.Cooldown(t.Action.Value)}");
            var cost = price ? $"   {t.Price} gold" : "";
            return $"{t.Name}  ({string.Join(", ", tags)}){cost}\n{t.Description}";
        }

        /// <summary>Who can learn a technique: one button per army member, disabled when it can't.</summary>
        private void DrawLearners(Technique technique, Func<ArmyMember, bool> can, Action<ArmyMember> learn)
        {
            foreach (var member in _run.Army)
            {
                var why = technique.CanLearn(member) ? ""
                    : member.Knows(technique) ? "  (knows it)"
                    : technique.Requires != null && !member.Knows(technique.Requires) && (technique.IsShared || member.Stats.Root == technique.Line) ? $"  (needs {technique.Requires.Name})"
                    : "  (other line)";
                GUI.enabled = can(member);
                var m = member;
                if (GUILayout.Button($"{member.Stats.Name} (level {member.Level} -> {member.Level + 1}){why}"))
                    Do(() => learn(m));
                GUI.enabled = true;
            }
        }

        /// <summary>One line describing a unit type: stats, abilities and actions.</summary>
        private static string Describe(UnitStats s)
        {
            var text = $"{s.Name}  HP {s.MaxHp}  DMG {s.Damage}  RNG {s.Range}  AP {s.MaxAp}  MV {s.Moves}\n{s.Role}";
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
            GUILayout.Label($"Pick {Run.DraftSize} different units. Each won fight lets one of them learn a technique (and go up a level).");
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
            GUILayout.Label("? = something unexpected. Shops sell techniques, items, a recruit, and help forgetting a technique. Every won fight offers a technique pick.");
            GUILayout.Label("Moon Shrine: defend it for 5 enemy turns to earn a blessing. If it breaks, you take a curse.");
            GUILayout.Label("Break the Ritual: smash 3 totems before enemy turn 4 to earn a technique pick. If the summoning completes, you take a curse.");
            GUILayout.Label("Battle: a faction you haven't beaten (a rematch once you've met them all). Elite: a harder rematch of a faction you beat; its pick has a rare technique. Battles and elites show their faction when you reach their step.");
            DrawArmy();
            GUILayout.EndArea();
        }

        private void DrawShop(float screenWidth)
        {
            const float w = 560;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 30, w, 660), GUI.skin.box);
            GUILayout.Label("Shop", Title);
            GUILayout.Label($"Gold: {_run.Gold}");
            GUILayout.Label("A unit that learns a technique goes up a level. Each unit has one item slot; a new item replaces the old.");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(520));

            GUILayout.Label("Techniques for sale (click one):");
            if (_run.Offers.Count == 0)
                GUILayout.Label("  Sold out.");
            var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            foreach (var offer in _run.Offers.ToList())
            {
                var marker = offer == _selectedOffer ? "> " : "";
                GUI.enabled = _run.Gold >= offer.Price;
                if (GUILayout.Button(marker + Describe(offer, true), style))
                {
                    _selectedOffer = offer;
                    _selectedItem = null;
                }

                GUI.enabled = true;
            }

            GUILayout.Space(6);
            GUILayout.Label("Items for sale (click one):");
            if (_run.ItemOffers.Count == 0)
                GUILayout.Label("  Sold out.");
            foreach (var item in _run.ItemOffers.ToList())
            {
                var marker = item == _selectedItem ? "> " : "";
                GUI.enabled = _run.Gold >= item.Price;
                if (GUILayout.Button($"{marker}{item.Name}   {item.Effect}   {item.Price} gold"))
                {
                    _selectedItem = item;
                    _selectedOffer = null;
                }

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
                GUILayout.Label($"Who learns {_selectedOffer.Name}?");
                var offer = _selectedOffer;
                DrawLearners(offer, m => _run.CanBuy(offer, m), buyer =>
                {
                    if (_run.Buy(offer, buyer))
                    {
                        _runLog.RecordPurchase(offer, buyer);
                        _selectedOffer = null;
                    }
                });
            }
            else if (_selectedItem != null && _run.ItemOffers.Contains(_selectedItem))
            {
                GUILayout.Label($"Give the {_selectedItem.Name} to:");
                foreach (var member in _run.Army)
                {
                    var current = member.Equipment != null ? $"replaces {member.Equipment.Name}" : "empty slot";
                    GUI.enabled = _run.CanBuyItem(_selectedItem, member);
                    if (GUILayout.Button($"{member.Stats.Name}   ({current})"))
                    {
                        var buyer = member;
                        var item = _selectedItem;
                        Do(() =>
                        {
                            var replaced = buyer.Equipment;
                            if (_run.BuyItem(item, buyer))
                            {
                                _runLog.RecordItemPurchase(item, buyer, replaced);
                                _selectedItem = null;
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

            // Forgetting: frees a unit of a technique it doesn't want; the level stays.
            if (_run.Army.Any(m => m.Techniques.Count > 0))
            {
                GUILayout.Space(8);
                GUILayout.Label($"Forget a technique ({Run.ForgetPrice} gold; the level stays, and techniques that need it go too):");
                foreach (var member in _run.Army.Where(m => m.Techniques.Count > 0))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(member.Stats.Name, GUILayout.Width(110));
                    foreach (var technique in member.Techniques.ToList())
                    {
                        GUI.enabled = _run.CanForget(member, technique);
                        var m = member;
                        var t = technique;
                        if (GUILayout.Button(technique.Name))
                            Do(() =>
                            {
                                if (_run.Forget(m, t))
                                    _runLog.RecordForget(t, m);
                            });
                        GUI.enabled = true;
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
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
                    var cart = _run.CartTechnique;
                    if (cart != null)
                    {
                        GUILayout.Label("Inside, an old training manual:\n" + Describe(cart));
                        GUILayout.Label("Who learns it?");
                        DrawLearners(cart, cart.CanLearn, taker =>
                        {
                            _run.TakeCart(taker);
                            AfterEvent($"{cart.Name} -> {taker.Stats.Name}");
                        });
                    }
                    else
                    {
                        var cartItem = _run.CartItem;
                        GUILayout.Label($"Inside: {cartItem.Name} ({cartItem.Effect}).");
                        foreach (var member in _run.Army)
                        {
                            var current = member.Equipment != null ? $"replaces {member.Equipment.Name}" : "empty slot";
                            GUI.enabled = cartItem.CanEquip(member.Stats);
                            var taker = member;
                            if (GUILayout.Button($"Give it to the {member.Stats.Name} ({current})"))
                                Do(() =>
                                {
                                    _run.TakeCart(taker);
                                    AfterEvent($"{cartItem.Name} -> {taker.Stats.Name}");
                                });
                            GUI.enabled = true;
                        }
                    }

                    if (GUILayout.Button("Leave it"))
                        Do(() =>
                        {
                            _run.TakeCart(null);
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

        private Technique _selectedReward;

        /// <summary>After a won fight: pick one technique for one unit, or skip it for gold.</summary>
        private void DrawReward(float screenWidth)
        {
            const float w = 560;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 40, w, 640), GUI.skin.box);
            var elite = _run.CurrentEncounter != null && _run.CurrentEncounter.IsElite;
            GUILayout.Label(elite ? $"{_run.CurrentEncounter.Name} beaten" : "Victory", Title);
            if (!string.IsNullOrEmpty(_lastNote))
                GUILayout.Label(_lastNote);
            GUILayout.Label("Pick a technique for one unit (it goes up a level), or skip it for gold." + (elite ? " An elite's pick holds a rare one." : ""));
            GUILayout.Space(6);
            var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            foreach (var technique in _run.RewardOffers)
            {
                var marker = technique == _selectedReward ? "> " : "";
                if (GUILayout.Button(marker + Describe(technique), style))
                    _selectedReward = technique;
            }

            GUILayout.Space(8);
            if (_selectedReward != null && _run.RewardOffers.Contains(_selectedReward))
            {
                GUILayout.Label($"Who learns {_selectedReward.Name}?");
                var pick = _selectedReward;
                DrawLearners(pick, m => _run.CanTakeReward(pick, m), taker =>
                {
                    var offered = _run.RewardOffers.ToList();
                    if (!_run.TakeReward(pick, taker))
                        return;
                    _runLog.RecordReward(offered, pick, taker);
                    _lastNote = $"The {taker.Stats.Name} learns {pick.Name} (level {taker.Level}).";
                    _selectedReward = null;
                });
            }
            else
            {
                DrawArmy();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button($"Skip (+{Run.SkipGold} gold)", GUILayout.Height(32)))
                Do(() =>
                {
                    var offered = _run.RewardOffers.ToList();
                    _run.SkipReward();
                    _runLog.RecordReward(offered, null, null);
                    _lastNote = $"Skipped the pick: +{Run.SkipGold} gold.";
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
