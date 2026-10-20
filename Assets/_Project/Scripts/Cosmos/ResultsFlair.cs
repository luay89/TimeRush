using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drama for the results screen, layered on top of whatever ResultsController built:
///  - the title becomes a glitching "SHIP DESTROYED" (or a golden "NEW RECORD!"),
///  - the final score counts up with ticking blips and lands with a punch,
///  - a new personal best bursts into confetti with a short fanfare.
/// Only restyles existing labels and adds decoration; never changes what the labels say about
/// the run (the score text ends exactly as ResultsController wrote it).
/// </summary>
public sealed class ResultsFlair : MonoBehaviour
{
    private const float CountSeconds = 1.3f;
    private static readonly Regex LastNumber = new Regex(@"(\d+)(?!.*\d)");

    private TextMeshProUGUI title;
    private Vector2 titleBasePosition;
    private Color titleColor;
    private string titleText;
    private float nextGlitch;
    private float glitchUntil;
    private AudioSource audioSource;
    private AudioClip blipClip;
    private AudioClip fanfareNote;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void TryInstall(Scene scene)
    {
        if (scene.name != SceneNames.Results || FindObjectOfType<ResultsFlair>())
        {
            return;
        }

        var go = new GameObject("ResultsFlair");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<ResultsFlair>();
    }

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        blipClip = CreateTone("Blip", 1318.5f, 0.05f, 60f);
        fanfareNote = CreateTone("Fanfare", 523.25f, 0.6f, 5f);
    }

    private void OnDestroy()
    {
        if (blipClip)
        {
            Destroy(blipClip);
        }

        if (fanfareNote)
        {
            Destroy(fanfareNote);
        }
    }

    private IEnumerator Start()
    {
        // Let ResultsController build/arrange its UI and write the labels first; keep looking for
        // a little while in case it builds them late.
        TextMeshProUGUI score = null;
        for (int frame = 0; frame < 60 && !score; frame++)
        {
            yield return null;
            score = FindLabel("FinalScoreText");
        }

        bool newRecord = ScoreSnapshot.HasValue && ScoreSnapshot.LastRunSetNewBest && ScoreSnapshot.LastScore > 0;
        try
        {
            StyleTitle(newRecord);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        Debug.Log($"ResultsFlair: title {(title ? title.name : "not found")}, score {(score ? score.name : "not found")}, new record {newRecord}");
        if (score)
        {
            score.color = newRecord ? new Color(1f, 0.85f, 0.3f, 1f) : score.color;
            yield return CountUp(score);
            if (newRecord)
            {
                StartCoroutine(Fanfare());
                Confetti(score.rectTransform);
            }
        }
    }

    private void StyleTitle(bool newRecord)
    {
        title = FindLabel("GameOverTitle");
        if (!title)
        {
            foreach (TextMeshProUGUI label in FindObjectsOfType<TextMeshProUGUI>())
            {
                string text = label.text != null ? label.text.Trim().ToUpperInvariant() : string.Empty;
                if (text == "GAME OVER" || text == "RUN ENDED")
                {
                    title = label;
                    break;
                }
            }
        }

        if (!title)
        {
            return;
        }

        titleText = newRecord ? "NEW RECORD!" : "SHIP DESTROYED";
        titleColor = newRecord ? new Color(1f, 0.82f, 0.25f, 1f) : new Color(1f, 0.32f, 0.38f, 1f);
        title.text = titleText;
        title.color = titleColor;
        title.fontStyle = FontStyles.Bold | FontStyles.Italic;
        title.characterSpacing = 6f;
        title.outlineWidth = 0.18f;
        title.outlineColor = newRecord ? new Color32(90, 40, 0, 255) : new Color32(40, 0, 20, 255);
        titleBasePosition = title.rectTransform.anchoredPosition;
        nextGlitch = Time.unscaledTime + 0.4f;
    }

    private void Update()
    {
        if (!title)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now >= nextGlitch)
        {
            glitchUntil = now + Random.Range(0.08f, 0.18f);
            nextGlitch = now + Random.Range(1.6f, 3.2f);
        }

        if (now < glitchUntil)
        {
            // Signal breaking up: jitter, colour split and a corrupted character or two.
            title.rectTransform.anchoredPosition = titleBasePosition + new Vector2(Random.Range(-10f, 10f), Random.Range(-3f, 3f));
            title.color = Random.value < 0.5f ? new Color(0.3f, 0.95f, 1f, 1f) : new Color(1f, 0.2f, 0.7f, 1f);
            char[] chars = titleText.ToCharArray();
            int index = Random.Range(0, chars.Length);
            if (chars[index] != ' ')
            {
                chars[index] = "#%&*/<>?"[Random.Range(0, 8)];
            }

            title.text = new string(chars);
        }
        else if (title.text != titleText)
        {
            title.rectTransform.anchoredPosition = titleBasePosition;
            title.color = titleColor;
            title.text = titleText;
        }
        else
        {
            // Slow glow breathing between glitches.
            float pulse = 0.85f + 0.15f * Mathf.Sin(now * 3f);
            title.color = new Color(titleColor.r * pulse, titleColor.g * pulse, titleColor.b * pulse, 1f);
        }
    }

    private IEnumerator CountUp(TextMeshProUGUI score)
    {
        string finalText = score.text;
        Match match = LastNumber.Match(finalText ?? string.Empty);
        if (!match.Success || !int.TryParse(match.Value, out int target) || target <= 0)
        {
            yield break;
        }

        string prefix = finalText.Substring(0, match.Index);
        string suffix = finalText.Substring(match.Index + match.Length);
        float start = Time.unscaledTime;
        float nextBlip = 0f;
        while (true)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - start) / CountSeconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            int value = Mathf.RoundToInt(target * eased);
            score.text = prefix + value + suffix;
            if (Time.unscaledTime >= nextBlip && t < 1f)
            {
                nextBlip = Time.unscaledTime + 0.06f;
                PlayClip(blipClip, 0.18f, 0.9f + t * 0.5f);
            }

            if (t >= 1f)
            {
                break;
            }

            yield return null;
        }

        score.text = finalText;
        // Landing punch.
        Transform scoreTransform = score.transform;
        float punchStart = Time.unscaledTime;
        while (Time.unscaledTime - punchStart < 0.3f)
        {
            float p = (Time.unscaledTime - punchStart) / 0.3f;
            scoreTransform.localScale = Vector3.one * (1f + 0.22f * Mathf.Sin(p * Mathf.PI) * (1f - p));
            yield return null;
        }

        scoreTransform.localScale = Vector3.one;
    }

    private IEnumerator Fanfare()
    {
        // C - E - G - C, bright and quick.
        float[] semitones = { 0f, 4f, 7f, 12f };
        for (int i = 0; i < semitones.Length; i++)
        {
            PlayClip(fanfareNote, 0.4f, Mathf.Pow(2f, semitones[i] / 12f));
            yield return new WaitForSecondsRealtime(0.11f);
        }
    }

    private void PlayClip(AudioClip clip, float volume, float pitch)
    {
        if (!clip || !FeedbackPreferences.IsAudioEnabled(null))
        {
            return;
        }

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume);
    }

    private void Confetti(RectTransform anchor)
    {
        Canvas canvas = anchor.GetComponentInParent<Canvas>();
        if (!canvas)
        {
            return;
        }

        Canvas rootCanvas = canvas.rootCanvas;
        var layer = new GameObject("Confetti", typeof(RectTransform));
        layer.transform.SetParent(rootCanvas.transform, false);
        layer.transform.SetAsLastSibling();
        var layerRect = (RectTransform)layer.transform;
        layerRect.anchorMin = Vector2.zero;
        layerRect.anchorMax = Vector2.one;
        layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;

        Vector3 origin = layer.transform.InverseTransformPoint(anchor.position);
        Color[] palette =
        {
            new Color(1f, 0.82f, 0.25f), new Color(0.3f, 0.95f, 1f), new Color(1f, 0.35f, 0.8f), new Color(0.6f, 1f, 0.5f), Color.white,
        };
        for (int i = 0; i < 70; i++)
        {
            var piece = new GameObject("Bit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            piece.transform.SetParent(layer.transform, false);
            var rect = (RectTransform)piece.transform;
            rect.sizeDelta = new Vector2(Random.Range(8f, 16f), Random.Range(14f, 26f));
            rect.localPosition = origin;
            var image = piece.GetComponent<Image>();
            image.color = palette[i % palette.Length];
            image.raycastTarget = false;
            float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
            var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(500f, 1200f);
            StartCoroutine(Fly(rect, image, velocity, Random.Range(-720f, 720f)));
        }

        Destroy(layer, 3f);
    }

    private static IEnumerator Fly(RectTransform rect, Image image, Vector2 velocity, float spin)
    {
        float age = 0f;
        Color color = image.color;
        while (age < 2.6f && rect)
        {
            float dt = Time.unscaledDeltaTime;
            age += dt;
            velocity += Vector2.down * 1400f * dt;
            velocity *= 1f - 0.9f * dt;
            rect.localPosition += (Vector3)(velocity * dt);
            rect.Rotate(0f, 0f, spin * dt);
            color.a = Mathf.Clamp01(2.6f - age);
            image.color = color;
            yield return null;
        }
    }

    private static TextMeshProUGUI FindLabel(string objectName)
    {
        foreach (TextMeshProUGUI label in FindObjectsOfType<TextMeshProUGUI>())
        {
            if (label.name == objectName)
            {
                return label;
            }
        }

        return null;
    }

    private static AudioClip CreateTone(string name, float frequency, float seconds, float decay)
    {
        const int rate = 44100;
        int length = (int)(rate * seconds);
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)rate;
            float envelope = Mathf.Min(1f, t * 500f) * Mathf.Exp(-t * decay);
            float sample = Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * frequency * t);
            data[i] = sample * envelope * 0.4f;
        }

        var clip = AudioClip.Create(name, length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
