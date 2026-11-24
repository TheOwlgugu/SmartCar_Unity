using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// USE 界面的按钮与模式状态机，和 EditManager 同一个套路：
/// 只管"现在是什么模式"，具体行为都在 UseMap 里。
/// </summary>
public class UseManager : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private UseMap useMap;

    [Header("按钮")]
    [SerializeField] private Button quitButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button goalButton;

    [Header("模式按钮配色")]
    [Tooltip("留空会自动取按钮自身的 targetGraphic")]
    [SerializeField] private Graphic startButtonGraphic;
    [SerializeField] private Graphic goalButtonGraphic;
    [SerializeField] private Color activeColor = new Color(0.25f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color idleColor = Color.white;

    private const string MenuSceneName = "Menu";

    public UseMode CurrentMode { get; private set; }

    private void Awake()
    {
        if (useMap == null) useMap = FindObjectOfType<UseMap>();
        if (startButtonGraphic == null && startButton != null) startButtonGraphic = startButton.targetGraphic;
        if (goalButtonGraphic == null && goalButton != null) goalButtonGraphic = goalButton.targetGraphic;

        if (quitButton != null) quitButton.onClick.AddListener(QuitToMenu);
        if (saveButton != null) saveButton.onClick.AddListener(SaveNow);
        if (startButton != null) startButton.onClick.AddListener(ToggleStartMode);
        if (goalButton != null) goalButton.onClick.AddListener(ToggleGoalMode);

        CurrentMode = UseMode.None;
        RefreshModeVisual();
    }

    public void ToggleStartMode() { ToggleMode(UseMode.SetStart); }
    public void ToggleGoalMode() { ToggleMode(UseMode.SetGoal); }

    /// <summary>再按一次同一个按钮退出；按另一个按钮则直接切过去。</summary>
    public void ToggleMode(UseMode target)
    {
        SetMode(CurrentMode == target ? UseMode.None : target);
    }

    public void SetMode(UseMode target)
    {
        CurrentMode = target;
        if (useMap != null) useMap.SetMode(target);
        RefreshModeVisual();
    }

    private void RefreshModeVisual()
    {
        if (startButtonGraphic != null) startButtonGraphic.color = CurrentMode == UseMode.SetStart ? activeColor : idleColor;
        if (goalButtonGraphic != null) goalButtonGraphic.color = CurrentMode == UseMode.SetGoal ? activeColor : idleColor;
    }

    public void SaveNow()
    {
        if (useMap != null) useMap.SaveNow();
    }

    public void ClearObstacles()
    {
        if (useMap != null) useMap.ClearObstacles();
    }

    private void QuitToMenu()
    {
        if (useMap != null) useMap.SaveNow();
        SceneManager.LoadScene(MenuSceneName);
    }
}
