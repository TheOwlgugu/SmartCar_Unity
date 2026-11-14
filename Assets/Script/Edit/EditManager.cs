using System;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EditManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EditInitializer editInitializer; // 引用初始化脚本
    [SerializeField] private Button quitButton;
    [SerializeField] private Button applyButton;

    [Header("UI Inputs")]
    [SerializeField] private TMP_InputField widthInput;
    [SerializeField] private TMP_InputField heightInput;

    private const string MapsFolderName = "MAPS";
    private const string MenuSceneName = "Menu"; // 你的主菜单场景名字

    private void Awake()
    {
        if (quitButton != null) quitButton.onClick.AddListener(QuitToMenu);
        if (applyButton != null) applyButton.onClick.AddListener(ApplyChanges);
    }

    private void QuitToMenu()
    {
        // 返回菜单场景，Menu 场景里的 JsonRefreshing.Start() 会自动刷新列表
        SceneManager.LoadScene(MenuSceneName);
    }

    private void ApplyChanges()
    {
        string mapName = SceneData.SelectedMapName;
        if (string.IsNullOrEmpty(mapName)) return;

        int width;
        int height;

        // 1. 校验输入值
        if (widthInput == null || !int.TryParse(widthInput.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out width) || width <= 0)
        {
            Debug.LogWarning("[EditManager] Invalid Width.");
            return;
        }

        if (heightInput == null || !int.TryParse(heightInput.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out height) || height <= 0)
        {
            Debug.LogWarning("[EditManager] Invalid Height.");
            return;
        }

        // 2. 覆盖写入 JSON 文件
        MapData data = new MapData { Width = width, Height = height };
        string json = JsonUtility.ToJson(data, true);
        string filePath = Path.Combine(Application.dataPath, MapsFolderName, mapName + ".json");

        try
        {
            File.WriteAllText(filePath, json);
            Debug.Log("[EditManager] Saved to JSON: " + filePath);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
        catch (Exception e)
        {
            Debug.LogError("[EditManager] Failed to save JSON: " + e.Message);
            return;
        }

        // 3. 通知 EditInitializer 重新生成地图
        if (editInitializer != null)
        {
            editInitializer.RebuildMap(width, height);
        }
        else
        {
            Debug.LogWarning("[EditManager] EditInitializer not assigned!");
        }
    }

    [Serializable]
    private class MapData
    {
        public int Height;
        public int Width;
    }
}