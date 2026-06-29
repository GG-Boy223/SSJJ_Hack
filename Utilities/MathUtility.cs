using UnityEngine;

namespace SkyDome.Utilities
{
    public class MathUtility
    {
        //计算距离
        public static float CalculateDistance(float x1, float y1, float x2, float y2)
        {
            float deltaX = x1 - x2;
            float deltaY = y1 - y2;
            return Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        //计算水平速度
        public static float CalculateHorizontalSpeed(Vector3 velocity)
        {
            float x = velocity.x;
            float y = velocity.y;
            return Mathf.Sqrt(x * x + y * y);
        }
    }
}
