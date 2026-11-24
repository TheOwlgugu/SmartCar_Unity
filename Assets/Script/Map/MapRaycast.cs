using UnityEngine;

/// <summary>
/// 屏幕坐标 → 地图格索引。用射线打 y=0 平面再换算，不依赖任何 Collider。
/// 相机在地面以下、射线与平面平行、或落点在地图外时返回 false。
/// </summary>
public static class MapRaycast
{
    public static bool TryGetCell(Camera camera, MapFileData data, out int x, out int y)
    {
        return TryGetCell(camera, data, Input.mousePosition, out x, out y);
    }

    public static bool TryGetCell(Camera camera, MapFileData data, Vector3 screenPosition, out int x, out int y)
    {
        x = 0;
        y = 0;

        if (camera == null || data == null) return false;
        if (camera.transform.position.y <= 0f) return false;   // 相机钻到地面以下时不判定

        Ray ray = camera.ScreenPointToRay(screenPosition);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        float distance;
        if (!ground.Raycast(ray, out distance) || distance <= 0f) return false;

        Vector3 hit = ray.GetPoint(distance);
        float startX = -(data.Width - 1) * 0.5f;
        float startZ = -(data.Height - 1) * 0.5f;

        x = Mathf.RoundToInt(hit.x - startX);
        y = Mathf.RoundToInt(hit.z - startZ);
        return data.InBounds(x, y);
    }

    /// <summary>鼠标是否压在 UGUI 上（压在按钮上时不应该刷地图）。</summary>
    public static bool IsPointerOverUI()
    {
        return UnityEngine.EventSystems.EventSystem.current != null
               && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}
