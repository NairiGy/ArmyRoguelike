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
                    // With Mercenaries, the hire screen comes first and starts the battle.
                    if (!_run.MercPending)
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
            else if (_draftPicks.Count < _run.DraftCount)
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
            if (_lastNote != null && _run.LastLoserGains.Count > 0)
                _lastNote += $" Loser: {string.Join(", ", _run.LastLoserGains.Select(m => m.Stats.Name))} " +
                             $"+{Banners.LoserHp} max HP, +{Banners.LoserDamage} damage.";
            var cracked = _run.LastCracked.Where(m => !_run.LastShattered.Contains(m)).ToList();
            if (_lastNote != null && cracked.Count > 0)
                _lastNote += $" Cracked: {string.Join(", ", cracked.Select(m => $"{m.Stats.Name} ({m.Cracks}/{Run.CracksToShatter})"))}.";
            if (_lastNote != null && _run.LastShattered.Count > 0)
                _lastNote += $" Shattered for good: {string.Join(", ", _run.LastShattered.Select(m => m.Stats.Name))}.";
            if (_run.State == RunState.Won || _run.State == RunState.Lost)
                _runLog.Save();
        }

        /// <summary>After an event choice: log it, then follow wherever the run went (map or ambush battle).</summary>
        private void AfterEvent(string choice)
        {
            _runLog.RecordEvent(choice, _run.EventOutcome);
            _lastNote = _run.EventOutcome;
            if (_run.State == RunState.Battle && !_run.MercPending)
                StartBattle();
        }

        // ---------- Banners ----------

        private void HireMerc(UnitStats unit)
        {
            var offered = _run.MercOffers.ToList();
            if (unit != null && !_run.HireMerc(unit))
                return;
            if (unit == null)
                _run.DeclineMerc();
            _runLog.RecordMerc(offered, unit);
            StartBattle();
        }

        private readonly System.Collections.Generic.List<ArmyMember> _keepPicks = new System.Collections.Generic.List<ArmyMember>();

        private void ConfirmDismiss()
        {
            var keep = _keepPicks.ToList();
            _runLog.RecordDismiss(keep);
            var gone = _run.Army.Where(m => !keep.Contains(m)).Select(m => m.Stats.Name).ToList();
            if (!_run.Dismiss(keep))
                return;
            _keepPicks.Clear();
            _lastNote = $"{string.Join(" and ", gone)} {(gone.Count == 1 ? "leaves" : "leave")} the army.";
        }

        // ---------- Test mode ----------
        //
        // Editor and dev builds only: any army, any encounter and any blessing or curse, straight into
        // the battle with no run around it. Afterwards it comes back to the setup screen.

        private static bool TestModeAvailable => Application.isEditor || Debug.isDebugBuild;

        private bool _testMode;
        private readonly System.Collections.Generic.List<ArmyMember> _testArmy = new System.Collections.Generic.List<ArmyMember>();

        // Index in the test army of the unit whose techniques are shown, or -1.
        private int _testSelected = -1;
        private Encounter _testEncounter = Encounter.MoonShrine;
        private readonly System.Collections.Generic.List<Encounter> _testFought = new System.Collections.Generic.List<Encounter>();

        // Null for no omen; otherwise the omen and whether it's a blessing (true) or a curse.
        private (Omen omen, bool blessing)? _testOmen;

        // Null for no battlefield.
        private Battlefield? _testField;

        // Banners that change the battle itself (Loser and Mercenaries work between fights, so they aren't here).
        private static readonly Banner[] TestableBanners = { Banner.Vampiric, Banner.ThirdWheel, Banner.BatteringRam };
        private readonly System.Collections.Generic.HashSet<Banner> _testBanners = new System.Collections.Generic.HashSet<Banner>();

        private void EnterTestMode()
        {
            _testMode = true;
            // Test battles aren't part of a run, so no run log is written for this one.
            _runLog = null;
            if (_testArmy.Count == 0)
                _testArmy.AddRange(new[] { UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue }.Select(s => new ArmyMember(s)));
        }

        private void StartTestBattle()
        {
            var seed = Environment.TickCount;
            var army = _testArmy.ToList();
            var battle = _testEncounter.CreateBattle(army, new System.Random(seed), _testFought.ToList());
            if (_testOmen.HasValue)
                battle.AddOmen(_testOmen.Value.omen, _testOmen.Value.blessing ? Team.Player : Team.Enemy);
            foreach (var banner in _testBanners)
                battle.AddBanner(banner);
            battle.SetField(_testField);

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

        /// <summary>
        /// Learns a technique, or drops a known one. Dropping rebuilds the unit from its other techniques (and
        /// those that needed the dropped one go too), so its level always equals what it knows.
        /// </summary>
        private void ToggleTestTechnique(int index, Technique technique)
        {
            var member = _testArmy[index];
            if (!member.Knows(technique))
            {
                member.Learn(technique);
                return;
            }

            var keep = member.Techniques.Where(t => t != technique && t.Requires != technique).ToArray();
            _testArmy[index] = new ArmyMember(member.Stats, member.Equipment, keep);
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
            GUILayout.Label("For testing only: any army with any techniques, any encounter, any blessing or curse, any battle banners, any battlefield. Normal battle rules; no run, gold or map.");
            GUILayout.BeginHorizontal();

            // Left: every unit type, click to add (up to the army limit); below, the chosen unit's techniques.
            GUILayout.BeginVertical(GUILayout.Width(300));
            GUILayout.Label($"Add a unit ({_testArmy.Count}/{Run.MaxArmySize}):");
            foreach (var type in UnitStats.BaseUnits)
            {
                GUI.enabled = _testArmy.Count < Run.MaxArmySize;
                var pick = type;
                if (GUILayout.Button(type.Name))
                    Do(() =>
                    {
                        _testArmy.Add(new ArmyMember(pick));
                        _testSelected = _testArmy.Count - 1;
                    });
                GUI.enabled = true;
            }

            GUILayout.Space(10);
            if (_testSelected >= 0 && _testSelected < _testArmy.Count)
            {
                var member = _testArmy[_testSelected];
                GUILayout.Label($"{member.Stats.Name}'s techniques (click to learn or drop; level {member.Level}):");
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(400));
                foreach (var technique in Technique.All.Where(t => t.Line == member.Stats))
                {
                    var known = member.Knows(technique);
                    var needs = !known && technique.Requires != null && !member.Knows(technique.Requires) ? $"  (needs {technique.Requires.Name})" : "";
                    GUI.enabled = known || technique.CanLearn(member);
                    var t = technique;
                    var index = _testSelected;
                    if (GUILayout.Button(new GUIContent($"{(known ? "[X]" : "[  ]")} {technique.Name}{needs}", technique.Description)))
                        Do(() => ToggleTestTechnique(index, t));
                    GUI.enabled = true;
                }

                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Label("Click a unit in the army to give it techniques.");
            }

            GUILayout.EndVertical();

            // Middle: the army and the encounter.
            GUILayout.BeginVertical(GUILayout.Width(260));
            GUILayout.Label("Army (click a unit for its techniques, x to remove):");
            for (var i = 0; i < _testArmy.Count; i++)
            {
                var index = i;
                var m = _testArmy[i];
                GUILayout.BeginHorizontal();
                var marker = i == _testSelected ? "> " : "";
                if (GUILayout.Button($"{marker}{m.Stats.Name} (level {m.Level})"))
                    Do(() => _testSelected = index);
                if (GUILayout.Button("x", GUILayout.Width(28)))
                    Do(() =>
                    {
                        _testArmy.RemoveAt(index);
                        if (_testSelected >= _testArmy.Count)
                            _testSelected = _testArmy.Count - 1;
                    });
                GUILayout.EndHorizontal();
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

            GUILayout.Space(10);
            GUILayout.Label("Banners:");
            foreach (var banner in TestableBanners)
            {
                var pick = banner;
                if (GUILayout.Button(new GUIContent((_testBanners.Contains(banner) ? "[X] " : "[  ] ") + Banners.Name(banner), Banners.Describe(banner))))
                    Do(() =>
                    {
                        if (!_testBanners.Remove(pick))
                            _testBanners.Add(pick);
                    });
            }

            GUILayout.Space(10);
            GUILayout.Label("Battlefield:");
            if (GUILayout.Button((_testField == null ? "> " : "") + "None"))
                Do(() => _testField = null);
            foreach (var field in Battlefields.All)
            {
                var pick = field;
                if (GUILayout.Button(new GUIContent((_testField == field ? "> " : "") + Battlefields.Name(field), Battlefields.Describe(field))))
                    Do(() => _testField = pick);
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
            HudArt.ApplyFont();
            if (_run == null || _battleRoot != null)
                return;
            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            DrawScreen(width);
            KeywordText.DrawTooltip(width, ReferenceHeight);
        }

        private void DrawScreen(float width)
        {
            if (_testMode)
            {
                DrawTestSetup(width);
                return;
            }

            if (_abandoning)
            {
                DrawRunEnd(width);
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
                case RunState.Battle:
                    if (_run.MercPending)
                        DrawMercs(width);
                    break;
                case RunState.Dismiss:
                    DrawDismiss(width);
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

        /// <summary>A plain word-wrapped label, for rule texts drawn with <see cref="KeywordText"/>.</summary>
        private static GUIStyle WrapLabel => new GUIStyle(GUI.skin.label) { wordWrap = true };

        /// <summary>
        /// A full-width button in GUILayout flow whose text shows its keywords (hover one for its meaning). Sized to
        /// the text, or at least <paramref name="minHeight"/>. True when clicked.
        /// </summary>
        private static bool KeywordButton(string text, GUIStyle style, float width, float minHeight = 0f)
        {
            var pad = style.padding;
            var inner = width - pad.horizontal;
            var height = Mathf.Max(minHeight, KeywordText.Height(text, style, inner) + pad.vertical);
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(true));
            var clicked = GUI.Button(rect, GUIContent.none, style);
            var textStyle = new GUIStyle(style);
            textStyle.normal.textColor = GUI.enabled ? style.normal.textColor : new Color(style.normal.textColor.r, style.normal.textColor.g, style.normal.textColor.b, 0.5f);
            KeywordText.Draw(new Rect(rect.x + pad.left, rect.y + pad.top, rect.width - pad.horizontal, rect.height - pad.vertical), text, textStyle);
            return clicked;
        }

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
                if (m.Cracks > 0)
                    bonus += $", {CrackText(m)}";
                var item = m.Equipment != null ? $" [{m.Equipment.Name}]" : "";
                GUILayout.Label($"  {m.Stats.Name} (level {m.Level}){item}: {techniques}{bonus}");
            }
        }

        /// <summary>"2 cracks (-2 max HP, 1 more shatters it)".</summary>
        private static string CrackText(ArmyMember m) =>
            $"{m.Cracks} crack{(m.Cracks == 1 ? "" : "s")} (-{m.Cracks * Run.CrackHp} max HP{(m.Cracks == Run.CracksToShatter - 1 ? ", one more shatters it" : "")})";

        /// <summary>One button's text for a technique: name, rarity, line, requirement and rule.</summary>
        private static string Describe(Technique t)
        {
            var tags = new System.Collections.Generic.List<string> { t.Rarity == TechniqueRarity.Rare ? "rare" : "common", t.IsShared ? "any unit" : t.Line.Name };
            if (t.Requires != null)
                tags.Add($"needs {t.Requires.Name}");
            if (t.Action.HasValue)
                tags.Add($"action, cooldown {Battle.Cooldown(t.Action.Value)}");
            return $"{t.Name}  ({string.Join(", ", tags)})\n{t.Description}";
        }

        /// <summary>Who can learn a technique: one button per army member, disabled when it can't.</summary>
        private void DrawLearners(Technique technique, Func<ArmyMember, bool> can, Action<ArmyMember> learn)
        {
            foreach (var member in _run.Army)
            {
                var why = WhyNot(technique, member);
                GUI.enabled = can(member);
                var m = member;
                var level = member.WouldLevelUp(technique) ? $"level {member.Level} -> {member.Level + 1}" : $"level {member.Level}, relearning";
                if (GUILayout.Button($"{member.Stats.Name} ({level}){(why.Length > 0 ? $"  ({why})" : "")}"))
                    Do(() => learn(m));
                GUI.enabled = true;
            }
        }

        /// <summary>Why a member can't learn a technique, or "" when it can.</summary>
        private static string WhyNot(Technique technique, ArmyMember member) =>
            technique.CanLearn(member) ? ""
            : member.Knows(technique) ? "knows it"
            : technique.Requires != null && !member.Knows(technique.Requires) && (technique.IsShared || member.Stats == technique.Line) ? $"needs {technique.Requires.Name}"
            : "other line";

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

        /// <summary>The start banner picked on the draft screen, taken when the run starts.</summary>
        private Banner? _startPick;

        /// <summary>Units to draft with a given start banner: 3, or the army limit (Third Wheel: 2).</summary>
        private static int DraftCountWith(Banner? banner) =>
            Math.Min(Run.DraftSize, banner == Banner.ThirdWheel ? Banners.ThirdWheelArmySize : Run.MaxArmySize);

        private int DraftTarget => _run.StartBannerChosen ? _run.DraftCount : DraftCountWith(_startPick);

        private const float DraftWidth = 1060f;
        private const float StatueHeight = 96f;

        /// <summary>
        /// The run's start: pick one banner (top row) and the units (cards below) on one screen, then start the run.
        /// </summary>
        private void DrawDraft(float screenWidth)
        {
            if (_detailUnit != null)
            {
                DrawHeroDetail(screenWidth);
                return;
            }

            EnsureShopStyles();
            var w = Mathf.Min(DraftWidth, screenWidth - 20f);
            var inner = w - 2 * ShopPad;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 10, w, ReferenceHeight - 20), HudArt.PanelStyle(HudArt.PanelColor, ShopPad));
            _shopTitle.normal.textColor = Color.white;
            GUILayout.Label("Prepare your army", _shopTitle);
            GUILayout.Space(4);

            var banners = _run.StartBannerChosen ? new Banner[0] : _run.StartBanners.ToArray();
            if (banners.Length > 0)
            {
                Section("BANNER  ·  pick 1, it bends a rule for your whole army all run", GoldColor);
                var cardWidth = (inner - CardGap * (banners.Length - 1)) / banners.Length;
                var row = GUILayoutUtility.GetRect(inner, banners.Max(b => BannerCardHeight(b, null, cardWidth)), GUILayout.ExpandWidth(false));
                for (var i = 0; i < banners.Length; i++)
                {
                    var pick = banners[i];
                    DrawBannerCard(new Rect(row.x + i * (cardWidth + CardGap), row.y, cardWidth, row.height), pick, null, () => PickStartBanner(pick), _startPick == pick);
                }

                GUILayout.Space(12);
            }

            var target = DraftTarget;
            Section($"UNITS  ·  pick {target}  ({_draftPicks.Count}/{target})  ·  each won fight teaches one of them a technique", GoldColor);
            var pool = _run.DraftPool.ToList();
            var unitWidth = (inner - CardGap * (pool.Count - 1)) / pool.Count;
            var unitHeight = pool.Max(u => UnitCardHeight(u, unitWidth));
            var rows = GUILayoutUtility.GetRect(inner, unitHeight, GUILayout.ExpandWidth(false));
            for (var i = 0; i < pool.Count; i++)
                DrawUnitCard(new Rect(rows.x + i * (unitWidth + CardGap), rows.y, unitWidth, rows.height), pool[i]);

            GUILayout.FlexibleSpace();
            var missing = banners.Length > 0 && !_startPick.HasValue ? "Pick a banner"
                : _draftPicks.Count < target ? $"Pick {target - _draftPicks.Count} more unit{(target - _draftPicks.Count == 1 ? "" : "s")}"
                : "Start the run";
            GUI.enabled = missing == "Start the run";
            if (GUILayout.Button(missing, GUILayout.Height(36)))
                Do(StartRun);
            GUI.enabled = true;
            if (TestModeAvailable && GUILayout.Button("Test battle (testing only: any army, encounter and omen)"))
                Do(EnterTestMode);
            GUILayout.EndArea();
        }

        /// <summary>Picks (or switches) the start banner; drops the last unit picks if it lowers the count.</summary>
        private void PickStartBanner(Banner banner)
        {
            _startPick = banner;
            while (_draftPicks.Count > DraftTarget)
                _draftPicks.RemoveAt(_draftPicks.Count - 1);
        }

        /// <summary>Takes the picked banner, then drafts the picked units.</summary>
        private void StartRun()
        {
            if (!_run.StartBannerChosen && _startPick.HasValue && _run.ChooseStartBanner(_startPick.Value))
                _runLog.RecordBanner(_startPick.Value, "start", _run.StartBanners);
            ConfirmDraft();
        }

        /// <summary>A unit's kit for its draft card: (name and cost, rule text) per passive and action.</summary>
        private static System.Collections.Generic.List<(string name, string text)> DraftKit(UnitStats s)
        {
            var kit = new System.Collections.Generic.List<(string, string)>();
            foreach (var a in s.Abilities)
                kit.Add((Abilities.Name(a), Capitalise(Abilities.Short(a))));
            foreach (var a in s.Actions)
                kit.Add(($"{Abilities.Name(a)}  ·  {Battle.ActionCost(a)} AP", Capitalise(Abilities.Short(a))));
            return kit;
        }

        private static string Capitalise(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        private float UnitCardHeight(UnitStats s, float width)
        {
            var inner = width - 20;
            var h = 8 + StatueHeight + 6 + 22 + 10 + 4 + 16 + _cardTag.CalcHeight(new GUIContent(s.Role), inner) + 6;
            foreach (var (_, text) in DraftKit(s))
                h += 18 + KeywordText.Height(text, _cardText, inner) + 6;
            return h + 6 + MoreInfoHeight + 6;
        }

        private const float MoreInfoHeight = 24f;

        /// <summary>A draft card: the statue, name, HP in pieces, stats, role, then each passive and action.</summary>
        private void DrawUnitCard(Rect r, UnitStats s)
        {
            var picked = _draftPicks.Contains(s);
            var full = !picked && _draftPicks.Count >= DraftTarget;
            var old = GUI.color;
            if (full)
                GUI.color = Faded;
            // More info sits on the card, so the card itself doesn't take clicks over it.
            var info = new Rect(r.x + 10, r.yMax - 8 - MoreInfoHeight, r.width - 20, MoreInfoHeight);
            if (Card(r, picked, !info.Contains(Event.current.mousePosition)))
                Do(() => ToggleDraftPick(s));
            GUI.color = old;
            if (GUI.Button(info, "More info"))
                Do(() => OpenDetail(s));
            if (full)
                GUI.color = Faded;
            var x = r.x + 10;
            var inner = r.width - 20;
            var y = r.y + 8;
            DrawStatue(new Rect(x, y, inner, StatueHeight), s.Kind);
            y += StatueHeight + 6;
            Label(new Rect(x, y, inner, 22), s.Name, _cardName, picked ? GoldColor : Color.white);
            y += 22;

            // HP in pieces, like the bars over the statues.
            if (Event.current.type == EventType.Repaint)
            {
                const float gap = 2f;
                var piece = (inner - gap * (s.MaxHp - 1)) / s.MaxHp;
                for (var i = 0; i < s.MaxHp; i++)
                    GUI.DrawTexture(new Rect(x + i * (piece + gap), y, piece, 10f), HudArt.Solid(new Color(0.35f, 0.8f, 0.4f)));
            }

            y += 14;
            Label(new Rect(x, y, inner, 16), $"HP {s.MaxHp}  ·  DMG {s.Damage}  ·  RNG {s.Range}  ·  MV {s.Moves}", _cardTag, DimText);
            y += 16;
            var roleHeight = _cardTag.CalcHeight(new GUIContent(s.Role), inner);
            Label(new Rect(x, y, inner, roleHeight), s.Role, _cardTag, DimText);
            y += roleHeight + 6;
            _cardText.normal.textColor = TextColor;
            foreach (var (name, text) in DraftKit(s))
            {
                Label(new Rect(x, y, inner, 18), name, _section, Color.white);
                y += 18;
                var h = KeywordText.Height(text, _cardText, inner);
                KeywordText.Draw(new Rect(x, y, inner, h), text, _cardText);
                y += h + 6;
            }

            GUI.color = old;
        }

        // ---------- Hero detail (More info on a draft card) ----------

        /// <summary>The unit whose detail screen is open from the draft, or null.</summary>
        private UnitStats _detailUnit;

        private Vector2 _detailScroll;

        /// <summary>The skill under the pointer last frame: it and the skills it needs or unlocks get a gold edge.</summary>
        private string _hoveredSkill;
        private string _hoveredSkillNext;

        private static readonly Color StartColor = new Color(0.55f, 0.85f, 1f);
        private static readonly Color HpGrowthColor = new Color(0.35f, 0.8f, 0.4f);

        /// <summary>A skill on the detail screen.</summary>
        private sealed class DetailSkill
        {
            public string Name, Tags, Text, Requires;
            public Color Color;

            /// <summary>Drawn under the skill it needs, with a connector (its requirement is in the same column).</summary>
            public bool Indented;
        }

        /// <summary>
        /// A unit's detail screen: its statue and stats, what each level gives, and every skill it can have, split into
        /// passive and active and coloured by source (starting kit, common or rare). Skills that need another sit
        /// under it; hovering a skill lights up what it needs and what it unlocks. Arrows (or the buttons) switch hero.
        /// </summary>
        private void DrawHeroDetail(float screenWidth)
        {
            EnsureShopStyles();
            var s = _detailUnit;
            var pool = _run.DraftPool.ToList();
            var index = Mathf.Max(0, pool.IndexOf(s));
            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow))
            {
                var next = pool[(index + (e.keyCode == KeyCode.LeftArrow ? pool.Count - 1 : 1)) % pool.Count];
                Do(() => OpenDetail(next));
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Do(() => _detailUnit = null);
                e.Use();
            }

            var w = Mathf.Min(DraftWidth, screenWidth - 20f);
            var inner = w - 2 * ShopPad;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 10, w, ReferenceHeight - 20), HudArt.PanelStyle(HudArt.PanelColor, ShopPad));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Back", GUILayout.Width(90), GUILayout.Height(30)))
                Do(() => _detailUnit = null);
            GUILayout.Space(12);
            _shopTitle.normal.textColor = Color.white;
            GUILayout.Label(s.Name, _shopTitle);
            GUILayout.FlexibleSpace();
            var prev = pool[(index + pool.Count - 1) % pool.Count];
            var after = pool[(index + 1) % pool.Count];
            if (GUILayout.Button($"< {prev.Name}", GUILayout.Width(150), GUILayout.Height(30)))
                Do(() => OpenDetail(prev));
            if (GUILayout.Button($"{after.Name} >", GUILayout.Width(150), GUILayout.Height(30)))
                Do(() => OpenDetail(after));
            GUILayout.EndHorizontal();
            _cardText.normal.textColor = DimText;
            GUILayout.Label(s.Role, _cardText);
            GUILayout.Space(8);

            const float leftWidth = 290f;
            var area = GUILayoutUtility.GetRect(inner, ReferenceHeight - 20 - 2 * ShopPad - 80, GUILayout.ExpandWidth(false));
            DrawDetailStats(new Rect(area.x, area.y, leftWidth, area.height), s);

            // Skills on the right, passive and active side by side, scrolling if they don't fit.
            _hoveredSkill = _hoveredSkillNext;
            if (e.type == EventType.Repaint)
                _hoveredSkillNext = null;
            var skills = new Rect(area.x + leftWidth + 16, area.y, area.width - leftWidth - 16, area.height);
            var (passive, active) = DetailSkills(s);
            const float columnGap = 14f, scrollBar = 16f;
            var column = (skills.width - scrollBar - columnGap) * 0.5f;
            var contentHeight = Mathf.Max(SkillColumnHeight(passive, column), SkillColumnHeight(active, column)) + 30;
            _detailScroll = GUI.BeginScrollView(skills, _detailScroll, new Rect(0, 0, skills.width - scrollBar, contentHeight));
            DrawLegend(new Rect(0, 0, skills.width - scrollBar, 20));
            var all = passive.Concat(active).ToList();
            DrawSkillColumn(new Rect(0, 26, column, contentHeight - 26), "PASSIVE", passive, all);
            DrawSkillColumn(new Rect(column + columnGap, 26, column, contentHeight - 26), "ACTIVE", active, all);
            GUI.EndScrollView();
            GUILayout.EndArea();
        }

        private void OpenDetail(UnitStats unit)
        {
            _detailUnit = unit;
            _detailScroll = Vector2.zero;
            _hoveredSkill = _hoveredSkillNext = null;
        }

        /// <summary>A stat icon, tinted, with its value beside it and its name dim after.</summary>
        private float StatRow(float x, float y, HudArt.Icon icon, Color color, string value, string name)
        {
            if (Event.current.type == EventType.Repaint)
            {
                var old = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(new Rect(x, y + 1, 16, 16), HudArt.IconTexture(icon));
                GUI.color = old;
            }

            Label(new Rect(x + 24, y, 60, 20), value, _cardName, Color.white);
            Label(new Rect(x + 70, y + 2, 160, 18), name, _cardText, DimText);
            return y + 22;
        }

        /// <summary>Left column: the statue, the stats and what each level gives.</summary>
        private void DrawDetailStats(Rect r, UnitStats s)
        {
            GUI.DrawTexture(r, HudArt.Solid(HudArt.CardColor));
            var x = r.x + 12;
            var inner = r.width - 24;
            var y = r.y + 10;
            DrawStatue(new Rect(x, y, inner, 160), s.Kind);
            y += 168;

            if (Event.current.type == EventType.Repaint)
            {
                const float gap = 2f;
                var piece = (inner - gap * (s.MaxHp - 1)) / s.MaxHp;
                for (var i = 0; i < s.MaxHp; i++)
                    GUI.DrawTexture(new Rect(x + i * (piece + gap), y, piece, 10f), HudArt.Solid(HpGrowthColor));
            }

            y += 18;
            y = StatRow(x, y, HudArt.Icon.Heart, HpGrowthColor, s.MaxHp.ToString(), "HP");
            y = StatRow(x, y, HudArt.Icon.Sword, new Color(1f, 0.6f, 0.35f), s.Damage.ToString(), "damage");
            y = StatRow(x, y, HudArt.Icon.Target, new Color(0.9f, 0.85f, 0.5f), s.Range.ToString(), s.Range == 1 ? "range (next to it)" : "range");
            y = StatRow(x, y, HudArt.Icon.Boot, new Color(0.6f, 0.8f, 1f), s.Moves.ToString(), s.Moves == 1 ? "move a turn" : "moves a turn");
            y = StatRow(x, y, HudArt.Icon.Diamond, new Color(1f, 0.85f, 0.25f), s.MaxAp.ToString(), "AP a turn");
            y = StatRow(x, y, HudArt.Icon.Weight, new Color(0.75f, 0.75f, 0.8f), s.Weight.ToString().ToLowerInvariant(), "weight");

            // Level growth as a strip: one cell per level, marked where damage (or a mine) comes.
            y += 10;
            _section.normal.textColor = GoldColor;
            GUI.Label(new Rect(x, y, inner, 18), "LEVEL GROWTH", _section);
            y += 20;
            var maxLevel = Technique.All.Count(t => t.Line == s);
            var damageLevels = Growth.DamageLevels(s);
            var mineLevel = Growth.MineLevel(s);
            const float cellGap = 2f, cellHeight = 34f;
            var cell = (inner - cellGap * (maxLevel - 1)) / maxLevel;
            var number = new GUIStyle(_cardTag) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            for (var level = 1; level <= maxLevel; level++)
            {
                var c = new Rect(x + (level - 1) * (cell + cellGap), y, cell, cellHeight);
                GUI.DrawTexture(c, HudArt.Solid(new Color(0.18f, 0.18f, 0.22f)));
                // Every level: HP (green strip at the bottom).
                GUI.DrawTexture(new Rect(c.x, c.yMax - 4, c.width, 4), HudArt.Solid(HpGrowthColor));
                number.normal.textColor = DimText;
                GUI.Label(new Rect(c.x, c.y + 1, c.width, 14), level.ToString(), number);
                if (damageLevels.Contains(level) && Event.current.type == EventType.Repaint)
                {
                    var old = GUI.color;
                    GUI.color = new Color(1f, 0.6f, 0.35f);
                    GUI.DrawTexture(new Rect(c.center.x - 6, c.y + 15, 12, 12), HudArt.IconTexture(HudArt.Icon.Sword));
                    GUI.color = old;
                }
                else if (level == mineLevel)
                    GUI.DrawTexture(new Rect(c.center.x - 4, c.y + 17, 8, 8), HudArt.Solid(new Color(0.6f, 0.8f, 1f)));
            }

            y += cellHeight + 6;
            var legend = $"Every level +{Growth.HpPerLevel(s)} max HP (green)";
            if (damageLevels.Count > 0)
                legend += ", sword: +1 damage";
            if (mineLevel > 0)
                legend += ", blue: +1 mine on the board";
            legend += $". At level {maxLevel}: HP {s.MaxHp + Growth.Hp(s, maxLevel)}, damage {s.Damage + Growth.Damage(s, maxLevel)}.";
            _cardTag.normal.textColor = DimText;
            var legendHeight = _cardTag.CalcHeight(new GUIContent(legend), inner);
            GUI.Label(new Rect(x, y, inner, legendHeight), legend, _cardTag);
            y += legendHeight + 4;
            GUI.Label(new Rect(x, y, inner, 30), "A unit goes up a level each time it learns a new technique.", _cardTag);
        }

        /// <summary>
        /// Every skill the unit can have: its starting kit, then its line's techniques, common before rare, each one
        /// that needs another right under it when both are in the same column.
        /// </summary>
        private static (System.Collections.Generic.List<DetailSkill> passive, System.Collections.Generic.List<DetailSkill> active) DetailSkills(UnitStats s)
        {
            var passive = new System.Collections.Generic.List<DetailSkill>();
            var active = new System.Collections.Generic.List<DetailSkill>();
            foreach (var (name, text) in Abilities.PassivesOf(s))
                passive.Add(new DetailSkill { Name = name, Tags = "Starts with it", Text = Capitalise(text), Color = StartColor });
            foreach (var (name, cost, kind, text) in Abilities.ActionsOf(s, s.Damage))
            {
                var tags = kind.HasValue ? $"Starts with it  ·  {cost} AP  ·  cooldown {Battle.Cooldown(kind.Value)}" : $"Starts with it  ·  {cost} AP  ·  basic attack";
                active.Add(new DetailSkill { Name = name, Tags = tags, Text = Capitalise(text), Color = StartColor });
            }

            var line = Technique.All.Where(t => t.Line == s).OrderBy(t => t.Rarity).ToList();
            bool IsActive(Technique t) => t.Action.HasValue;
            DetailSkill Make(Technique t, bool indented)
            {
                var rare = t.Rarity == TechniqueRarity.Rare;
                var tags = rare ? "Rare" : "Common";
                if (t.Action.HasValue)
                    tags += $"  ·  {Battle.ActionCost(t.Action.Value)} AP  ·  cooldown {Battle.Cooldown(t.Action.Value)}";
                if (t.Requires != null)
                    tags += $"  ·  needs {t.Requires.Name}";
                return new DetailSkill
                {
                    Name = t.Name, Tags = tags, Text = Capitalise(t.Description), Requires = t.Requires?.Name,
                    Color = rare ? RareColor : TextColor, Indented = indented,
                };
            }

            // Roots first (no requirement, or one in the other column), each followed by what it unlocks in its column.
            void Add(Technique t, bool indented)
            {
                (IsActive(t) ? active : passive).Add(Make(t, indented));
                foreach (var child in line.Where(c => c.Requires == t && IsActive(c) == IsActive(t)))
                    Add(child, true);
            }

            foreach (var t in line.Where(t => t.Requires == null || IsActive(t.Requires) != IsActive(t)))
                Add(t, false);
            return (passive, active);
        }

        private const float Indent = 18f;

        private float SkillHeight(DetailSkill skill, float width)
        {
            var inner = width - (skill.Indented ? Indent : 0) - 16;
            return 20 + _cardTag.CalcHeight(new GUIContent(skill.Tags), inner) + 2 + KeywordText.Height(skill.Text, _cardText, inner) + 14;
        }

        private float SkillColumnHeight(System.Collections.Generic.List<DetailSkill> skills, float width) =>
            22 + skills.Sum(k => SkillHeight(k, width) + 6);

        private void DrawSkillColumn(Rect r, string title, System.Collections.Generic.List<DetailSkill> skills, System.Collections.Generic.List<DetailSkill> all)
        {
            _section.normal.textColor = GoldColor;
            GUI.Label(new Rect(r.x, r.y, r.width, 18), $"{title}  ({skills.Count})", _section);
            var hovered = all.FirstOrDefault(k => k.Name == _hoveredSkill);
            var y = r.y + 22;
            var parentY = y;
            foreach (var skill in skills)
            {
                var h = SkillHeight(skill, r.width);
                var indent = skill.Indented ? Indent : 0f;
                var card = new Rect(r.x + indent, y, r.width - indent, h);
                if (card.Contains(Event.current.mousePosition))
                    _hoveredSkillNext = skill.Name;

                // Hovering a skill lights up it, what it needs and what needs it.
                var linked = hovered != null && (skill == hovered || skill.Name == hovered.Requires || skill.Requires == hovered.Name);
                if (linked)
                    GUI.DrawTexture(new Rect(card.x - 2, card.y - 2, card.width + 4, card.height + 4), HudArt.Solid(GoldColor));
                if (skill.Indented)
                {
                    // Connector from the skill above.
                    var line = new Color(0.4f, 0.4f, 0.45f);
                    GUI.DrawTexture(new Rect(r.x + 6, parentY, 2, y + 12 - parentY), HudArt.Solid(line));
                    GUI.DrawTexture(new Rect(r.x + 6, y + 11, Indent - 6, 2), HudArt.Solid(line));
                }
                else
                    parentY = y + h;

                GUI.DrawTexture(card, HudArt.Solid(HudArt.CardColor));
                GUI.DrawTexture(new Rect(card.x, card.y, 3f, card.height), HudArt.Solid(skill.Color));
                var x = card.x + 10;
                var inner = card.width - 16;
                var ty = card.y + 6;
                Label(new Rect(x, ty, inner, 20), skill.Name, _cardName, skill.Color);
                ty += 20;
                var tagHeight = _cardTag.CalcHeight(new GUIContent(skill.Tags), inner);
                Label(new Rect(x, ty, inner, tagHeight), skill.Tags, _cardTag, DimText);
                ty += tagHeight + 2;
                _cardText.normal.textColor = TextColor;
                KeywordText.Draw(new Rect(x, ty, inner, card.yMax - ty), skill.Text, _cardText);
                y += h + 6;
                if (skill.Indented)
                    parentY = y - 6;
            }
        }

        /// <summary>What the skill colours mean.</summary>
        private void DrawLegend(Rect r)
        {
            var x = r.x;
            foreach (var (name, color) in new[] { ("Starting kit", StartColor), ("Common technique", TextColor), ("Rare technique", RareColor) })
            {
                GUI.DrawTexture(new Rect(x, r.y + 5, 10, 10), HudArt.Solid(color));
                _cardTag.normal.textColor = color;
                var width = _cardTag.CalcSize(new GUIContent(name)).x;
                GUI.Label(new Rect(x + 14, r.y + 2, width + 4, 18), name, _cardTag);
                x += 14 + width + 20;
            }

            _cardTag.normal.textColor = DimText;
            GUI.Label(new Rect(x, r.y + 2, r.xMax - x, 18), "Hover a skill to light up what it needs and unlocks.  ← → switch hero", _cardTag);
        }

        /// <summary>
        /// The banners held, side by side in the panel's top right corner beside the title, where they take no room from
        /// the map's list; hovering one puts its name and rule in the map's tooltip line.
        /// </summary>
        private void DrawBannerRow(float panelWidth)
        {
            const float height = 42f, cell = 26f, gap = 3f, inset = 6f;
            var x = panelWidth - inset - _run.Banners.Count * (cell + gap) + gap;
            foreach (var banner in Banners.All.Where(_run.Banners.Contains))
            {
                var rect = new Rect(x, inset, cell, height);
                GUI.DrawTexture(rect, HudArt.Solid(rect.Contains(Event.current.mousePosition) ? HudArt.CardHoverColor : HudArt.CardColor));
                BannerView.DrawCardArt(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), banner);
                GUI.Button(rect, new GUIContent("", $"{Banners.Name(banner)}: {Banners.Describe(banner)}"), GUIStyle.none);
                x += cell + gap;
            }
        }

        private Vector2 _mapScroll;

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
            DrawBannerRow(w);
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
                    if (node.Field.HasValue)
                        label += $"  ·  {Battlefields.Name(node.Field.Value)}";
                    var rule = node.Field.HasValue ? $"{Battlefields.Name(node.Field.Value)}: {Battlefields.Describe(node.Field.Value)}" : "";
                    if (node.Champion != Affix.None)
                    {
                        label += $"  ·  {Champions.Name(node.Champion)} champion";
                        rule += (rule.Length > 0 ? "\n" : "") + Champions.Rule(node.Champion);
                    }

                    // Tooltips only work on enabled controls, so future steps stay enabled but ignore clicks.
                    GUI.enabled = current || (!past && node.Field.HasValue);
                    var oldColor = GUI.color;
                    if (past && !chosen)
                        GUI.color = new Color(1f, 1f, 1f, 0.35f);
                    else if (!current && !past)
                        GUI.color = new Color(1f, 1f, 1f, 0.6f);
                    var index = i;
                    if (GUILayout.Button(new GUIContent(label, rule), GUILayout.Height(current ? 30 : 22)) && current)
                        Do(() => ChooseNode(index));
                    GUI.color = oldColor;
                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();
            }

            // The hovered node's or banner's rule, with its keywords explained underneath (the pointer is on the node).
            var tip = string.IsNullOrEmpty(GUI.tooltip) ? "Every battle, elite and boss has a battlefield rule, named on its node. Hover one, or a banner, to read it." : GUI.tooltip;
            KeywordText.LayoutLabel(tip, WrapLabel, w - 12);
            var glossary = string.IsNullOrEmpty(GUI.tooltip) ? "" : KeywordText.Glossary(GUI.tooltip);
            if (glossary.Length > 0)
                GUILayout.Label(glossary, new GUIStyle(WrapLabel) { fontSize = 12 });
            GUILayout.Space(8);
            // The army and the help scroll, so a big army can't push the button below off the panel.
            _mapScroll = GUILayout.BeginScrollView(_mapScroll, GUILayout.ExpandHeight(true));
            DrawArmy();
            GUILayout.Space(8);
            GUILayout.Label("? = something unexpected. Shops sell techniques, items, a recruit, and help forgetting a technique. Every won fight offers a technique pick.");
            GUILayout.Label("Moon Shrine: defend it for 5 enemy turns to earn a blessing. If it breaks, you take a curse.");
            GUILayout.Label("Break the Ritual: smash 3 totems before enemy turn 4 to earn a technique pick. If the summoning completes, you take a curse.");
            GUILayout.Label($"From step {Champions.FirstStep + 1}, battles and elites bring a champion (hover the node). A statue that falls in a fight is cracked: -{Run.CrackHp} max HP until a shop mends it, and {Run.CracksToShatter} cracks shatter it for good. Bosses grow with your army's levels and fall in {Core.Battle.BossPhases} phases, one a round at most.");
            GUILayout.Label("Battle: a faction you haven't beaten (a rematch once you've met them all). Elite: a harder rematch of a faction you beat; its pick has a rare technique. Battles and elites show their faction when you reach their step.");
            GUILayout.EndScrollView();
            GUILayout.Space(4);
            if (GUILayout.Button("Abandon run", GUILayout.Width(120)))
                Do(() => _abandoning = true);
            GUILayout.EndArea();
        }

        // ---------- Shop screen ----------
        // Offers are cards in rows; picking one turns the army row at the bottom into "who gets it?".

        private const float ShopWidth = 940f;
        private const int ShopPad = 14;
        private const float CardGap = 10f;
        private const float MemberCardHeight = 146f;

        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.35f);
        private static readonly Color TooDearColor = new Color(1f, 0.45f, 0.4f);
        private static readonly Color RareColor = new Color(0.78f, 0.62f, 1f);
        private static readonly Color TextColor = new Color(0.9f, 0.9f, 0.9f);
        private static readonly Color DimText = new Color(0.62f, 0.62f, 0.67f);
        private static readonly Color Faded = new Color(1f, 1f, 1f, 0.5f);

        private GUIStyle _shopTitle, _cardName, _cardText, _cardTag, _section, _price, _chip;

        private void EnsureShopStyles()
        {
            if (_shopTitle != null)
                return;
            _shopTitle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _cardName = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip };
            _cardText = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, padding = new RectOffset() };
            _cardTag = new GUIStyle(_cardText) { fontSize = 12, richText = true };
            _section = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _price = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            _chip = new GUIStyle(GUI.skin.button) { fontSize = 11, fixedHeight = 20, padding = new RectOffset(8, 8, 2, 2) };
        }

        private static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private void Section(string text, Color color)
        {
            _section.normal.textColor = color;
            GUILayout.Label(text, _section);
        }

        /// <summary>Draws a card's background (lit on hover, gold-edged when selected) and returns whether it was clicked.</summary>
        private static bool Card(Rect rect, bool selected, bool clickable)
        {
            var hover = clickable && rect.Contains(Event.current.mousePosition);
            GUI.DrawTexture(rect, HudArt.Solid(selected ? HudArt.CardAimColor : hover ? HudArt.CardHoverColor : HudArt.CardColor));
            if (selected)
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f), HudArt.Solid(GoldColor));
            return clickable && GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void Price(Rect card, int price)
        {
            Label(new Rect(card.x + 10, card.yMax - 26, card.width - 20, 20), $"{price} gold", _price, _run.Gold >= price ? GoldColor : TooDearColor);
        }

        /// <summary>A unit's statue, fitted into the area and standing on its bottom edge.</summary>
        private static void DrawStatue(Rect area, UnitKind kind)
        {
            var sprite = PlaceholderArt.Statue(kind);
            if (sprite == null)
                return;
            var tex = sprite.texture;
            var uv = sprite.textureRect;
            var w = Mathf.Min(area.width, area.height * uv.width / uv.height);
            var h = w * uv.height / uv.width;
            GUI.DrawTextureWithTexCoords(new Rect(area.center.x - w * 0.5f, area.yMax - h, w, h), tex,
                new Rect(uv.x / tex.width, uv.y / tex.height, uv.width / tex.width, uv.height / tex.height));
        }

        private static string TechniqueTags(Technique t)
        {
            var rarity = t.Rarity == TechniqueRarity.Rare ? $"<color=#{ColorUtility.ToHtmlStringRGB(RareColor)}>Rare</color>" : "Common";
            var tags = new System.Collections.Generic.List<string> { rarity, t.IsShared ? "any unit" : t.Line.Name };
            if (t.Requires != null)
                tags.Add($"needs {t.Requires.Name}");
            if (t.Action.HasValue)
                tags.Add($"action, cooldown {Battle.Cooldown(t.Action.Value)}");
            return string.Join("  ·  ", tags);
        }

        /// <summary>Height of an offer card: name, tag line, rule text and the price row.</summary>
        private float OfferHeight(string tags, string text, float width) =>
            8 + 22 + _cardTag.CalcHeight(new GUIContent(tags), width - 20) + 4 + KeywordText.Height(text, _cardText, width - 20) + 30;

        private void DrawOffer(Rect r, string name, string tags, string text, int price, bool selected, Action select)
        {
            var old = GUI.color;
            var affordable = _run.Gold >= price;
            if (!affordable)
                GUI.color = Faded;
            if (Card(r, selected, affordable))
                Do(select);
            var x = r.x + 10;
            var inner = r.width - 20;
            var y = r.y + 8;
            Label(new Rect(x, y, inner, 22), name, _cardName, Color.white);
            y += 22;
            var tagHeight = _cardTag.CalcHeight(new GUIContent(tags), inner);
            Label(new Rect(x, y, inner, tagHeight), tags, _cardTag, DimText);
            y += tagHeight + 4;
            _cardText.normal.textColor = TextColor;
            KeywordText.Draw(new Rect(x, y, inner, r.yMax - 28 - y), text, _cardText);
            Price(r, price);
            GUI.color = old;
        }

        private const string BannerTag = "Banner, for the whole army";
        private const float BannerArtWidth = 34f;

        /// <summary>Height of a wide banner card: its rule text beside the painted banner, at least tall enough to show it.</summary>
        private float BannerCardHeight(Banner banner, int? price, float width)
        {
            var textWidth = width - 20 - BannerArtWidth - 10;
            var text = 8 + 22 + _cardTag.CalcHeight(new GUIContent(BannerTag), textWidth) + 4 +
                       KeywordText.Height(Banners.Describe(banner), _cardText, textWidth) + (price.HasValue ? 30 : 8);
            return Mathf.Max(84f, text);
        }

        /// <summary>A wide banner card: the painted banner on the left, then its name, what it is, its rule and any price.</summary>
        private void DrawBannerCard(Rect r, Banner banner, int? price, Action select, bool selected = false)
        {
            var old = GUI.color;
            var affordable = !price.HasValue || _run.Gold >= price.Value;
            if (!affordable)
                GUI.color = Faded;
            if (Card(r, selected, affordable))
                Do(select);
            BannerView.DrawCardArt(new Rect(r.x + 10, r.y + 8, BannerArtWidth, r.height - 16), banner);
            var x = r.x + 10 + BannerArtWidth + 10;
            var inner = r.xMax - 10 - x;
            var y = r.y + 8;
            Label(new Rect(x, y, inner, 22), Banners.Name(banner), _cardName, Color.white);
            y += 22;
            var tagHeight = _cardTag.CalcHeight(new GUIContent(BannerTag), inner);
            Label(new Rect(x, y, inner, tagHeight), BannerTag, _cardTag, DimText);
            y += tagHeight + 4;
            _cardText.normal.textColor = TextColor;
            KeywordText.Draw(new Rect(x, y, inner, r.yMax - (price.HasValue ? 28 : 8) - y), Banners.Describe(banner), _cardText);
            if (price.HasValue)
                Price(r, price.Value);
            GUI.color = old;
        }

        /// <summary>A row of offer cards, three to a row's width so cards keep their size as they sell.</summary>
        private void OfferRow<T>(System.Collections.Generic.IReadOnlyList<T> offers, float width, Func<T, float, float> height, Action<Rect, T> draw)
        {
            const int columns = 3;
            var cardWidth = (width - CardGap * (columns - 1)) / columns;
            var row = GUILayoutUtility.GetRect(width, offers.Max(o => height(o, cardWidth)), GUILayout.ExpandWidth(false));
            for (var i = 0; i < offers.Count; i++)
                draw(new Rect(row.x + i * (cardWidth + CardGap), row.y, cardWidth, row.height), offers[i]);
        }

        private void DrawShop(float screenWidth)
        {
            EnsureShopStyles();
            var w = Mathf.Min(ShopWidth, screenWidth - 20f);
            var inner = w - 2 * ShopPad;
            var scrollInner = inner - 18f;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 10, w, ReferenceHeight - 20), HudArt.PanelStyle(HudArt.PanelColor, ShopPad));

            GUILayout.BeginHorizontal();
            _shopTitle.normal.textColor = Color.white;
            GUILayout.Label("Shop", _shopTitle);
            GUILayout.FlexibleSpace();
            _shopTitle.normal.textColor = GoldColor;
            GUILayout.Label($"{_run.Gold} gold", _shopTitle);
            GUILayout.EndHorizontal();
            _cardText.normal.textColor = DimText;
            GUILayout.Label("A unit that learns a new technique goes up a level; relearning a forgotten one doesn't. Each unit has one item slot; a new item replaces the old.", _cardText);
            GUILayout.Space(8);

            _scroll = GUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.ExpandHeight(true));

            Section("Techniques", GoldColor);
            var techniques = _run.Offers.ToList();
            if (techniques.Count == 0)
                Section("Sold out.", DimText);
            else
                OfferRow(techniques, scrollInner, (t, cw) => OfferHeight(TechniqueTags(t), t.Description, cw),
                    (r, t) => DrawOffer(r, t.Name, TechniqueTags(t), t.Description, t.Price, t == _selectedOffer, () =>
                    {
                        _selectedOffer = t;
                        _selectedItem = null;
                    }));

            GUILayout.Space(10);
            Section("Items", GoldColor);
            var items = _run.ItemOffers.ToList();
            if (items.Count == 0)
                Section("Sold out.", DimText);
            else
                OfferRow(items, scrollInner, (e, cw) => OfferHeight("Item", e.Effect, cw),
                    (r, e) => DrawOffer(r, e.Name, "Item", e.Effect, e.Price, e == _selectedItem, () =>
                    {
                        _selectedItem = e;
                        _selectedOffer = null;
                    }));

            if (_run.ShopBanner.HasValue)
            {
                GUILayout.Space(10);
                Section("Banner", GoldColor);
                var banner = _run.ShopBanner.Value;
                OfferRow(new[] { banner }, scrollInner, (b, cw) => BannerCardHeight(b, Banners.Price, cw),
                    (r, b) => DrawBannerCard(r, b, Banners.Price, () =>
                    {
                        if (_run.BuyBanner())
                            _runLog.RecordBanner(b, "shop");
                    }));
            }

            var forgetters = _run.Army.Where(m => m.Techniques.Count > 0).ToList();
            if (_run.ShopHasRecruit || forgetters.Count > 0)
            {
                GUILayout.Space(10);
                Section("Services", GoldColor);
                var forgetHeight = 8 + 22 + 20 + forgetters.Count * 24 + 30;
                var row = GUILayoutUtility.GetRect(scrollInner, Mathf.Max(112f, forgetHeight), GUILayout.ExpandWidth(false));
                var recruitWidth = (scrollInner - CardGap * 2) / 3;
                var forgetRect = row;
                if (_run.ShopHasRecruit)
                {
                    DrawRecruitCard(new Rect(row.x, row.y, recruitWidth, row.height));
                    forgetRect = new Rect(row.x + recruitWidth + CardGap, row.y, row.width - recruitWidth - CardGap, row.height);
                }

                if (forgetters.Count > 0)
                    DrawForgetCard(forgetRect, forgetters);
            }

            var honers = _run.Army.Where(m => m.AllActions.Any(a => Battle.Cooldown(a) > 1 && !m.IsHoned(a))).ToList();
            if (honers.Count > 0)
            {
                GUILayout.Space(CardGap);
                var honeRect = GUILayoutUtility.GetRect(scrollInner, 8 + 22 + 20 + honers.Count * 24 + 30, GUILayout.ExpandWidth(false));
                DrawHoneCard(honeRect, honers);
            }

            var cracked = _run.Army.Where(m => m.Cracks > 0).ToList();
            if (cracked.Count > 0)
            {
                GUILayout.Space(CardGap);
                var mendRect = GUILayoutUtility.GetRect(scrollInner, 8 + 22 + 20 + cracked.Count * 24 + 30, GUILayout.ExpandWidth(false));
                DrawMendCard(mendRect, cracked);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(10);
            Section(_selectedOffer != null ? $"Who learns {_selectedOffer.Name}?"
                : _selectedItem != null ? $"Who gets the {_selectedItem.Name}?"
                : "Your army", _selectedOffer != null || _selectedItem != null ? GoldColor : TextColor);
            var armyRow = GUILayoutUtility.GetRect(inner, MemberCardHeight, GUILayout.ExpandWidth(false));
            var memberWidth = (inner - CardGap * (Run.MaxArmySize - 1)) / Run.MaxArmySize;
            for (var i = 0; i < _run.Army.Count; i++)
                DrawMemberCard(new Rect(armyRow.x + i * (memberWidth + CardGap), armyRow.y, memberWidth, armyRow.height), _run.Army[i]);

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Leave the shop", GUILayout.Width(220), GUILayout.Height(34)))
                Do(LeaveShop);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawRecruitCard(Rect r)
        {
            var recruit = _run.ShopRecruit;
            var can = _run.CanBuyRecruit;
            var old = GUI.color;
            if (!can)
                GUI.color = Faded;
            if (Card(r, false, can))
                Do(() =>
                {
                    if (_run.BuyRecruit())
                        _runLog.RecordRecruit(recruit.Name, Run.RecruitPrice);
                });
            DrawStatue(new Rect(r.x + 6, r.y + 8, 56, r.height - 16), recruit.Kind);
            var x = r.x + 68;
            var inner = r.xMax - 10 - x;
            Label(new Rect(x, r.y + 8, inner, 22), $"Recruit a {recruit.Name}", _cardName, Color.white);
            Label(new Rect(x, r.y + 30, inner, r.height - 60), recruit.Role, _cardText, TextColor);
            if (_run.Army.Count >= _run.ArmyLimit)
                Label(new Rect(x, r.yMax - 26, inner, 20), "Army is full", _cardText, TooDearColor);
            Price(r, Run.RecruitPrice);
            GUI.color = old;
        }

        // Forgetting: frees a unit of a technique it doesn't want; the level stays.
        private void DrawForgetCard(Rect r, System.Collections.Generic.List<ArmyMember> forgetters)
        {
            Card(r, false, false);
            var x = r.x + 10;
            var inner = r.width - 20;
            Label(new Rect(x, r.y + 8, inner, 22), "Forget a technique", _cardName, Color.white);
            Label(new Rect(x, r.y + 30, inner, 18), "The level stays (relearning it adds none), and techniques that need it go too. Click one to forget it.", _cardText, DimText);
            var y = r.y + 52;
            foreach (var member in forgetters)
            {
                Label(new Rect(x, y + 2, 100, 18), member.Stats.Name, _cardText, TextColor);
                var cx = x + 104;
                foreach (var technique in member.Techniques.ToList())
                {
                    var size = _chip.CalcSize(new GUIContent(technique.Name));
                    GUI.enabled = _run.CanForget(member, technique);
                    var m = member;
                    var t = technique;
                    if (GUI.Button(new Rect(cx, y, size.x, 20), technique.Name, _chip))
                        Do(() =>
                        {
                            if (_run.Forget(m, t))
                                _runLog.RecordForget(t, m);
                        });
                    GUI.enabled = true;
                    cx += size.x + 6;
                }

                y += 24;
            }

            Price(r, Run.ForgetPrice);
        }

        // Honing: one action of one unit gets a cooldown 1 turn shorter for the run.
        private void DrawHoneCard(Rect r, System.Collections.Generic.List<ArmyMember> honers)
        {
            Card(r, false, false);
            var x = r.x + 10;
            var inner = r.width - 20;
            Label(new Rect(x, r.y + 8, inner, 22), "Hone an action", _cardName, Color.white);
            Label(new Rect(x, r.y + 30, inner, 18), "Its cooldown is 1 turn shorter for this unit for the rest of the run (1 at least). Click one to hone it.", _cardText, DimText);
            var y = r.y + 52;
            foreach (var member in honers)
            {
                Label(new Rect(x, y + 2, 100, 18), member.Stats.Name, _cardText, TextColor);
                var cx = x + 104;
                foreach (var action in member.AllActions.Where(a => Battle.Cooldown(a) > 1 && !member.IsHoned(a)).ToList())
                {
                    var text = $"{Abilities.Name(action)} ({Battle.Cooldown(action)} -> {Battle.Cooldown(action) - 1})";
                    var size = _chip.CalcSize(new GUIContent(text));
                    GUI.enabled = _run.CanHone(member, action);
                    var m = member;
                    var a = action;
                    if (GUI.Button(new Rect(cx, y, size.x, 20), text, _chip))
                        Do(() =>
                        {
                            if (_run.Hone(m, a))
                                _runLog.RecordHone(m, a);
                        });
                    GUI.enabled = true;
                    cx += size.x + 6;
                }

                y += 24;
            }

            Price(r, Run.HonePrice);
        }

        // Mending: takes one crack off a unit.
        private void DrawMendCard(Rect r, System.Collections.Generic.List<ArmyMember> cracked)
        {
            Card(r, false, false);
            var x = r.x + 10;
            var inner = r.width - 20;
            Label(new Rect(x, r.y + 8, inner, 22), "Mend a crack", _cardName, Color.white);
            Label(new Rect(x, r.y + 30, inner, 18), $"Each crack costs {Run.CrackHp} max HP, and a statue with {Run.CracksToShatter} shatters for good. Click a unit to mend one crack.", _cardText, DimText);
            var y = r.y + 52;
            foreach (var member in cracked)
            {
                var text = $"{member.Stats.Name}: {CrackText(member)}";
                var size = _chip.CalcSize(new GUIContent(text));
                GUI.enabled = _run.CanMend(member);
                var m = member;
                if (GUI.Button(new Rect(x, y, size.x, 20), text, _chip))
                    Do(() =>
                    {
                        if (_run.Mend(m))
                            _runLog.RecordMend(m);
                    });
                GUI.enabled = true;
                y += 24;
            }

            Price(r, Run.MendPrice);
        }

        /// <summary>A unit in the army row: statue, level, item and techniques. While an offer is picked, clicking it buys the offer for that unit.</summary>
        private void DrawMemberCard(Rect r, ArmyMember m)
        {
            var technique = _selectedOffer != null && _run.Offers.Contains(_selectedOffer) ? _selectedOffer : null;
            var item = _selectedItem != null && _run.ItemOffers.Contains(_selectedItem) ? _selectedItem : null;
            var choosing = technique != null || item != null;
            var can = technique != null ? _run.CanBuy(technique, m) : item != null && _run.CanBuyItem(item, m);
            var old = GUI.color;
            if (choosing && !can)
                GUI.color = Faded;
            if (Card(r, false, can))
                Do(() =>
                {
                    if (technique != null)
                        BuyTechnique(technique, m);
                    else
                        BuyItem(item, m);
                });
            if (choosing && can)
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), HudArt.Solid(GoldColor));

            DrawStatue(new Rect(r.x + 6, r.y + 10, 58, r.height - 20), m.Stats.Kind);
            var x = r.x + 70;
            var inner = r.xMax - 8 - x;
            var y = r.y + 8;
            Label(new Rect(x, y, inner, 22), m.Stats.Name, _cardName, Color.white);
            y += 22;
            var levelUp = technique != null && can && m.WouldLevelUp(technique);
            Label(new Rect(x, y, inner, 16), levelUp ?$"Level {m.Level} -> {m.Level + 1}" : $"Level {m.Level}", _cardText, levelUp ? HudArt.BuffColor : TextColor);
            y += 16;
            var replacing = item != null && can && m.Equipment != null;
            Label(new Rect(x, y, inner, 16), replacing ? $"Replaces {m.Equipment.Name}" : $"Item: {(m.Equipment != null ? m.Equipment.Name : "none")}",
                _cardText, replacing ? HudArt.HarmColor : TextColor);
            y += 16;
            var bonus = new System.Collections.Generic.List<string>();
            if (m.LevelHp + m.BonusMaxHp > 0)
                bonus.Add($"+{m.LevelHp + m.BonusMaxHp} max HP");
            if (m.LevelDamage + m.BonusDamage > 0)
                bonus.Add($"+{m.LevelDamage + m.BonusDamage} damage");
            if (m.LevelMines > 0)
                bonus.Add($"+{m.LevelMines} mine");
            if (bonus.Count > 0)
            {
                Label(new Rect(x, y, inner, 16), string.Join(", ", bonus), _cardText, TextColor);
                y += 16;
            }

            if (m.Cracks > 0)
            {
                Label(new Rect(x, y, inner, 16), $"{m.Cracks} crack{(m.Cracks == 1 ? "" : "s")}: -{m.Cracks * Run.CrackHp} max HP", _cardText, HudArt.HarmColor);
                y += 16;
            }

            y += 4;
            var techniques = m.Techniques.Count > 0 ? string.Join(", ", m.Techniques.Select(t => t.Name)) : "No techniques yet";
            Label(new Rect(x, y, inner, r.yMax - 24 - y), techniques, _cardText, DimText);
            if (technique != null && !can)
            {
                var why = WhyNot(technique, m);
                Label(new Rect(x, r.yMax - 22, inner, 18), why.Length > 0 ? char.ToUpper(why[0]) + why.Substring(1) : "", _cardText, TooDearColor);
            }

            GUI.color = old;
        }

        private void BuyTechnique(Technique offer, ArmyMember buyer)
        {
            if (_run.Buy(offer, buyer))
            {
                _runLog.RecordPurchase(offer, buyer);
                _selectedOffer = null;
            }
        }

        private void BuyItem(Equipment item, ArmyMember buyer)
        {
            var replaced = buyer.Equipment;
            if (_run.BuyItem(item, buyer))
            {
                _runLog.RecordItemPurchase(item, buyer, replaced);
                _selectedItem = null;
            }
        }

        private void DrawEvent(float screenWidth)
        {
            const float w = 520;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 60, w, 560), GUI.skin.box);
            switch (_run.CurrentEvent)
            {
                case EventKind.WanderingRecruit:
                    GUILayout.Label($"A Wandering {_run.EventRecruit.Name}", Title);
                    KeywordText.LayoutLabel("A dusty statue offers to join you.\n" + Describe(_run.EventRecruit), WrapLabel, w - 12);
                    if (_run.Army.Count < _run.ArmyLimit)
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
                        KeywordText.LayoutLabel("Inside, an old training manual:\n" + Describe(cart), WrapLabel, w - 12);
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

                case EventKind.Mason:
                    GUILayout.Label("A Travelling Stonemason", Title);
                    if (_run.ArmyHasCracks)
                    {
                        GUILayout.Label("\"Those cracks will be the end of you. Hold still.\" She offers to mend every crack in your army, free of charge.");
                        if (GUILayout.Button("Mend every crack", GUILayout.Height(32)))
                            Do(() =>
                            {
                                _run.VisitMason(null);
                                AfterEvent("mend all");
                            });
                    }
                    else
                    {
                        GUILayout.Label($"\"Not a scratch on you. I'll reinforce one of you, then.\" (+{Run.MasonHp} max HP for the run)");
                        foreach (var member in _run.Army)
                        {
                            var m = member;
                            if (GUILayout.Button($"Reinforce the {member.Stats.Name}"))
                                Do(() =>
                                {
                                    _run.VisitMason(m);
                                    AfterEvent($"reinforce {m.Stats.Name}");
                                });
                        }
                    }

                    if (GUILayout.Button("Move on"))
                        Do(() =>
                        {
                            _run.VisitMason(null, true);
                            AfterEvent("leave");
                        });
                    break;

                case EventKind.Altar:
                    GUILayout.Label("A Cracked Altar", Title);
                    var altar = _run.AltarTechnique;
                    if (altar != null)
                    {
                        KeywordText.LayoutLabel($"Power seeps from a split in the stone. A statue that lays its hands on it learns a rare technique, but takes a scar: a crack that can never be mended (-{Run.CrackHp} max HP; {Run.CracksToShatter} cracks shatter it).\n" + Describe(altar), WrapLabel, w - 12);
                        foreach (var member in _run.Army)
                        {
                            var m = member;
                            var why = !Run.CanCrackFor(member) ? "one crack from shattering" : !altar.CanLearn(member) ? WhyNot(altar, member) : "";
                            GUI.enabled = _run.CanTakeAltar(member);
                            if (GUILayout.Button($"The {member.Stats.Name} touches it ({member.Cracks} crack{(member.Cracks == 1 ? "" : "s")} now){(why.Length > 0 ? $"  ({why})" : "")}"))
                                Do(() =>
                                {
                                    _run.TakeAltar(m);
                                    AfterEvent($"{altar.Name} -> {m.Stats.Name}");
                                });
                            GUI.enabled = true;
                        }
                    }
                    else
                    {
                        GUILayout.Label("A split altar, long gone cold. Nothing here for your army.");
                    }

                    if (GUILayout.Button("Leave it"))
                        Do(() =>
                        {
                            _run.TakeAltar(null);
                            AfterEvent("leave");
                        });
                    break;

                case EventKind.Sculptor:
                    GUILayout.Label("The Sculptor", Title);
                    GUILayout.Label("\"I can carve something better out of you. Give me one of your techniques, and I'll chisel a rare one of your line in its place. Which one? That's up to the stone.\"");
                    var any = false;
                    foreach (var member in _run.Army.Where(m => m.Techniques.Count > 0))
                    {
                        GUILayout.Label($"{member.Stats.Name} gives up:");
                        GUILayout.BeginHorizontal();
                        foreach (var technique in member.Techniques.ToList())
                        {
                            var m = member;
                            var t = technique;
                            GUI.enabled = _run.CanSculpt(member, technique);
                            any |= GUI.enabled;
                            if (GUILayout.Button(technique.Name))
                                Do(() =>
                                {
                                    _run.Sculpt(m, t);
                                    AfterEvent($"{t.Name} ({m.Stats.Name}) -> {_run.SculptedInto?.Name}");
                                });
                            GUI.enabled = true;
                        }

                        GUILayout.EndHorizontal();
                    }

                    if (!any)
                        GUILayout.Label("Nobody has a technique the sculptor can rework yet.");
                    if (GUILayout.Button("No, thank you"))
                        Do(() =>
                        {
                            _run.Sculpt(null, null);
                            AfterEvent("leave");
                        });
                    break;

                case EventKind.Bargain:
                    GUILayout.Label("A Hooded Stranger", Title);
                    GUILayout.Label($"\"Gold for your luck. {Run.BargainGold} pieces, and all I ask is a little misfortune to follow you.\"\nAccepting gives a random curse for every later battle.");
                    GUI.enabled = _run.CanBargain;
                    if (GUILayout.Button($"Take the gold (+{Run.BargainGold} gold, a curse)", GUILayout.Height(32)))
                        Do(() =>
                        {
                            _run.Bargain(true);
                            if (_run.LastCurse.HasValue)
                                _runLog.RecordOmen(_run.LastCurse.Value, false);
                            AfterEvent("accept");
                        });
                    GUI.enabled = true;
                    if (GUILayout.Button("Refuse"))
                        Do(() =>
                        {
                            _run.Bargain(false);
                            AfterEvent("refuse");
                        });
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
                if (KeywordButton($"{Omens.Name(omen, true)}\n{Omens.Describe(omen, true)}", style, w - 12, 70f))
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
                if (KeywordButton(marker + Describe(technique), style, w - 12))
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
                    var banner = _run.RewardBanner;
                    if (!_run.TakeReward(pick, taker))
                        return;
                    _runLog.RecordReward(offered, pick, taker, banner);
                    _lastNote = $"The {taker.Stats.Name} learns {pick.Name} (level {taker.Level}).";
                    _selectedReward = null;
                });
            }
            else
            {
                DrawArmy();
            }

            GUILayout.FlexibleSpace();
            if (_run.RewardBanner.HasValue)
            {
                var banner = _run.RewardBanner.Value;
                GUILayout.Label("Or take this banner instead of a technique:");
                EnsureShopStyles();
                var card = GUILayoutUtility.GetRect(w - 12, BannerCardHeight(banner, null, w - 12), GUILayout.ExpandWidth(true));
                DrawBannerCard(card, banner, null, () =>
                {
                    var offered = _run.RewardOffers.ToList();
                    if (!_run.TakeRewardBanner())
                        return;
                    _runLog.RecordReward(offered, null, null, banner, true);
                    _runLog.RecordBanner(banner, "elite");
                    _lastNote = $"Banner raised: {Banners.Name(banner)}.";
                    _selectedReward = null;
                });
                GUILayout.Space(6);
            }

            if (GUILayout.Button($"Skip (+{Run.SkipGold} gold)", GUILayout.Height(32)))
                Do(() =>
                {
                    var offered = _run.RewardOffers.ToList();
                    var banner = _run.RewardBanner;
                    _run.SkipReward();
                    _runLog.RecordReward(offered, null, null, banner);
                    _lastNote = $"Skipped the pick: +{Run.SkipGold} gold.";
                    _selectedReward = null;
                });
            GUILayout.EndArea();
        }

        /// <summary>Mercenaries banner: before a fight, hire one of the offered units for it, or go without.</summary>
        private void DrawMercs(float screenWidth)
        {
            const float w = 560;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 40, w, 640), GUI.skin.box);
            GUILayout.Label("Mercenaries for hire", Title);
            if (!string.IsNullOrEmpty(_lastNote))
                GUILayout.Label(_lastNote);
            GUILayout.Label($"Next: {_run.CurrentEncounter.Name}. Hire one for {Banners.MercPrice} gold: it fights this battle only and doesn't count " +
                            $"as a survivor for gold. You have {_run.Gold} gold.");
            GUILayout.Space(6);
            var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            foreach (var unit in _run.MercOffers)
            {
                var pick = unit;
                GUI.enabled = _run.CanHire(unit);
                if (KeywordButton($"Hire: {Describe(unit)}", style, w - 12))
                    Do(() => HireMerc(pick));
                GUI.enabled = true;
            }

            GUILayout.FlexibleSpace();
            DrawArmy();
            if (GUILayout.Button("Fight without one", GUILayout.Height(32)))
                Do(() => HireMerc(null));
            GUILayout.EndArea();
        }

        /// <summary>Third Wheel taken with too many units: choose who stays.</summary>
        private void DrawDismiss(float screenWidth)
        {
            const float w = 560;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 40, w, 640), GUI.skin.box);
            GUILayout.Label("Too many for the banner", Title);
            GUILayout.Label($"Your army can hold {_run.ArmyLimit} units now. Choose who stays; the others leave for good, with their techniques and items.");
            GUILayout.Space(6);
            var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            foreach (var member in _run.Army)
            {
                var m = member;
                var kept = _keepPicks.Contains(member);
                var techniques = m.Techniques.Count > 0 ? string.Join(", ", m.Techniques.Select(t => t.Name)) : "no techniques";
                var item = m.Equipment != null ? $" [{m.Equipment.Name}]" : "";
                if (GUILayout.Button($"{(kept ? "[X] " : "[  ] ")}{m.Stats.Name} (level {m.Level}){item}: {techniques}", style))
                    Do(() =>
                    {
                        if (!_keepPicks.Remove(m) && _keepPicks.Count < _run.ArmyLimit)
                            _keepPicks.Add(m);
                    });
            }

            GUILayout.FlexibleSpace();
            GUI.enabled = _keepPicks.Count == _run.ArmyLimit;
            if (GUILayout.Button($"Keep these ({_keepPicks.Count}/{_run.ArmyLimit})", GUILayout.Height(34)))
                Do(ConfirmDismiss);
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        // The "Abandon run" screen is up: the run is still going until New run is clicked, which saves it as abandoned.
        private bool _abandoning;

        // The answers on the run-end screen; both can be skipped.
        private string _runTag = "";
        private int _difficulty;

        private void SetRating(string tag, int difficulty)
        {
            _runTag = tag;
            _difficulty = difficulty;
            _runLog?.RecordRating(tag, difficulty);
        }

        /// <summary>The run-end screen (won, lost or abandoned): New run always works, the tag and the rating are optional.</summary>
        private void DrawRunEnd(float screenWidth)
        {
            const float w = 340;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 140, w, 310), GUI.skin.box);
            var centred = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label(_abandoning ? "Abandon this run?" : _run.State == RunState.Won ? "Run won!" : "Run lost", Title);
            GUILayout.Label(_abandoning ? $"You are at step {_run.StepIndex + 1}."
                : _run.State == RunState.Won ? $"The ogre is rubble. Gold left: {_run.Gold}"
                : $"Defeated at step {_run.StepIndex + 1}: {_run.CurrentEncounter?.Name}", centred);

            GUILayout.Space(8);
            GUILayout.Label("Was this run... (optional)", centred);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_runTag == "serious" ? "> " : "") + "Serious run"))
                Do(() => SetRating("serious", _difficulty));
            if (GUILayout.Button((_runTag == "testing" ? "> " : "") + "Just testing"))
                Do(() => SetRating("testing", _difficulty));
            GUILayout.EndHorizontal();

            GUILayout.Label("How hard was it? 1 easy, 5 brutal (optional)", centred);
            GUILayout.BeginHorizontal();
            for (var i = 1; i <= 5; i++)
            {
                var rating = i;
                if (GUILayout.Button((_difficulty == i ? "> " : "") + i))
                    Do(() => SetRating(_runTag, rating));
            }

            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            if (_abandoning && GUILayout.Button("Back to the run", GUILayout.Height(26)))
                Do(() => _abandoning = false);
            if (GUILayout.Button(_abandoning ? "Abandon and start a new run" : "New run", GUILayout.Height(32)))
                Do(Restart);
            GUILayout.EndArea();
        }
    }
}
