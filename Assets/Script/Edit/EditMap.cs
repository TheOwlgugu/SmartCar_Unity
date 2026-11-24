using UnityEngine;

public enum MapMode
{
    None = 0,
    Road = 1,
    Wall = 2
}

/// <summary>
/// 地图编辑的核心：持有模型、绘制、引导球、脏标记与落盘。
/// 铺格子和命中判定的通用逻辑在 MapGridView / MapRaycast 里，和 USE 场景共用。
/// </summary>
public class EditMap : MonoBehaviour
{
    [Header("格子渲染")]
    [SerializeField] private Transform mapRoot;
    [SerializeField] private GameObject pointPrefab;
    [SerializeField] private Material roadMaterial;
    [SerializeField] private Material wallMaterial;

    [Header("引导球")]
    [Tooltip("指示当前选中格子的预制体，为空则不显示引导")]
    [SerializeField] private GameObject cellGuidePrefab;
    [Tooltip("可选：引导球的父节点。留空则挂在场景根节点，绝不会被重建地图时误删")]
    [SerializeField] private Transform guideRoot;
    [Tooltip("引导球相对地面的高度，编辑界面里直接调")]
    public float height = 0.5f;

    [Header("保存")]
    [Tooltip("最后一次绘制后多久自动落盘（秒）")]
    [SerializeField] private float autoSaveDelay = 1f;

    private MapFileData data;
    private string mapName;
    private MapGridView grid;
    private GameObject guide;
    private MapMode mode = MapMode.None;
    private bool dirty;
    private float lastEditTime;
    private int lastPaintedX = int.MinValue;
    private int lastPaintedY = int.MinValue;

    public MapFileData Data { get { return data; } }
    public string MapName { get { return mapName; } }
    public int Width { get { return data != null ? data.Width : 0; } }
    public int Height { get { return data != null ? data.Height : 0; } }
    public MapMode Mode { get { return mode; } }

    private void Start()
    {
        EnsureRefs();
    }

    /// <summary>
    /// EditMap.Start 和 EditInitializer.Start 的先后顺序没有保证，
    /// 所以每个入口都先补齐引用，避免依赖脚本执行顺序。
    /// </summary>
    private void EnsureRefs()
    {
        if (mapRoot == null)
        {
            GameObject found = GameObject.Find("MAP");
            if (found != null) mapRoot = found.transform;
        }
    }

    // ---------------- 加载 / 重建 ----------------

    public void LoadFromMap(string name)
    {
        EnsureRefs();
        mapName = name;
        data = MapJson.Load(name);

        if (data == null)
        {
            Debug.LogWarning("[EditMap] 读取失败，用 10x10 全道路占位: " + name);
            data = MapFileData.Create(10, 10);
            dirty = true;
        }

        RebuildGrid();
        LogAscii();
    }

    public void ApplySize(int width, int height)
    {
        if (data == null) return;

        data.Resize(width, height);
        RebuildGrid();
        LogAscii();

        dirty = true;
        SaveNow(); // 改尺寸属于"结构性变更"，立即落盘更稳
    }

    private void RebuildGrid()
    {
        grid = new MapGridView(mapRoot, pointPrefab, roadMaterial, wallMaterial);

        if (!grid.IsReady)
        {
            Debug.LogError("[EditMap] mapRoot / pointPrefab 未绑定，无法生成格子");
            return;
        }

        grid.Build(data);
    }

    // ---------------- 模式 / 绘制 ----------------

    public void SetMode(MapMode next)
    {
        mode = next;
        lastPaintedX = int.MinValue;
        lastPaintedY = int.MinValue;
        if (mode == MapMode.None) HideGuide();
    }

    private void Update()
    {
        UpdateGuideAndPaint();
        UpdateAutoSave();
    }

    private void UpdateAutoSave()
    {
        if (!dirty) return;
        if (Time.unscaledTime - lastEditTime < autoSaveDelay) return;
        SaveNow();
    }

    private void UpdateGuideAndPaint()
    {
        if (mode == MapMode.None || data == null)
        {
            HideGuide();
            return;
        }

        if (MapRaycast.IsPointerOverUI())
        {
            HideGuide();
            return;
        }

        int cellX;
        int cellY;
        if (!MapRaycast.TryGetCell(Camera.main, data, out cellX, out cellY))
        {
            HideGuide();
            lastPaintedX = int.MinValue;
            return;
        }

        ShowGuide(cellX, cellY);

        if (!Input.GetMouseButton(0))
        {
            lastPaintedX = int.MinValue;
            lastPaintedY = int.MinValue;
            return;
        }

        if (cellX == lastPaintedX && cellY == lastPaintedY) return;
        Paint(cellX, cellY);
    }

    private void Paint(int x, int y)
    {
        if (data == null) return;

        bool wall = mode == MapMode.Wall;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            wall = !wall; // Shift 临时反色
        }
        bool road = !wall;

        lastPaintedX = x;
        lastPaintedY = y;

        if (data.IsRoad(x, y) == road) return;

        data.SetRoad(x, y, road);
        if (grid != null) grid.Refresh(x, y);   // 视觉只取决于 R，邻格无需重绘

        dirty = true;
        lastEditTime = Time.unscaledTime;
    }

    private void ShowGuide(int x, int y)
    {
        if (cellGuidePrefab == null || data == null) return;

        if (guide == null)
        {
            // 引导球绝不能挂在 MAP 下：重建地图会把子物体清空
            Transform parent = guideRoot;
            if (parent == null || parent == mapRoot || (mapRoot != null && parent.IsChildOf(mapRoot)))
            {
                parent = null;
            }

            guide = Instantiate(cellGuidePrefab, parent);
            guide.name = "CellGuide";
        }

        if (!guide.activeSelf) guide.SetActive(true);

        if (grid != null)
        {
            guide.transform.position = grid.CellToWorld(x, y, height);
        }
        else
        {
            float startX = -(data.Width - 1) * 0.5f;
            float startZ = -(data.Height - 1) * 0.5f;
            guide.transform.position = new Vector3(startX + x, height, startZ + y);
        }
    }

    private void HideGuide()
    {
        if (guide != null && guide.activeSelf) guide.SetActive(false);
    }

    // ---------------- 落盘 ----------------

    public void SaveNow()
    {
        if (data == null || string.IsNullOrEmpty(mapName)) return;

        if (MapJson.Save(mapName, data))
        {
            dirty = false;
            Debug.Log("[EditMap] 已保存: " + MapJson.FileFor(mapName));
        }
    }

    private void LogAscii()
    {
        if (data == null) return;
        Debug.Log("[EditMap] " + mapName + "\n" + data.ToAscii());
    }

    private void OnDisable()
    {
        if (dirty) SaveNow();
    }

    private void OnApplicationQuit()
    {
        if (dirty) SaveNow();
    }
}
