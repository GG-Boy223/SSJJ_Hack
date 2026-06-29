using SkyDome.Entity;
using UnityEngine;

namespace SkyDome.Utilities
{
    public static class ViewportUtility
    {
        public static bool IsScreenPointVisible(Vector3 screenPoint)
        {
            return screenPoint.z > 0.01f && screenPoint.x > -5f && screenPoint.y > -5f && screenPoint.x < Screen.width && screenPoint.y < Screen.height;
        }
        public static Vector3 WorldPointToScreenPoint(Vector3 worldPoint)
        {
            Vector3 screenPosition = PlayerUpdate.MainCamera.WorldToScreenPoint(worldPoint);
            float screenScaleFactor = (float)Screen.height / PlayerUpdate.MainCamera.scaledPixelHeight;
            screenPosition.y = Screen.height - screenPosition.y * screenScaleFactor;
            screenPosition.x *= screenScaleFactor;
            return screenPosition;
        }
    }
}
