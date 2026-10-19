using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The game's music, composed and synthesised in code at startup (no audio files):
///  - "Nebula Dawn"  (start screen): slow dreamy pads, a glassy bell arpeggio, soft sub.
///  - "Overdrive"    (in a run): 124 BPM driving synthwave - kick, clap, hats, pumping bass,
///                    16th-note arpeggio with echo, wide pads.
///  - "Aftermath"    (results): a slow, warm minor pad with a lonely bell melody.
/// Each loop is rendered on a worker thread (rendered twice and the second pass kept, so echo
/// tails wrap seamlessly), then cross-faded in when its scene loads. Follows the Audio setting,
/// ducks while the game is paused, and never touches gameplay.
/// </summary>
public sealed class ProceduralSoundtrack : MonoBehaviour
{
    private const int SampleRate = 32000;
    private const float MusicVolume = 0.32f;
    private const float FadeSeconds = 1.4f;

    private enum Track
    {
        None,
        Menu,
        Game,
        Results
    }

    private static ProceduralSoundtrack instance;

    private readonly Dictionary<Track, AudioClip> clips = new Dictionary<Track, AudioClip>();
    private readonly Dictionary<Track, Task<float[]>> pending = new Dictionary<Track, Task<float[]>>();
    private AudioSource[] sources;
    private int activeSource;
    private Track wanted = Track.None;
    private Track playing = Track.None;
    private float audioCheckTimer;
    private bool audioEnabled = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance)
        {
            return;
        }

        var go = new GameObject("ProceduralSoundtrack");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ProceduralSoundtrack>();
    }

    private void Awake()
    {
        sources = new AudioSource[2];
        for (int i = 0; i < sources.Length; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.ignoreListenerPause = true;
            source.priority = 0;
            sources[i] = source;
        }

        // Start composing every track right away so later scenes never wait.
        Request(Track.Menu);
        Request(Track.Game);
        Request(Track.Results);

        SceneManager.sceneLoaded += HandleSceneLoaded;
        wanted = TrackFor(SceneManager.GetActiveScene().name);
        audioEnabled = FeedbackPreferences.IsAudioEnabled(null);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        foreach (AudioClip clip in clips.Values)
        {
            if (clip)
            {
                Destroy(clip);
            }
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        wanted = TrackFor(scene.name);
    }

    private static Track TrackFor(string sceneName)
    {
        if (sceneName == SceneNames.Game)
        {
            return Track.Game;
        }

        if (sceneName == SceneNames.Results)
        {
            return Track.Results;
        }

        return Track.Menu;
    }

    private void Request(Track track)
    {
        if (clips.ContainsKey(track) || pending.ContainsKey(track))
        {
            return;
        }

        switch (track)
        {
            case Track.Menu:
                pending[track] = Task.Run(() => Composer.NebulaDawn(SampleRate));
                break;
            case Track.Game:
                pending[track] = Task.Run(() => Composer.Overdrive(SampleRate));
                break;
            case Track.Results:
                pending[track] = Task.Run(() => Composer.Aftermath(SampleRate));
                break;
        }
    }

    private void Update()
    {
        CollectFinishedTracks();

        audioCheckTimer -= Time.unscaledDeltaTime;
        if (audioCheckTimer <= 0f)
        {
            audioCheckTimer = 0.4f;
            audioEnabled = FeedbackPreferences.IsAudioEnabled(null);
        }

        if (wanted != playing && clips.TryGetValue(wanted, out AudioClip clip) && clip)
        {
            activeSource = 1 - activeSource;
            AudioSource next = sources[activeSource];
            next.clip = clip;
            next.volume = 0f;
            next.time = 0f;
            next.Play();
            playing = wanted;
        }

        bool paused = GameStateMachine.HasInstance && GameStateMachine.Instance.CurrentState == GameStateKind.Paused;
        float target = audioEnabled ? MusicVolume * (paused ? 0.35f : 1f) : 0f;
        float step = Time.unscaledDeltaTime / FadeSeconds * MusicVolume;
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            float goal = i == activeSource ? target : 0f;
            source.volume = Mathf.MoveTowards(source.volume, goal, step);
            if (i != activeSource && source.isPlaying && source.volume <= 0.0001f)
            {
                source.Stop();
            }
        }
    }

    private void CollectFinishedTracks()
    {
        if (pending.Count == 0)
        {
            return;
        }

        Track done = Track.None;
        foreach (KeyValuePair<Track, Task<float[]>> entry in pending)
        {
            if (entry.Value.IsCompleted)
            {
                done = entry.Key;
                break;
            }
        }

        if (done == Track.None)
        {
            return;
        }

        Task<float[]> task = pending[done];
        pending.Remove(done);
        if (task.Status != TaskStatus.RanToCompletion || task.Result == null)
        {
            Debug.LogWarning($"ProceduralSoundtrack: could not compose {done}: {task.Exception?.GetBaseException().Message}");
            return;
        }

        float[] data = task.Result;
        var clip = AudioClip.Create("Music_" + done, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        clips[done] = clip;
    }

    // ==================================================================== composition

    /// <summary>Pure C# synthesis (no Unity API), so it can run on a worker thread.</summary>
    private static class Composer
    {
        private const float Tau = 6.2831853f;

        private enum Wave
        {
            Sine,
            Saw,
            Square,
            Triangle,
            Bell
        }

        private sealed class Bus
        {
            public readonly float[] Data;
            public readonly int Rate;

            public Bus(int length, int rate)
            {
                Data = new float[length];
                Rate = rate;
            }
        }

        private static float Midi(float note) => 440f * (float)System.Math.Pow(2.0, (note - 69f) / 12f);

        // ------------------------------------------------------------ tracks

        public static float[] Overdrive(int rate)
        {
            const float bpm = 124f;
            const int bars = 8;
            float beat = 60f / bpm;
            int loop = (int)(bars * 4 * beat * rate);
            // Render the loop twice; keep the second pass so echoes from the end wrap into the start.
            int length = loop * 2;
            var drums = new Bus(length, rate);
            var music = new Bus(length, rate);
            var lead = new Bus(length, rate);

            // Am - F - C - G | Am - F - G - E
            int[][] chords =
            {
                new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 60, 64, 67 }, new[] { 55, 59, 62 },
                new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 }, new[] { 52, 56, 59 },
            };
            int[] roots = { 45, 41, 48, 43, 45, 41, 43, 40 };

            var random = new System.Random(124);
            for (int pass = 0; pass < 2; pass++)
            {
                float offset = pass * loop / (float)rate;
                for (int bar = 0; bar < bars; bar++)
                {
                    float barStart = offset + bar * 4 * beat;
                    int[] chord = chords[bar];

                    for (int b = 0; b < 4; b++)
                    {
                        float t = barStart + b * beat;
                        Kick(drums, t);
                        if (b == 1 || b == 3)
                        {
                            Clap(drums, t, random);
                        }

                        Hat(drums, t + beat * 0.5f, 0.22f, random);
                        Hat(drums, t + beat * 0.25f, 0.07f, random);
                        Hat(drums, t + beat * 0.75f, 0.07f, random);
                    }

                    // Pumping 8th-note bass: root, root, octave, root ...
                    for (int e = 0; e < 8; e++)
                    {
                        float note = roots[bar] + (e % 4 == 2 ? 12 : 0);
                        Note(music, barStart + e * beat * 0.5f, beat * 0.42f, Midi(note), Wave.Saw, 0.26f, 0.004f, 0.06f, 0.09f);
                        Note(music, barStart + e * beat * 0.5f, beat * 0.42f, Midi(note - 12), Wave.Sine, 0.22f, 0.004f, 0.06f, 1f);
                    }

                    // Wide pad: two detuned saws per chord tone.
                    for (int n = 0; n < chord.Length; n++)
                    {
                        Note(music, barStart, 4 * beat, Midi(chord[n] + 12) * 1.003f, Wave.Saw, 0.055f, 0.25f, 0.4f, 0.05f);
                        Note(music, barStart, 4 * beat, Midi(chord[n] + 12) * 0.997f, Wave.Saw, 0.055f, 0.25f, 0.4f, 0.05f);
                    }

                    // 16th-note arpeggio up and down the chord, an octave higher on the second half.
                    int[] pattern = { 0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2, 1, 0, 2, 1, 3 };
                    for (int s = 0; s < 16; s++)
                    {
                        int step = pattern[s];
                        float note = step == 3 ? chord[0] + 12 : chord[step];
                        note += bar >= 4 ? 24 : 12;
                        float accent = s % 4 == 0 ? 1f : 0.7f;
                        Note(lead, barStart + s * beat * 0.25f, beat * 0.2f, Midi(note), Wave.Square, 0.075f * accent, 0.002f, 0.07f, 0.16f);
                    }
                }
            }

            Echo(lead, beat * 0.75f, 0.38f);
            Sidechain(music, beat, 0.55f, length, rate, loop);
            return Finish(Mix(drums, music, lead, 1f, 0.9f, 0.85f), loop);
        }

        public static float[] NebulaDawn(int rate)
        {
            const float bpm = 80f;
            const int bars = 8;
            float beat = 60f / bpm;
            int loop = (int)(bars * 4 * beat * rate);
            int length = loop * 2;
            var pads = new Bus(length, rate);
            var bells = new Bus(length, rate);

            // Am(add9) - Fmaj7 - Cmaj7 - G6, two bars each.
            int[][] chords =
            {
                new[] { 57, 60, 64, 71 }, new[] { 53, 57, 60, 64 }, new[] { 48, 55, 59, 64 }, new[] { 55, 59, 62, 64 },
            };
            int[] roots = { 45, 41, 36, 43 };

            for (int pass = 0; pass < 2; pass++)
            {
                float offset = pass * loop / (float)rate;
                for (int c = 0; c < chords.Length; c++)
                {
                    float start = offset + c * 8 * beat;
                    int[] chord = chords[c];
                    for (int n = 0; n < chord.Length; n++)
                    {
                        Note(pads, start, 8 * beat, Midi(chord[n]) * 1.004f, Wave.Saw, 0.05f, 1.6f, 2.2f, 0.035f);
                        Note(pads, start, 8 * beat, Midi(chord[n]) * 0.996f, Wave.Triangle, 0.07f, 1.6f, 2.2f, 1f);
                    }

                    Note(pads, start, 8 * beat, Midi(roots[c] - 12), Wave.Sine, 0.2f, 0.8f, 1.5f, 1f);

                    // Glassy bells drifting over the chord.
                    int[] order = { 0, 2, 3, 1, 2, 3, 0, 2, 1, 3, 2, 0, 3, 1, 2, 3 };
                    for (int s = 0; s < 16; s++)
                    {
                        float note = chord[order[s]] + 24;
                        float t = start + s * beat * 0.5f;
                        Note(bells, t, beat * 1.5f, Midi(note), Wave.Bell, s % 4 == 0 ? 0.09f : 0.055f, 0.003f, 1.2f, 1f);
                    }
                }
            }

            Echo(bells, beat * 0.75f, 0.5f);
            return Finish(Mix(pads, bells, null, 1f, 0.8f, 0f), loop);
        }

        public static float[] Aftermath(int rate)
        {
            const float bpm = 70f;
            const int bars = 8;
            float beat = 60f / bpm;
            int loop = (int)(bars * 4 * beat * rate);
            int length = loop * 2;
            var pads = new Bus(length, rate);
            var bells = new Bus(length, rate);

            // Dm - Bb - F - A, two bars each.
            int[][] chords =
            {
                new[] { 50, 57, 62, 65 }, new[] { 46, 53, 58, 62 }, new[] { 53, 57, 60, 65 }, new[] { 45, 57, 61, 64 },
            };
            // A lonely melody, one phrase per chord (scale steps in D minor).
            int[][] melody =
            {
                new[] { 74, 72, 69 }, new[] { 70, 69, 65 }, new[] { 72, 69, 67 }, new[] { 69, 68, 69 },
            };

            for (int pass = 0; pass < 2; pass++)
            {
                float offset = pass * loop / (float)rate;
                for (int c = 0; c < chords.Length; c++)
                {
                    float start = offset + c * 8 * beat;
                    int[] chord = chords[c];
                    for (int n = 0; n < chord.Length; n++)
                    {
                        Note(pads, start, 8 * beat, Midi(chord[n]) * 1.003f, Wave.Saw, 0.045f, 2f, 2.5f, 0.03f);
                        Note(pads, start, 8 * beat, Midi(chord[n]), Wave.Sine, 0.08f, 2f, 2.5f, 1f);
                    }

                    int[] phrase = melody[c];
                    for (int m = 0; m < phrase.Length; m++)
                    {
                        Note(bells, start + (m * 2 + 1) * beat, beat * 2.2f, Midi(phrase[m]), Wave.Bell, 0.1f, 0.003f, 1.6f, 1f);
                    }
                }
            }

            Echo(bells, beat * 1f, 0.45f);
            return Finish(Mix(pads, bells, null, 1f, 0.75f, 0f), loop);
        }

        // ------------------------------------------------------------ instruments

        private static void Kick(Bus bus, float time)
        {
            int start = (int)(time * bus.Rate);
            int length = (int)(0.32f * bus.Rate);
            float phase = 0f;
            for (int i = 0; i < length && start + i < bus.Data.Length; i++)
            {
                float t = i / (float)bus.Rate;
                float frequency = 48f + 110f * (float)System.Math.Exp(-t * 28f);
                phase += Tau * frequency / bus.Rate;
                float envelope = (float)System.Math.Exp(-t * 9f);
                float click = t < 0.004f ? 0.5f * (1f - t / 0.004f) : 0f;
                bus.Data[start + i] += ((float)System.Math.Sin(phase) * envelope + click) * 0.85f;
            }
        }

        private static void Clap(Bus bus, float time, System.Random random)
        {
            int start = (int)(time * bus.Rate);
            int length = (int)(0.22f * bus.Rate);
            float band = 0f;
            float previous = 0f;
            for (int i = 0; i < length && start + i < bus.Data.Length; i++)
            {
                float t = i / (float)bus.Rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                // Crude band-pass: high-pass then low-pass.
                float high = noise - previous;
                previous = noise;
                band += (high - band) * 0.45f;
                // Three quick bursts then a tail, like hands clapping together.
                float bursts = t < 0.03f ? (float)System.Math.Exp(-((t * 1000f) % 10f) * 0.35f) : 1f;
                float envelope = (float)System.Math.Exp(-t * 18f) * bursts;
                bus.Data[start + i] += band * envelope * 0.45f;
            }
        }

        private static void Hat(Bus bus, float time, float volume, System.Random random)
        {
            int start = (int)(time * bus.Rate);
            int length = (int)(0.06f * bus.Rate);
            float previous = 0f;
            for (int i = 0; i < length && start + i < bus.Data.Length; i++)
            {
                float t = i / (float)bus.Rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                float high = noise - previous;
                previous = noise;
                bus.Data[start + i] += high * (float)System.Math.Exp(-t * 70f) * volume;
            }
        }

        // One note with an attack/release envelope and a one-pole low-pass (cutoff 0..1, 1 = open).
        private static void Note(Bus bus, float time, float duration, float frequency, Wave wave, float volume, float attack, float release, float cutoff)
        {
            int start = (int)(time * bus.Rate);
            int length = (int)((duration + release) * bus.Rate);
            // Phase starts at zero so a note sounds identical in both render passes (seamless loop).
            float phase = 0f;
            float increment = frequency / bus.Rate;
            float filtered = 0f;
            for (int i = 0; i < length && start + i < bus.Data.Length; i++)
            {
                float t = i / (float)bus.Rate;
                float envelope = t < attack ? t / attack
                    : t < duration ? 1f
                    : 1f - (t - duration) / release;
                if (t >= duration && envelope <= 0f)
                {
                    break;
                }

                phase += increment;
                phase -= (float)System.Math.Floor(phase);
                float sample;
                switch (wave)
                {
                    case Wave.Saw:
                        sample = phase * 2f - 1f;
                        break;
                    case Wave.Square:
                        sample = phase < 0.5f ? 0.8f : -0.8f;
                        break;
                    case Wave.Triangle:
                        sample = 1f - 4f * System.Math.Abs(phase - 0.5f);
                        break;
                    case Wave.Bell:
                        // FM bell: bright strike that mellows as it rings.
                        float modulation = (float)System.Math.Sin(Tau * phase * 3.5f) * 2.2f * (float)System.Math.Exp(-t * 5f);
                        sample = (float)System.Math.Sin(Tau * phase + modulation);
                        envelope *= (float)System.Math.Exp(-t * 2.2f);
                        break;
                    default:
                        sample = (float)System.Math.Sin(Tau * phase);
                        break;
                }

                filtered += (sample - filtered) * cutoff;
                bus.Data[start + i] += filtered * envelope * volume;
            }
        }

        // Feedback echo with a gentle high cut on each repeat.
        private static void Echo(Bus bus, float delaySeconds, float feedback)
        {
            int delay = (int)(delaySeconds * bus.Rate);
            float tone = 0f;
            for (int i = delay; i < bus.Data.Length; i++)
            {
                tone += (bus.Data[i - delay] - tone) * 0.5f;
                bus.Data[i] += tone * feedback;
            }
        }

        // Ducks the music on every beat (where the kick lands) for that pumping dance feel.
        private static void Sidechain(Bus bus, float beat, float depth, int length, int rate, int loop)
        {
            int beatSamples = (int)(beat * rate);
            for (int i = 0; i < length; i++)
            {
                float sinceKick = (i % loop % beatSamples) / (float)rate;
                float gain = 1f - depth * (float)System.Math.Exp(-sinceKick * 14f);
                bus.Data[i] *= gain;
            }
        }

        private static float[] Mix(Bus a, Bus b, Bus c, float gainA, float gainB, float gainC)
        {
            var output = new float[a.Data.Length];
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = a.Data[i] * gainA + b.Data[i] * gainB + (c != null ? c.Data[i] * gainC : 0f);
            }

            return output;
        }

        // Keeps the second pass (tails already wrapped in), normalises and soft-clips.
        private static float[] Finish(float[] rendered, int loop)
        {
            var output = new float[loop];
            System.Array.Copy(rendered, loop, output, 0, loop);
            float peak = 0.0001f;
            for (int i = 0; i < output.Length; i++)
            {
                peak = System.Math.Max(peak, System.Math.Abs(output[i]));
            }

            float gain = 1.1f / peak;
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = (float)System.Math.Tanh(output[i] * gain) * 0.92f;
            }

            return output;
        }
    }
}
