using System;
using System.Collections;
using System.Collections.Generic;
using ArmyRoguelike.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Test battles only: F1 opens a panel to try the effects and sounds without waiting for an omen.
    /// Keys 5-0 play an effect on the hovered tile or unit (1-4 stay the selected unit's actions); a button
    /// per sound plays it alone; sliders set the volumes. Visual and sound only: nothing takes damage.
    /// </summary>
    public sealed class FxPlayground : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        private const float Width = 280f;
        private const float Height = 520f;
        private const float Margin = 10f;
        private const int ShieldPoints = 4;

        private static FxPlayground _instance;

        private static readonly string[] Keys =
        {
            "5  Sun mark on tile",
            "6  Sun strike on tile",
            "7  Moon shield on unit",
            "8  Hit the shield (1 point)",
            "9  Shield fades",
            "0  Lightning on tile",
        };

        private BoardView _board;
        private Func<GridPos?> _hovered;
        private bool _open;
        private Vector2 _scroll;
        private readonly Dictionary<GridPos, SunMark> _marks = new Dictionary<GridPos, SunMark>();
        private readonly Dictionary<UnitView, MoonShieldView> _shields = new Dictionary<UnitView, MoonShieldView>();

        public static FxPlayground Create(Transform parent, BoardView board, Func<GridPos?> hovered)
        {
            var playground = new GameObject("Fx playground").AddComponent<FxPlayground>();
            playground.transform.SetParent(parent, false);
            playground._board = board;
            playground._hovered = hovered;
            return _instance = playground;
        }

        private static Rect PanelRect => new Rect(Margin, (ReferenceHeight - Height) * 0.5f, Width, Height);

        /// <summary>Whether the open panel covers this screen point, so clicks on it don't reach the board.</summary>
        public static bool IsOver(Vector2 screen)
        {
            if (_instance == null || !_instance._open)
                return false;
            var scale = Screen.height / ReferenceHeight;
            return PanelRect.Contains(new Vector2(screen.x / scale, (Screen.height - screen.y) / scale));
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.f1Key.wasPressedThisFrame)
                _open = !_open;
            if (!_open)
                return;

            var tile = _hovered();
            if (!tile.HasValue)
                return;
            if (keyboard.digit5Key.wasPressedThisFrame)
                Mark(tile.Value);
            if (keyboard.digit6Key.wasPressedThisFrame)
                StartCoroutine(Strike(tile.Value));
            if (keyboard.digit7Key.wasPressedThisFrame)
                Shield(tile.Value);
            if (keyboard.digit8Key.wasPressedThisFrame && ShieldOn(tile.Value) is { } hit)
                hit.Absorb(1);
            if (keyboard.digit9Key.wasPressedThisFrame && ShieldOn(tile.Value) is { } fading)
                fading.Fade();
            if (keyboard.digit0Key.wasPressedThisFrame)
                StartCoroutine(LightningStrike.Strike(_board.transform, tile.Value));
        }

        private SunMark Mark(GridPos tile)
        {
            if (!_marks.TryGetValue(tile, out var mark) || mark == null)
                _marks[tile] = mark = SunMark.Create(_board.transform, tile);
            return mark;
        }

        private IEnumerator Strike(GridPos tile)
        {
            var mark = Mark(tile);
            _marks.Remove(tile);
            yield return mark.Charge();
            mark.Impact();
        }

        private UnitView ViewAt(GridPos tile)
        {
            var unit = _board.Battle.UnitAt(tile);
            return unit != null ? _board.ViewOf(unit) : null;
        }

        private void Shield(GridPos tile)
        {
            var view = ViewAt(tile);
            if (view == null)
                return;
            ShieldOn(tile)?.Fade();
            _shields[view] = MoonShieldView.Create(view, ShieldPoints, 0);
        }

        /// <summary>The unit's playground shield while it is still up.</summary>
        private MoonShieldView ShieldOn(GridPos tile)
        {
            var view = ViewAt(tile);
            if (view == null || !_shields.TryGetValue(view, out var shield))
                return null;
            if (shield != null && shield.Points > 0)
                return shield;
            _shields.Remove(view);
            return null;
        }

        private void OnGUI()
        {
            if (!_open)
                return;
            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("<b>Effects playground</b> (F1 closes)", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("Keys act on the tile or unit under the cursor:");
            foreach (var key in Keys)
                GUILayout.Label(key);

            GUILayout.Space(6);
            GUILayout.Label($"Master volume {Sfx.MasterVolume:P0}");
            Sfx.MasterVolume = GUILayout.HorizontalSlider(Sfx.MasterVolume, 0f, 1f);
            GUILayout.Label($"Effects volume {Sfx.EffectsVolume:P0}");
            Sfx.EffectsVolume = GUILayout.HorizontalSlider(Sfx.EffectsVolume, 0f, 1f);

            GUILayout.Space(6);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Lock a variant (the effects then always play it):");
            foreach (var name in Sfx.Names)
                LockRow(name);

            GUILayout.Space(6);
            GUILayout.Label("Each sound on its own:");
            foreach (var name in Sfx.Names)
                if (GUILayout.Button(name))
                    Sfx.Play(name, pitch: 0f);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>For a sound with variants: "random" or one variant number, as a row of toggle buttons.</summary>
        private static void LockRow(string sound)
        {
            var variants = Sfx.VariantsOf(sound);
            if (variants.Count < 2)
                return;
            var labels = new string[variants.Count + 1];
            labels[0] = "rnd";
            for (var i = 0; i < variants.Count; i++)
                labels[i + 1] = (i + 1).ToString();
            var current = variants.IndexOf(Sfx.LockedVariant(sound)) + 1;
            GUILayout.Label(sound);
            var picked = GUILayout.SelectionGrid(current, labels, 7);
            if (picked != current)
                Sfx.Lock(sound, picked == 0 ? null : variants[picked - 1]);
        }
    }
}
