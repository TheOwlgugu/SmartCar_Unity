using UnityEngine;

/// <summary>
/// 统一音效管理（只管"放声音"，不管"谁触发的"）。
/// 用法：
///   1. 场景里放一个空物体，挂上本组件（建议放 Menu，播放后自动跨场景保留）；
///   2. 把音效拖到 Click Clip；
///   3. 给每个要发声的按钮挂一个 ButtonSound（点击时它会来找这里播）。
/// </summary>
public class UiAudio : MonoBehaviour
{
    [Header("音效")]
    [Tooltip("按钮点击音（mp3 / wav 都可以；短音效建议用 wav 或 PCM 压缩）")]
    [SerializeField] private AudioClip clickClip;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;
    [SerializeField] private bool muted;
    [Tooltip("没指定 Click Clip 时，用代码合成的短促音兜底")]
    [SerializeField] private bool generateIfMissing;
    [Tooltip("启动时用 0 音量预播一次，强制解码，避免第一次点击有额外延迟")]
    [SerializeField] private bool warmupOnStart = true;

    private static UiAudio instance;
    private static bool warnedMissing;

    private AudioSource source;
    private AudioClip generated;
    private bool warnedNoClip;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;       // 2D，不受相机位置影响
        source.volume = 1f;             // 音量统一交给 PlayOneShot 的 volumeScale
        source.ignoreListenerPause = true;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Start()
    {
        if (warmupOnStart && clickClip != null && source != null)
        {
            source.PlayOneShot(clickClip, 0f);
        }
    }

    /// <summary>播放点击音。传 null 用 Click Clip，传了就用传入的那一个。</summary>
    public static void PlayClick(AudioClip clip = null)
    {
        if (instance == null)
        {
            if (!warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning("[UiAudio] 场景里没有 UiAudio 组件，按钮音效不会播放");
            }
            return;
        }

        instance.PlayClickInternal(clip);
    }

    private void PlayClickInternal(AudioClip clip)
    {
        if (muted || source == null) return;

        AudioClip target = clip != null ? clip : clickClip;
        if (target == null)
        {
            if (!generateIfMissing)
            {
                if (!warnedNoClip)
                {
                    warnedNoClip = true;
                    Debug.LogWarning("[UiAudio] 没有指定 Click Clip，按钮不会出声");
                }
                return;
            }

            if (generated == null) generated = CreateClickClip();
            target = generated;
        }

        source.PlayOneShot(target, Mathf.Clamp01(volume));
    }

    /// <summary>手动播放任意音效（非按钮场景用）。</summary>
    public void Play(AudioClip clip, float volumeScale = 1f)
    {
        if (muted || source == null || clip == null) return;
        source.PlayOneShot(clip, Mathf.Clamp01(volume * volumeScale));
    }

    /// <summary>合成一个 ~50ms 的短促"嗒"声，纯代码，不依赖音频资源。</summary>
    private static AudioClip CreateClickClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.05f;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));

        float[] data = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)sampleCount;
            float envelope = (1f - progress) * (1f - progress);
            float frequency = Mathf.Lerp(1600f, 900f, progress);
            float phase = 2f * Mathf.PI * frequency * (i / (float)sampleRate);
            data[i] = Mathf.Sin(phase) * envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create("UiClickGenerated", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
