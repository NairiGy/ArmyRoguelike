using System;
using System.Linq;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Builds and runs the mini-run at runtime, so the scene only needs its camera and light:
    /// battle, shop, battle, shop, battle, then a run-end screen.
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
        private int _lastEarned;

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
            StartBattle();
        }

        private void OnDestroy() => _runLog?.Save();

        private void StartBattle()
        {
            var battle = _run.StartBattle();
            _battleRoot = new GameObject($"Battle {_run.BattleIndex + 1}");
            _battleRoot.transform.SetParent(transform, false);
            var board = BoardView.Create(battle, _battleRoot.transform);
            FrameCamera(Camera.main, board);
            var context = new BattleContext(_run.BattleIndex + 1, _run.Encounters.Count, _run.CurrentEncounter.Name, _run.Gold, _run.Seed);
            _controller = _battleRoot.AddComponent<BattleController>();
            _controller.Init(board, Camera.main, context, FinishBattle);
        }

        private void FinishBattle()
        {
            var battle = _run.CurrentBattle;
            _lastEarned = _run.FinishBattle();
            _runLog.RecordBattle(battle, _lastEarned, _controller.LogFile);
            Destroy(_battleRoot);
            _battleRoot = null;
            _controller = null;
            _selectedOffer = null;
            if (_run.State == RunState.Shop)
                _runLog.OpenShop();
            else
                _runLog.Save();
        }

        private void LeaveShop()
        {
            _runLog.CloseShop();
            _run.LeaveShop();
            StartBattle();
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

        // ---------- Shop and run-end screens ----------

        private void OnGUI()
        {
            if (_run == null || _battleRoot != null)
                return;
            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;

            switch (_run.State)
            {
                case RunState.Shop:
                    DrawShop(width);
                    break;
                case RunState.Won:
                case RunState.Lost:
                    DrawRunEnd(width);
                    break;
            }
        }

        private void DrawShop(float screenWidth)
        {
            const float w = 520;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 40, w, 620), GUI.skin.box);
            var title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("Shop", title);
            GUILayout.Label($"Battle won: +{_lastEarned} gold.   Gold: {_run.Gold}");
            GUILayout.Label("Each unit has one slot. Buying replaces (and loses) the old item.");
            GUILayout.Space(8);

            GUILayout.Label("For sale (click one):");
            if (_run.Offers.Count == 0)
                GUILayout.Label("  Sold out.");
            foreach (var offer in _run.Offers.ToList())
            {
                var marker = offer == _selectedOffer ? "> " : "";
                var label = $"{marker}{offer.Name}   {offer.Effect}   {offer.Price} gold";
                GUI.enabled = _run.Gold >= offer.Price;
                if (GUILayout.Button(label))
                    _selectedOffer = offer;
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
                        var replaced = member.Equipment;
                        if (_run.Buy(_selectedOffer, member))
                        {
                            _runLog.RecordPurchase(_selectedOffer, member, replaced);
                            _selectedOffer = null;
                        }
                    }

                    GUI.enabled = true;
                }
            }
            else
            {
                GUILayout.Label("Your army:");
                foreach (var member in _run.Army)
                    GUILayout.Label($"  {member.Stats.Name}: {(member.Equipment != null ? $"{member.Equipment.Name} ({member.Equipment.Effect})" : "nothing")}");
            }

            GUILayout.FlexibleSpace();
            var next = _run.Encounters[_run.BattleIndex + 1];
            if (GUILayout.Button($"Continue to battle {_run.BattleIndex + 2}: {next.Name}", GUILayout.Height(32)))
                LeaveShop();
            GUILayout.EndArea();
        }

        private void DrawRunEnd(float screenWidth)
        {
            const float w = 320;
            GUILayout.BeginArea(new Rect((screenWidth - w) * 0.5f, 140, w, 200), GUI.skin.box);
            var title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var centred = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label(_run.State == RunState.Won ? "Run won!" : "Run lost", title);
            GUILayout.Label(_run.State == RunState.Won
                ? $"All {_run.Encounters.Count} battles beaten. Gold left: {_run.Gold}"
                : $"Defeated in battle {_run.BattleIndex + 1}: {_run.CurrentEncounter.Name}", centred);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("New run", GUILayout.Height(32)))
                Restart();
            GUILayout.EndArea();
        }
    }
}
