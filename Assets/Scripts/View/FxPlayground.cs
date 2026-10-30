using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArmyRoguelike.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Test battles only: F1 opens a panel to try the effects and sounds without waiting for an omen.
    /// Keys 5-0 play an effect on the hovered tile or unit (1-4 stay the selected unit's actions). The panel
    /// groups the sounds by effect, one row each: the arrows step through its variants (that variant is then
    /// locked, so the effect always plays it), ▶ plays it alone, and Try starts an audition: the real effect
    /// plays on a unit with each variant in turn, the number shows big, and Y / N mark it keep or drop into
    /// Playtests/sound-review.txt. Visual and sound only: nothing takes damage.
    /// </summary>
    public sealed class FxPlayground : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        private const float Width = 320f;
        private const float Height = 640f;
        private const float Margin = 10f;
        private const int ShieldPoints = 4;
        private const string ReviewFile = "sound-review.txt";

        private static FxPlayground _instance;

        /// <summary>The panel's sections: title, the sound name prefix it holds, and how to trigger the effect.</summary>
        private static readonly (string Title, string Prefix, string Keys)[] Sections =
        {
            ("Sun omen", "sun_", "5 mark the tile    6 strike it"),
            ("Moon shield", "moon_", "7 shield the unit    8 hit it    9 fade"),
            ("Lightning", "lightning_", "0 strike the tile"),
            ("Units", "unit_", "walk, attack and kill in the battle"),
        };

        private BoardView _board;
        private Func<GridPos?> _hovered;
        private bool _open;
        private Vector2 _scroll;
        private readonly HashSet<string> _folded = new HashSet<string>();
        private GUIStyle _header;
        private GUIStyle _small;
        private GUIStyle _big;
        private readonly Dictionary<GridPos, SunMark> _marks = new Dictionary<GridPos, SunMark>();
        private readonly Dictionary<UnitView, MoonShieldView> _shields = new Dictionary<UnitView, MoonShieldView>();
        private GridPos? _lastTile;

        // The audition: one sound, its variants, which one is playing, and the verdicts so far.
        private string _auditionSound;
        private List<string> _auditionVariants;
        private int _auditionIndex;
        private string _lockBefore;
        private Coroutine _auditionLoop;
        private Action _undoDemo;
        private string _note;
        private float _noteUntil;
        private Dictionary<string, string> _verdicts;

        private bool Auditioning => _auditionSound != null;

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
            if (_instance == null || !_instance._open || _instance.Auditioning)
                return false;
            var scale = Screen.height / ReferenceHeight;
            return PanelRect.Contains(new Vector2(screen.x / scale, (Screen.height - screen.y) / scale));
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (Auditioning)
            {
                AuditionKeys(keyboard);
                return;
            }

            if (keyboard.f1Key.wasPressedThisFrame)
                _open = !_open;
            if (!_open)
                return;

            var tile = _hovered();
            if (!tile.HasValue)
                return;
            _lastTile = tile;
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

        // ---------- Audition ----------

        private void StartAudition(string sound)
        {
            var view = DemoUnit();
            if (view == null)
            {
                ShowNote("No unit to play the effect on yet: place your army first.");
                return;
            }

            _verdicts = LoadVerdicts();
            _auditionSound = sound;
            _auditionVariants = Sfx.VariantsOf(sound);
            _lockBefore = Sfx.LockedVariant(sound);
            // Start on the first variant not judged yet.
            _auditionIndex = Mathf.Max(0, _auditionVariants.FindIndex(v => !_verdicts.ContainsKey(v)));
            PlayCurrent();
        }

        private void StopAudition(string note)
        {
            StopDemo();
            Sfx.Lock(_auditionSound, _lockBefore);
            _auditionSound = null;
            ShowNote(note);
        }

        private void AuditionKeys(Keyboard keyboard)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
                StopAudition("Audition stopped. Verdicts so far are saved.");
            else if (keyboard.yKey.wasPressedThisFrame)
                Judge("keep");
            else if (keyboard.nKey.wasPressedThisFrame)
                Judge("drop");
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                Step(1);
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
                Step(-1);
            else if (keyboard.spaceKey.wasPressedThisFrame)
                PlayCurrent();
        }

        private void Judge(string verdict)
        {
            _verdicts[_auditionVariants[_auditionIndex]] = verdict;
            SaveVerdicts();
            if (_auditionIndex == _auditionVariants.Count - 1)
                StopAudition($"All {_auditionVariants.Count} judged and saved to Playtests/{ReviewFile}.");
            else
                Step(1);
        }

        private void Step(int by)
        {
            _auditionIndex = Mathf.Clamp(_auditionIndex + by, 0, _auditionVariants.Count - 1);
            PlayCurrent();
        }

        /// <summary>Locks the current variant and plays its effect over and over until the user moves on.</summary>
        private void PlayCurrent()
        {
            StopDemo();
            Sfx.Lock(_auditionSound, _auditionVariants[_auditionIndex]);
            _auditionLoop = StartCoroutine(AuditionLoop());
        }

        private IEnumerator AuditionLoop()
        {
            while (true)
            {
                yield return Demo(_auditionSound);
                _undoDemo?.Invoke();
                _undoDemo = null;
                yield return new WaitForSeconds(1.2f);
            }
        }

        private void StopDemo()
        {
            if (_auditionLoop != null)
                StopCoroutine(_auditionLoop);
            _auditionLoop = null;
            _undoDemo?.Invoke();
            _undoDemo = null;
        }

        /// <summary>The unit the demos play on: the one on the last hovered tile, else the first of the player's.</summary>
        private UnitView DemoUnit()
        {
            if (_lastTile.HasValue && ViewAt(_lastTile.Value) is { } hovered && hovered.Unit.IsAlive)
                return hovered;
            var unit = _board.Battle.Units.FirstOrDefault(u => u.Team == Team.Player && u.IsPlaced && u.IsAlive && !u.IsObjective);
            return unit != null ? _board.ViewOf(unit) : null;
        }

        /// <summary>Plays the real effect that makes this sound, on the demo unit or its tile.</summary>
        private IEnumerator Demo(string sound)
        {
            var view = DemoUnit();
            if (view == null)
                yield break;
            var tile = view.Unit.Pos;
            // Unit demos move, tint or crumble the statue; this puts it back however far they got.
            if (sound.StartsWith("unit_"))
                _undoDemo = view.Restore;

            switch (sound)
            {
                case "unit_step":
                    yield return view.MoveAlong(new[] { tile });
                    break;
                case "unit_attack":
                    var ranged = view.Unit.IsRanged;
                    var target = IsoMath.GridToWorld(new GridPos(tile.X + 1, tile.Y));
                    // Ranged attacks have no blow of their own; play it as the shot lands so it can still be judged.
                    yield return view.Lunge(target, () => { if (ranged) Sfx.Play("unit_attack", target); });
                    break;
                case "unit_hurt":
                    yield return view.Hit();
                    break;
                case "unit_die":
                    yield return view.Die();
                    yield return new WaitForSeconds(0.6f);
                    break;
                case "sun_mark":
                    var mark = SunMark.Create(_board.transform, tile);
                    _undoDemo = () => { if (mark != null) Destroy(mark.gameObject); };
                    yield return new WaitForSeconds(2f);
                    break;
                case "sun_charge":
                case "sun_impact":
                case "sun_sizzle":
                    var strike = SunMark.Create(_board.transform, tile);
                    yield return new WaitForSeconds(0.3f);
                    yield return strike.Charge();
                    strike.Impact();
                    yield return new WaitForSeconds(sound == "sun_sizzle" ? 2.5f : 1.2f);
                    break;
                case "moon_cast":
                case "moon_absorb":
                case "moon_shatter":
                case "moon_fade":
                    var shield = MoonShieldView.Create(view, ShieldPoints, 0);
                    _undoDemo = () => { if (shield != null) Destroy(shield.gameObject); };
                    yield return new WaitForSeconds(sound == "moon_cast" ? 1.5f : 0.9f);
                    if (sound == "moon_absorb")
                        shield.Absorb(1);
                    else if (sound == "moon_shatter")
                        shield.Absorb(ShieldPoints);
                    else if (sound == "moon_fade")
                        shield.Fade();
                    yield return new WaitForSeconds(1f);
                    break;
                default:
                    if (sound.StartsWith("lightning_"))
                        yield return LightningStrike.Strike(_board.transform, tile);
                    else
                        Sfx.Play(sound, view.transform.position, pitch: 0f);
                    yield return new WaitForSeconds(1f);
                    break;
            }
        }

        private static string ReviewPath => Path.Combine(
            Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Playtests"))
                : Path.Combine(Application.persistentDataPath, "Playtests"),
            ReviewFile);

        /// <summary>Verdicts by variant name, from earlier auditions too (one "name verdict" line each).</summary>
        private static Dictionary<string, string> LoadVerdicts()
        {
            var verdicts = new Dictionary<string, string>();
            if (!File.Exists(ReviewPath))
                return verdicts;
            foreach (var line in File.ReadAllLines(ReviewPath))
            {
                var parts = line.Split(' ');
                if (parts.Length >= 2 && !line.StartsWith("#"))
                    verdicts[parts[0]] = parts[1];
            }

            return verdicts;
        }

        private void SaveVerdicts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReviewPath));
            var lines = new List<string> { $"# Sound audition verdicts (effects playground), updated {DateTime.Now:yyyy-MM-dd HH:mm}" };
            foreach (var kv in _verdicts.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                lines.Add($"{kv.Key} {kv.Value}");
            File.WriteAllLines(ReviewPath, lines);
        }

        private void ShowNote(string note)
        {
            _note = note;
            _noteUntil = Time.unscaledTime + 4f;
        }

        // ---------- Panel ----------

        private void OnGUI()
        {
            if (!_open)
                return;
            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            _header ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _big ??= new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            var width = Screen.width / scale;
            if (_note != null && Time.unscaledTime < _noteUntil)
                GUI.Box(new Rect((width - 460f) * 0.5f, 20f, 460f, 28f), _note);

            if (Auditioning)
            {
                DrawAudition(width);
                return;
            }

            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("<b>Effects playground</b>   F1 closes", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("Keys act on the tile or unit under the cursor. ◄ ► pick a variant (the effect then always plays it; click the number for random), ▶ plays it alone, Try auditions every variant on the last unit you hovered.", _small);
            VolumeRow("Master", Sfx.MasterVolume, v => Sfx.MasterVolume = v);
            VolumeRow("Effects", Sfx.EffectsVolume, v => Sfx.EffectsVolume = v);

            _scroll = GUILayout.BeginScrollView(_scroll);
            var sounds = BaseSounds();
            foreach (var (title, prefix, keys) in Sections)
            {
                var open = !_folded.Contains(title);
                if (GUILayout.Button((open ? "▼ " : "► ") + title, _header))
                {
                    if (open)
                        _folded.Add(title);
                    else
                        _folded.Remove(title);
                }

                if (!open)
                    continue;
                GUILayout.Label(keys, _small);
                foreach (var sound in sounds)
                    if (sound.StartsWith(prefix))
                        SoundRow(sound, prefix);
                GUILayout.Space(4);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>The audition overlay: the sound and variant number big, every variant's verdict, and the keys.</summary>
        private void DrawAudition(float width)
        {
            const float w = 560f;
            GUILayout.BeginArea(new Rect((width - w) * 0.5f, 60f, w, 150f), GUI.skin.box);
            GUILayout.Label($"{_auditionSound}   {_auditionIndex + 1} / {_auditionVariants.Count}", _big);

            var strip = new System.Text.StringBuilder();
            for (var i = 0; i < _auditionVariants.Count; i++)
            {
                _verdicts.TryGetValue(_auditionVariants[i], out var v);
                var mark = v == "keep" ? "✓" : v == "drop" ? "✗" : "·";
                strip.Append(i == _auditionIndex ? $"[{i + 1}{mark}] " : $"{i + 1}{mark} ");
            }

            GUILayout.Label(strip.ToString(), new GUIStyle(_small) { fontSize = 14, alignment = TextAnchor.MiddleCenter });
            GUILayout.Label("Y keep    N drop    ← → previous / next    Space replay    Esc stop", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            GUILayout.EndArea();
        }

        private static void VolumeRow(string label, float value, Action<float> set)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value:P0}", GUILayout.Width(90));
            set(GUILayout.HorizontalSlider(value, 0f, 1f));
            GUILayout.EndHorizontal();
        }

        /// <summary>Sound names without their variant numbers ("sun_mark", not "sun_mark_1").</summary>
        private static List<string> BaseSounds()
        {
            var names = new List<string>();
            foreach (var name in Sfx.Names)
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"_\d+$"))
                    names.Add(name);
            return names;
        }

        /// <summary>One sound: its name, ◄ variant ► to step through (and lock) its variants, ▶ to play it, and Try.</summary>
        private void SoundRow(string sound, string prefix)
        {
            var variants = Sfx.VariantsOf(sound);
            var current = variants.IndexOf(Sfx.LockedVariant(sound)) + 1;
            GUILayout.BeginHorizontal();
            GUILayout.Label(sound.Substring(prefix.Length), GUILayout.Width(70));
            if (variants.Count > 1)
            {
                if (GUILayout.Button("◄", GUILayout.Width(24)))
                    current = current <= 1 ? variants.Count : current - 1;
                if (GUILayout.Button(current == 0 ? $"random ({variants.Count})" : $"{current} / {variants.Count}", GUILayout.Width(78)))
                    current = 0;
                if (GUILayout.Button("►", GUILayout.Width(24)))
                    current = current % variants.Count + 1;
                if (current != variants.IndexOf(Sfx.LockedVariant(sound)) + 1)
                {
                    Sfx.Lock(sound, current == 0 ? null : variants[current - 1]);
                    if (current > 0)
                        Sfx.Play(sound, pitch: 0f);
                }
            }
            else
                GUILayout.Label("", GUILayout.Width(134));

            if (GUILayout.Button("▶", GUILayout.Width(26)))
                Sfx.Play(sound, pitch: 0f);
            if (variants.Count > 1 && GUILayout.Button("Try", GUILayout.Width(36)))
                StartAudition(sound);
            GUILayout.EndHorizontal();
        }
    }
}
