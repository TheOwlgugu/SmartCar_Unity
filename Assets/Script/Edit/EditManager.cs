using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 编辑界面的"小状态机"：只负责模式切换、按钮/输入框和 Apply/Quit。
/// 具体的绘制、命中判定、落盘全部在 EditMap 里，这里不碰模型细节。
/// </summary>
public class EditManager : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private EditMap editMap;

    [Header("按钮")]
    [SerializeField] private Button quitButton;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button roadButton;
    [SerializeField] private Button wallButton;

    [Header("尺寸输入框")]
    [SerializeField] private TMP_InputField widthInput;
    [SerializeField] private TMP_InputField heightInput;

    [Header("模式按钮配色")]
    [Tooltip("留空会自动取按钮自身的 targetGraphic")]
    [SerializeField] private Graphic roadButtonGraphic;
    [SerializeField] private Graphic wallButtonGraphic;
    [SerializeField] private Color activeColor = new Color(0.25f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color idleColor = Color.white;

    private const string MenuSceneName = "Menu";

    public MapMode CurrentMode { get; private set; }

    private void Awake()
    {
        if (editMap == null) editMap = FindObjectOfType<EditMap>();
        if (roadButtonGraphic == null && roadButton != null) roadButtonGraphic = roadButton.targetGraphic;
        if (wallButtonGraphic == null && wallButton != null) wallButtonGraphic = wallButton.targetGraphic;

        if (quitButton != null) quitButton.onClick.AddListener(QuitToMenu);
        if (applyButton != null) applyButton.onClick.AddListener(ApplyChanges);
        if (roadButton != null) roadButton.onClick.AddListener(ToggleRoadMode);
        if (wallButton != null) wallButton.onClick.AddListener(ToggleWallMode);

        CurrentMode = MapMode.None;
        RefreshModeVisual();
    }

    // ---------------- 模式 ----------------

    public void ToggleRoadMode() { ToggleMode(MapMode.Road); }
    public void ToggleWallMode() { ToggleMode(MapMode.Wall); }

    /// <summary>再按一次同一个按钮退出；按另一个按钮则直接切过去（同一时刻只有一个模式）。</summary>
    public void ToggleMode(MapMode target)
    {
        SetMode(CurrentMode == target ? MapMode.None : target);
    }

    public void SetMode(MapMode target)
    {
        CurrentMode = target;
        if (editMap != null) editMap.SetMode(target);
        RefreshModeVisual();
    }

    private void RefreshModeVisual()
    {
        if (roadButtonGraphic != null) roadButtonGraphic.color = CurrentMode == MapMode.Road ? activeColor : idleColor;
        if (wallButtonGraphic != null) wallButtonGraphic.color = CurrentMode == MapMode.Wall ? activeColor : idleColor;
    }

    // ---------------- 尺寸 ----------------

    /// <summary>由 EditInitializer 在加载完地图后调用，把宽高回填到输入框。</summary>
    public void SetSizeText(int width, int height)
    {
        if (widthInput != null) widthInput.text = width.ToString(CultureInfo.InvariantCulture);
        if (heightInput != null) heightInput.text = height.ToString(CultureInfo.InvariantCulture);
    }

    private void ApplyChanges()
    {
        if (editMap == null)
        {
            Debug.LogWarning("[EditManager] editMap 未绑定");
            return;
        }

        int width;
        int height;

        if (widthInput == null || !int.TryParse(widthInput.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out width) || width <= 0)
        {
            Debug.LogWarning("[EditManager] Width 非法");
            return;
        }

        if (heightInput == null || !int.TryParse(heightInput.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out height) || height <= 0)
        {
            Debug.LogWarning("[EditManager] Height 非法");
            return;
        }

        editMap.ApplySize(width, height);
        SetSizeText(editMap.Width, editMap.Height); // 被夹到 200 上限时同步显示
        ValidateMap();
    }

    private void ValidateMap()
    {
        if (editMap == null || editMap.Data == null) return;

        MapFileData map = editMap.Data;

        if (map.RoadCount() == 0)
        {
            Debug.LogWarning("[EditManager] 地图里没有任何道路格");
        }

        int components = map.ComponentCount();
        if (components > 1)
        {
            Debug.LogWarning("[EditManager] 地图不连通，道路连通块数量: " + components + "，A* 可能大量失败");
        }
    }

    private void QuitToMenu()
    {
        if (editMap != null) editMap.SaveNow();
        SceneManager.LoadScene(MenuSceneName);
    }
}
