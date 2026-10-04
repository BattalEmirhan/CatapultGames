using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // Procedural sound hub, the audio twin of GameFX.
    //
    // Auto-instantiates on first use (GameAudio.Play), so there is no scene wiring
    // and no GameplaySceneBuilder reference to lose. The project ships no audio
    // assets, so every clip is synthesised once, when the hub wakes, from a few
    // lines of maths: plucks, bells and filtered noise, tuned to the toy-block look.
    //
    // Replacing a sound with a real recording needs no code: put a clip named
    // after the Sfx value in Assets/Resources/Audio/ (e.g. Audio/CellTick.wav).
    // A clip found there always wins over the synthesised one.
    //
    // One-shots play on a small round-robin pool of AudioSources rather than one
    // source's PlayOneShot, because pitch is per-source in Unity and the paint
    // wave plays a rising scale.
    public sealed class GameAudio : MonoBehaviour
    {
        // Persisted across sessions. No UI toggles it yet; a settings screen
        // only has to flip this.
        public static bool Muted
        {
            get
            {
                if (_muted < 0)
                    _muted = PlayerPrefs.GetInt(MutedKey, 0);
                return _muted == 1;
            }
            set
            {
                _muted = value ? 1 : 0;
                PlayerPrefs.SetInt(MutedKey, _muted);
            }
        }

        private static GameAudio Instance
        {
            get
            {
                // A destroyed hub reads as null (Unity's ==), so a scene reload
                // transparently builds a fresh one, exactly like GameFX.
                if (_instance == null)
                    _instance = new GameObject("GameAudio").AddComponent<GameAudio>();
                return _instance;
            }
        }

        private const string ResourceFolder = "Audio/";
        private const string MutedKey       = "SoundMuted";
        private const int    PoolSize       = 10;
        private const int    Rate           = 44100;

        // Per-sound mix. The paint wave fires a tick per cube, so the tick sits
        // well under the one-off events.
        private static readonly Dictionary<Sfx, float> Mix = new()
        {
            { Sfx.Launch, 0.55f }, { Sfx.Land, 0.75f }, { Sfx.CellTick, 0.38f },
            { Sfx.IceCrack, 0.6f }, { Sfx.ColorFanfare, 0.7f }, { Sfx.Praise, 0.6f },
            { Sfx.Win, 0.8f }, { Sfx.Lose, 0.6f }, { Sfx.Booster, 0.65f },
            { Sfx.Warning, 0.6f }, { Sfx.Undo, 0.5f }, { Sfx.Pop, 0.45f },
        };

        // Major pentatonic, in semitones — any run of it sounds pleasant, which is
        // what lets a wave of 3 or 25 cubes climb without ever hitting a sour note.
        private static readonly int[] Pentatonic = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };
        private static GameAudio _instance;
        private static int       _muted = -1;   // lazy: PlayerPrefs is not allowed in static initialisers
        private readonly Dictionary<Sfx, AudioClip> _clips     = new();
        private readonly List<AudioClip>            _generated = new();   // ours to destroy
        private AudioSource[] _pool;
        private int           _next;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < _pool.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake  = false;
                src.spatialBlend = 0f;   // UI-style 2D sound; the camera moves, the mix should not
                _pool[i] = src;
            }

            // Scenes built before the builder added one have no listener, and
            // without it every sound is silently dropped.
            if (FindAnyObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();

            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                var clip = Resources.Load<AudioClip>(ResourceFolder + sfx);
                if (clip == null)
                {
                    clip = Synthesize(sfx);
                    _generated.Add(clip);
                }
                _clips[sfx] = clip;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            foreach (var clip in _generated)
                if (clip)
                    Destroy(clip);
        }

        public static void Play(Sfx sfx, float pitch = 1f, float volume = 1f)
        {
            if (!Application.isPlaying || Muted)
                return;
            Instance.PlayClip(sfx, pitch, volume);
        }

        // The index-th cube of one shot's paint wave: each one a step up the scale,
        // holding the top note on very long waves.
        public static void PlayTick(int index)
        {
            int semis = Pentatonic[Mathf.Clamp(index, 0, Pentatonic.Length - 1)];
            Play(Sfx.CellTick, Semitones(semis));
        }

        // tier 0 = GOOD, 1 = GREAT!, 2 = AMAZING! — the same bell, higher and louder.
        public static void PlayPraise(int tier)
        {
            tier = Mathf.Clamp(tier, 0, 2);
            Play(Sfx.Praise, Semitones(tier * 3), 0.8f + 0.1f * tier);
        }

        private static float Semitones(int s) => Mathf.Pow(2f, s / 12f);

        private void PlayClip(Sfx sfx, float pitch, float volume)
        {
            if (!_clips.TryGetValue(sfx, out var clip) || clip == null)
                return;

            // Prefer an idle source; if all ten are busy, cut the oldest.
            AudioSource src = null;
            for (int i = 0; i < _pool.Length && src == null; i++)
            {
                var candidate = _pool[(_next + i) % _pool.Length];
                if (!candidate.isPlaying)
                    src = candidate;
            }
            if (src == null)
                src = _pool[_next];
            _next = (_next + 1) % _pool.Length;

            src.clip   = clip;
            src.pitch  = Mathf.Clamp(pitch, 0.25f, 3f);
            src.volume = Mathf.Clamp01(volume * Mix[sfx]);
            src.Play();
        }

        private static AudioClip Synthesize(Sfx sfx)
        {
            float[] b;
            switch (sfx)
            {
                case Sfx.Launch:
                    b = Buffer(0.26f);
                    Noise(b, 0f, 0.24f, 0.45f, attack: 0.06f, decay: 0.07f, cutoffFrom: 0.04f, cutoffTo: 0.30f);
                    Tone(b, 0f, 0.14f, 260f, 640f, SynthWave.Sine, 0.35f, 0.004f, 0.05f);
                    break;

                case Sfx.Land:
                    b = Buffer(0.28f);
                    Tone(b, 0f, 0.26f, 150f, 55f, SynthWave.Sine, 0.9f, 0.002f, 0.07f);
                    Noise(b, 0f, 0.04f, 0.35f, attack: 0.001f, decay: 0.012f, cutoffFrom: 0.35f, cutoffTo: 0.10f);
                    break;

                case Sfx.CellTick:   // a bubbly "pop": short upward chirp around C5
                    b = Buffer(0.10f);
                    Tone(b, 0f, 0.09f, 520f, 860f, SynthWave.Sine, 0.6f, 0.002f, 0.028f);
                    Tone(b, 0f, 0.06f, 1040f, 1720f, SynthWave.Sine, 0.12f, 0.002f, 0.018f);
                    break;

                case Sfx.IceCrack:
                    b = Buffer(0.14f);
                    foreach (float at in new[] { 0f, 0.028f, 0.062f })
                        Noise(b, at, 0.04f, 0.55f, attack: 0.001f, decay: 0.008f, cutoffFrom: 0.9f, cutoffTo: 0.6f, highpass: true);
                    Tone(b, 0f, 0.06f, 1800f, 1650f, SynthWave.Sine, 0.12f, 0.001f, 0.02f);
                    break;

                case Sfx.ColorFanfare:   // C E G C' arpeggio, octave sparkle on top
                    b = Buffer(0.80f);
                    Arpeggio(b, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0f, 0.08f, 0.5f, SynthWave.Triangle, 0.32f, 0.18f);
                    Arpeggio(b, new[] { 1046.5f, 1318.5f, 1568f, 2093f }, 0.01f, 0.08f, 0.35f, SynthWave.Sine, 0.08f, 0.10f);
                    break;

                case Sfx.Praise:   // two-note bell, G5 → D6
                    b = Buffer(0.55f);
                    Bell(b, 0f,    783.99f, 0.45f);
                    Bell(b, 0.09f, 1174.7f, 0.50f);
                    break;

                case Sfx.Win:   // quick run, then a held major chord
                    b = Buffer(1.45f);
                    Arpeggio(b, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0f, 0.10f, 0.22f, SynthWave.Triangle, 0.30f, 0.09f);
                    foreach (float f in new[] { 523.25f, 659.25f, 783.99f, 1046.5f })
                        Tone(b, 0.45f, 0.95f, f, f, SynthWave.Triangle, 0.20f, 0.01f, 0.42f);
                    Bell(b, 0.45f, 2093f, 0.25f);
                    break;

                case Sfx.Lose:   // G4 → E4 → C4, the last one sagging
                    b = Buffer(0.85f);
                    Tone(b, 0f,    0.20f, 392f, 392f, SynthWave.Triangle, 0.35f, 0.005f, 0.10f);
                    Tone(b, 0.16f, 0.20f, 329.6f, 329.6f, SynthWave.Triangle, 0.35f, 0.005f, 0.10f);
                    Tone(b, 0.32f, 0.50f, 261.6f, 246.9f, SynthWave.Triangle, 0.40f, 0.005f, 0.22f);
                    break;

                case Sfx.Booster:   // rapid rising sparkle
                    b = Buffer(0.48f);
                    var sparkle = new float[8];
                    for (int i = 0; i < sparkle.Length; i++)
                        sparkle[i] = 1046.5f * Semitones(Pentatonic[i]);
                    Arpeggio(b, sparkle, 0f, 0.035f, 0.16f, SynthWave.Sine, 0.25f, 0.06f);
                    Noise(b, 0f, 0.40f, 0.06f, attack: 0.05f, decay: 0.12f, cutoffFrom: 0.9f, cutoffTo: 0.9f, highpass: true);
                    break;

                case Sfx.Warning:   // two soft, falling beeps
                    b = Buffer(0.40f);
                    Tone(b, 0f,    0.13f, 330f, 330f, SynthWave.Soft, 0.28f, 0.005f, 0.08f);
                    Tone(b, 0.17f, 0.18f, 262f, 262f, SynthWave.Soft, 0.28f, 0.005f, 0.10f);
                    break;

                case Sfx.Undo:   // a falling blip, the launch played backwards in spirit
                    b = Buffer(0.18f);
                    Tone(b, 0f, 0.16f, 700f, 320f, SynthWave.Sine, 0.5f, 0.003f, 0.07f);
                    break;

                default:   // Pop
                    b = Buffer(0.24f);
                    Noise(b, 0f, 0.10f, 0.5f, attack: 0.001f, decay: 0.035f, cutoffFrom: 0.5f, cutoffTo: 0.08f);
                    Tone(b, 0f, 0.20f, 220f, 90f, SynthWave.Sine, 0.45f, 0.002f, 0.06f);
                    break;
            }

            Finish(b);
            var clip = AudioClip.Create("Synth_" + sfx, b.Length, 1, Rate, false);
            clip.SetData(b, 0);
            return clip;
        }

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        // A pitched voice gliding (exponentially) from f0 to f1 over its duration.
        private static void Tone(float[] b, float start, float dur, float f0, float f1, SynthWave wave,
                                 float amp, float attack, float decay)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int n  = Mathf.Min(Mathf.RoundToInt(dur * Rate), b.Length - s0);
            float end = (float)n / Rate;   // a voice cut short by the buffer still fades out
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float f = f0 * Mathf.Pow(f1 / f0, t / dur);
                phase += f / Rate;
                float p = (float)(phase - System.Math.Floor(phase));

                float v = wave switch
                {
                    SynthWave.Triangle => 1f - 4f * Mathf.Abs(p - 0.5f),
                    SynthWave.Soft     => (float)System.Math.Tanh(3.0 * Mathf.Sin(2f * Mathf.PI * p)) * 0.8f,
                    _             => Mathf.Sin(2f * Mathf.PI * p),
                };
                b[s0 + i] += v * amp * Envelope(t, end, attack, decay);
            }
        }

        // White noise through a one-pole filter whose cutoff (0..1 of the filter
        // coefficient) sweeps over the voice — a whoosh opens, a thud closes.
        // `highpass` keeps what the filter removes instead: clicks and crackle.
        private static void Noise(float[] b, float start, float dur, float amp,
                                  float attack, float decay, float cutoffFrom, float cutoffTo,
                                  bool highpass = false)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int n  = Mathf.Min(Mathf.RoundToInt(dur * Rate), b.Length - s0);
            float end = (float)n / Rate;   // a voice cut short by the buffer still fades out
            var rng = new System.Random(s0 + n);   // fixed seed: the same sound every time
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float k = Mathf.Lerp(cutoffFrom, cutoffTo, t / dur);
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += k * (white - lp);
                float v = highpass ? white - lp : lp * 2f;   // the low-passed signal is quiet; lift it
                b[s0 + i] += v * amp * Envelope(t, end, attack, decay);
            }
        }

        private static void Arpeggio(float[] b, float[] freqs, float start, float step, float noteDur,
                                     SynthWave wave, float amp, float decay)
        {
            for (int i = 0; i < freqs.Length; i++)
                Tone(b, start + i * step, noteDur, freqs[i], freqs[i], wave, amp, 0.004f, decay);
        }

        // Struck-bell partials: inharmonic overtones are what make it read as a bell.
        private static void Bell(float[] b, float start, float f, float dur)
        {
            Tone(b, start, dur, f,         f,         SynthWave.Sine, 0.40f, 0.002f, dur * 0.40f);
            Tone(b, start, dur, f * 2.76f, f * 2.76f, SynthWave.Sine, 0.12f, 0.002f, dur * 0.20f);
            Tone(b, start, dur, f * 5.40f, f * 5.40f, SynthWave.Sine, 0.05f, 0.002f, dur * 0.10f);
        }

        // Linear attack, exponential decay, and a 4 ms fade at the voice's end so
        // no voice stops on a click.
        private static float Envelope(float t, float dur, float attack, float decay)
        {
            float env = t < attack ? t / Mathf.Max(attack, 1e-4f)
                                   : Mathf.Exp(-(t - attack) / Mathf.Max(decay, 1e-4f));
            float tail = dur - t;
            if (tail < 0.004f)
                env *= Mathf.Max(0f, tail / 0.004f);
            return env;
        }

        // Normalise to a common peak and soft-clip, so the per-sound Mix table
        // is the only loudness knob.
        private static void Finish(float[] b)
        {
            float peak = 0f;
            foreach (float v in b)
                peak = Mathf.Max(peak, Mathf.Abs(v));
            float gain = peak > 1e-5f ? 0.9f / peak : 1f;
            for (int i = 0; i < b.Length; i++)
                b[i] = (float)System.Math.Tanh(b[i] * gain * 1.1f) * 0.92f;
        }
    }
}
