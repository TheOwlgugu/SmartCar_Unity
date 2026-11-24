using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图网格的铺/清/换色。普通 C# 类（不是 MonoBehaviour），
/// Edit 和 USE 各自持有一个实例，引用由各自的脚本从 Inspector 传进来。
/// 注意：它会清空 mapRoot 的所有子物体，所以引导球、标记物**不能**挂在 mapRoot 下。
/// </summary>
public class MapGridView
{
    private readonly Transform mapRoot;
    private readonly GameObject pointPrefab;
    private readonly Material roadMaterial;
    private readonly Material wallMaterial;

    private readonly List<GameObject> points = new List<GameObject>();
    private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
    private MapFileData data;

    public MapGridView(Transform mapRoot, GameObject pointPrefab, Material roadMaterial, Material wallMaterial)
    {
        this.mapRoot = mapRoot;
        this.pointPrefab = pointPrefab;
        this.roadMaterial = roadMaterial;
        this.wallMaterial = wallMaterial;
    }

    public bool IsReady { get { return mapRoot != null && pointPrefab != null; } }
    public int Count { get { return points.Count; } }
    public MapFileData Data { get { return data; } }

    public void Build(MapFileData map)
    {
        Clear();
        data = map;

        if (!IsReady || data == null) return;

        float startX = -(data.Width - 1) * 0.5f;
        float startZ = -(data.Height - 1) * 0.5f;

        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                GameObject go = Object.Instantiate(pointPrefab, mapRoot);
                go.transform.localPosition = new Vector3(startX + x, 0f, startZ + y);
                go.name = string.Format("Cell_{0}_{1}", x, y);

                points.Add(go);
                renderers.Add(go.GetComponentInChildren<MeshRenderer>());
            }
        }

        RefreshAll();
    }

    public void RefreshAll()
    {
        if (data == null) return;
        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                Refresh(x, y);
            }
        }
    }

    public void Refresh(int x, int y)
    {
        if (data == null) return;

        int index = data.IndexOf(x, y);
        if (index < 0 || index >= renderers.Count) return;

        MeshRenderer renderer = renderers[index];
        if (renderer == null) return;

        Material target = data.IsRoad(x, y) ? roadMaterial : wallMaterial;
        if (target != null) renderer.sharedMaterial = target;
    }

    public void Clear()
    {
        for (int i = 0; i < points.Count; i++)
        {
            GameObject go = points[i];
            if (go == null) continue;
            go.SetActive(false);
            DestroyObject(go);
        }
        points.Clear();
        renderers.Clear();

        // 兜底：清掉 mapRoot 下所有残留子物体
        if (mapRoot != null)
        {
            for (int i = mapRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = mapRoot.GetChild(i).gameObject;
                if (child == null) continue;
                child.SetActive(false);
                DestroyObject(child);
            }
        }

        data = null;
    }

    /// <summary>格子中心的世界坐标（marker 用）。</summary>
    public Vector3 CellToWorld(int x, int y, float height)
    {
        if (data == null) return Vector3.zero;

        float startX = -(data.Width - 1) * 0.5f;
        float startZ = -(data.Height - 1) * 0.5f;
        Vector3 local = new Vector3(startX + x, height, startZ + y);
        return mapRoot != null ? mapRoot.TransformPoint(local) : local;
    }

    private static void DestroyObject(GameObject go)
    {
        if (Application.isPlaying) Object.Destroy(go);
        else Object.DestroyImmediate(go);
    }
}
