using System;
using System.IO;
using TMPro;
using UnityEngine;

public class EditInitializer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text nameDisplayText;
    [SerializeField] private TMP_InputField widthInput;  // 新增：宽输入框
    [SerializeField] private TMP_InputField heightInput; // 新增：高输入框

    [Header("Map Settings")]
    [SerializeField] private Transform mapRoot;
    [SerializeField] private GameObject pointPrefab;

    private const string MapsFolderName = "MAPS";

    private void Start()
    {
        string mapName = SceneData.SelectedMapName;

        if (string.IsNullOrEmpty(mapName))
        {
            Debug.LogWarning("[EditInitializer] No map name received.");
            if (nameDisplayText != null) nameDisplayText.text = "No Map Selected";
            return;
        }

        if (nameDisplayText != null)
        {
            nameDisplayText.text = mapName;
        }

        LoadMapData(mapName);
    }

    public void LoadMapData(string mapName)
    {
        string filePath = Path.Combine(Application.dataPath, MapsFolderName, mapName + ".json");
        if (!File.Exists(filePath))
        {
            Debug.LogError("[EditInitializer] JSON file not found: " + filePath);
            return;
        }

        int width = 0;
        int height = 0;

        try
        {
            string json = File.ReadAllText(filePath);
            MapData data = JsonUtility.FromJson<MapData>(json);
            if (data != null)
            {
                width = data.Width;
                height = data.Height;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[EditInitializer] Failed to parse JSON: " + e.Message);
            return;
        }

        // 把读到的值填入输入框（作为显示，也允许用户修改）
        if (widthInput != null) widthInput.text = width.ToString();
        if (heightInput != null) heightInput.text = height.ToString();

        if (width > 0 && height > 0)
        {
            RebuildMap(width, height);
        }
    }

    // 暴露给 EditManager 调用的重新生成接口
    public void RebuildMap(int width, int height)
    {
        if (mapRoot == null || pointPrefab == null) return;
        ClearMap();
        GenerateGrid(width, height);
    }

    private void ClearMap()
    {
        for (int i = mapRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = mapRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void GenerateGrid(int width, int height)
    {
        float startX = -(width - 1) * 0.5f;
        float startZ = -(height - 1) * 0.5f;

        for (int z = 0; z < height; z++) // Z轴：南北 (Height)
        {
            for (int x = 0; x < width; x++) // X轴：东西 (Width)
            {
                GameObject point = Instantiate(pointPrefab, mapRoot);
                point.transform.localPosition = new Vector3(startX + x, 0f, startZ + z);
                point.name = $"Point_X{x}_Z{z}";
            }
        }
    }

    [Serializable]
    private class MapData
    {
        public int Height;
        public int Width;
    }
}