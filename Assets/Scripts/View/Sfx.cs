using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Sound effects. Clips live in <c>Resources/Sfx</c> (see <c>Assets/Audio</c>); variants of one sound are
    /// numbered (<c>moon_absorb_1</c>, <c>moon_absorb_2</c>) and one is picked at random. Effects call
    /// <see cref="Play"/> on the same line as the moment they show, so sound and animation stay in sync.
    /// A sound with no clip yet plays nothing and warns once. Volumes are saved in PlayerPrefs.
    /// </summary>
    public static class Sfx
    {
        private const int Voices = 16;
        private const string MasterKey = "Audio.Master";
        private const string EffectsKey = "Audio.Effects";
        private static readonly Regex VariantSuffix = new Regex(@"_\d+$");

        private static Runner _runner;
        private static AudioSource[] _voices;
        private static Dictionary<string, AudioClip[]> _clips;
        private static readonly HashSet<string> Missing = new HashSet<string>();
        private static float? _master;
        private static float? _effects;

        private sealed class Runner : MonoBehaviour
        {
        }

        public static float MasterVolume
        {
            get => _master ??= PlayerPrefs.GetFloat(MasterKey, 1f);
            set
            {
                _master = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MasterKey, _master.Value);
            }
        }

        public static float EffectsVolume
        {
            get => _effects ??= PlayerPrefs.GetFloat(EffectsKey, 1f);
            set
            {
                _effects = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(EffectsKey, _effects.Value);
            }
        }

        private static float Gain => MasterVolume * EffectsVolume;

        private static Runner Host
        {
            get
            {
                if (_runner != null)
                    return _runner;
                _runner = new GameObject("Sfx").AddComponent<Runner>();
                Object.DontDestroyOnLoad(_runner.gameObject);
                _voices = new AudioSource[Voices];
                for (var i = 0; i < Voices; i++)
                    _voices[i] = NewSource(_runner.gameObject);
                return _runner;
            }
        }

        private static AudioSource NewSource(GameObject host)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>Every clip by sound name; a numbered variant is also there under its own name.</summary>
        private static Dictionary<string, AudioClip[]> Clips
        {
            get
            {
                if (_clips != null)
                    return _clips;
                _clips = new Dictionary<string, AudioClip[]>();
                var groups = new Dictionary<string, List<AudioClip>>();
                foreach (var clip in Resources.LoadAll<AudioClip>("Sfx"))
                {
                    var key = VariantSuffix.Replace(clip.name, "");
                    if (!groups.TryGetValue(key, out var list))
                        groups[key] = list = new List<AudioClip>();
                    list.Add(clip);
                    if (key != clip.name)
                        _clips[clip.name] = new[] { clip };
                }

                foreach (var kv in groups)
                    _clips[kv.Key] = kv.Value.ToArray();
                return _clips;
            }
        }

        /// <summary>Every sound and variant name, sorted (for the effects playground).</summary>
        public static IEnumerable<string> Names
        {
            get
            {
                var names = new List<string>(Clips.Keys);
                names.Sort(System.StringComparer.Ordinal);
                return names;
            }
        }

        /// <summary>The numbered variants of a sound, in order (empty if it has none).</summary>
        public static List<string> VariantsOf(string sound)
        {
            var variants = new List<string>();
            for (var i = 1; Clips.ContainsKey($"{sound}_{i}"); i++)
                variants.Add($"{sound}_{i}");
            return variants;
        }

        /// <summary>
        /// Plays this variant whenever the sound is played, instead of a random one (null goes back to random).
        /// For comparing variants in context in the effects playground; not saved.
        /// </summary>
        public static void Lock(string sound, string variant)
        {
            if (variant == null)
                Locks.Remove(sound);
            else
                Locks[sound] = variant;
        }

        public static string LockedVariant(string sound) => Locks.TryGetValue(sound, out var v) ? v : null;

        private static readonly Dictionary<string, string> Locks = new Dictionary<string, string>();

        /// <summary>A random (or locked) variant of the named sound, or null if it has no clip yet.</summary>
        private static AudioClip Clip(string name)
        {
            if (Locks.TryGetValue(name, out var locked))
                name = locked;
            if (Clips.TryGetValue(name, out var variants))
                return variants[Random.Range(0, variants.Length)];
            if (Missing.Add(name))
                Debug.LogWarning($"Sfx: no clip for \"{name}\" in Resources/Sfx");
            return null;
        }

        /// <summary>Left-right pan from where the sound happens on screen, kept gentle.</summary>
        private static float Pan(Vector3? at)
        {
            var cam = Camera.main;
            if (at == null || cam == null)
                return 0f;
            return Mathf.Clamp((cam.WorldToViewportPoint(at.Value).x - 0.5f) * 0.6f, -0.4f, 0.4f);
        }

        /// <summary>Loudness pass (2026-11-01): big moments brought down towards the unit sounds, about -20 LUFS. Others play as cut.</summary>
        private static readonly Dictionary<string, float> Levels = new Dictionary<string, float>
        {
            ["sun_impact"] = 0.5f,
            ["sun_charge"] = 0.6f,
            ["sun_mark"] = 0.75f,
            ["lightning_strike"] = 0.75f,
            ["moon_cast"] = 0.8f,
        };

        /// <summary>Plays a sound once. <paramref name="pitch"/> varies it a little each time so repeats don't sound identical.</summary>
        public static void Play(string name, Vector3? at = null, float volume = 1f, float pitch = 0.05f)
        {
            var clip = Clip(name);
            if (clip == null)
                return;
            _ = Host;
            var source = FreeVoice();
            source.clip = clip;
            source.loop = false;
            source.volume = volume * (Levels.TryGetValue(name, out var level) ? level : 1f) * Gain;
            source.pitch = 1f + Random.Range(-pitch, pitch);
            source.panStereo = Pan(at);
            source.Play();
        }

        /// <summary>A voice not playing, or else the one closest to finishing.</summary>
        private static AudioSource FreeVoice()
        {
            AudioSource best = null;
            var bestLeft = float.MaxValue;
            foreach (var voice in _voices)
            {
                if (!voice.isPlaying)
                    return voice;
                var left = voice.clip.length - voice.time;
                if (left < bestLeft)
                {
                    bestLeft = left;
                    best = voice;
                }
            }

            return best;
        }

        /// <summary>Starts a looping sound that fades in; stop it through the returned handle. Null if it has no clip yet.</summary>
        public static SfxLoop Loop(string name, Vector3? at = null, float volume = 1f, float fadeIn = 0.4f)
        {
            var clip = Clip(name);
            if (clip == null)
                return null;
            var loop = new GameObject($"Sfx loop {name}").AddComponent<SfxLoop>();
            loop.transform.SetParent(Host.transform, false);
            var source = NewSource(loop.gameObject);
            source.clip = clip;
            source.loop = true;
            source.panStereo = Pan(at);
            source.time = Random.Range(0f, clip.length);
            loop.Begin(source, volume, fadeIn);
            return loop;
        }

        internal static float LoopGain => Gain;
    }

    /// <summary>A looping sound started by <see cref="Sfx.Loop"/>. Fades in, follows the volume settings, fades out on Stop.</summary>
    public sealed class SfxLoop : MonoBehaviour
    {
        private AudioSource _source;
        private float _volume;
        private float _fade;
        private float _fadeSpeed;

        internal void Begin(AudioSource source, float volume, float fadeIn)
        {
            _source = source;
            _volume = volume;
            _fadeSpeed = fadeIn > 0f ? 1f / fadeIn : float.MaxValue;
            _source.volume = 0f;
            _source.Play();
        }

        /// <summary>Fades out and removes itself.</summary>
        public void Stop(float fadeOut = 0.3f)
        {
            if (this == null)
                return;
            _fadeSpeed = fadeOut > 0f ? -1f / fadeOut : float.MinValue;
        }

        private void Update()
        {
            _fade = Mathf.Clamp01(_fade + _fadeSpeed * Time.unscaledDeltaTime);
            _source.volume = _volume * _fade * Sfx.LoopGain;
            if (_fadeSpeed < 0f && _fade <= 0f)
                Destroy(gameObject);
        }
    }
}
