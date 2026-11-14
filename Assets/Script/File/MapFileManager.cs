using System;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class MapFileManager : MonoBehaviour
{
    [Header("Sub Panel")]
    [SerializeField] private GameObject subPanel;

    [Header("Buttons")]
    [SerializeField] private Button addButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button commitButton;

    [Header("Input Fields")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField heightInput;
    [SerializeField] private TMP_InputField widthInput;

    [Header("Refreshing")]
    [SerializeField] private JsonRefreshing refresher;

    private const string MapsFolderName = "MAPS";

    private string MapsFolderPath
    {
        get { return Path.Combine(Application.dataPath, MapsFolderName); }
    }

    private void Awake()
    {
        if (addButton != null)
        {
            addButton.onClick.AddListener(OpenSubPanel);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(CloseSubPanel);
        }

        if (commitButton != null)
        {
            commitButton.onClick.AddListener(CommitAndClose);
        }

        if (subPanel != null)
        {
            subPanel.SetActive(false);
        }
    }

    public void OpenSubPanel()
    {
        if (subPanel != null)
        {
            // 每次打开前清空输入框
            if (nameInput != null) nameInput.text = string.Empty;
            if (heightInput != null) heightInput.text = string.Empty;
            if (widthInput != null) widthInput.text = string.Empty;
            subPanel.SetActive(true);
        }
    }

    public void CloseSubPanel()
    {
        if (subPanel != null)
        {
            subPanel.SetActive(false);
        }
    }

    public void CommitAndClose()
    {
        string rawName = nameInput != null ? nameInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(rawName))
        {
            Debug.LogWarning("[MapFileManager] Name is empty.");
            return;
        }

        int height;
        int width;

        if (heightInput == null ||
            !int.TryParse(heightInput.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out height))
        {
            Debug.LogWarning("[MapFileManager] Height is invalid.");
            return;
        }

        if (widthInput == null ||
            !int.TryParse(widthInput.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out width))
        {
            Debug.LogWarning("[MapFileManager] Width is invalid.");
            return;
        }

        MapData data = new MapData
        {
            Height = height,
            Width = width
        };

        string json = JsonUtility.ToJson(data, true);
        string safeName = SanitizeFileName(rawName);
        string filePath = Path.Combine(MapsFolderPath, safeName + ".json");

        try
        {
            Directory.CreateDirectory(MapsFolderPath);
            File.WriteAllText(filePath, json);
            Debug.Log("[MapFileManager] Saved: " + filePath);
        }
        catch (Exception e)
        {
            Debug.LogError("[MapFileManager] Save failed: " + e.Message);
            return;
        }

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

        if (refresher != null)
        {
            refresher.Refresh();
        }

        CloseSubPanel();
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] chars = name.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalidChars, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    [Serializable]
    private class MapData
    {
        public int Height;
        public int Width;
    }
}