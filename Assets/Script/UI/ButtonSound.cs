using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 挂在需要发声的 Button 上即可，点击时播放 UiAudio 里的音效。
/// 默认"按下就响"（比 onClick 松手才响跟手）；取消 Play On Press 则改回松手触发。
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonSound : MonoBehaviour, IPointerDownHandler
{
    [Header("触发时机")]
    [Tooltip("勾选 = 鼠标按下就响（跟手）；取消 = 松手完成点击才响")]
    [SerializeField] private bool playOnPress = true;

    [Header("音效")]
    [Tooltip("留空则使用 UiAudio 的 Click Clip")]
    [SerializeField] private AudioClip clip;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (!playOnPress && button != null)
        {
            button.onClick.AddListener(Play);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!playOnPress) return;
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
        if (button != null && !button.IsInteractable()) return;

        Play();
    }

    private void Play()
    {
        UiAudio.PlayClick(clip);
    }
}