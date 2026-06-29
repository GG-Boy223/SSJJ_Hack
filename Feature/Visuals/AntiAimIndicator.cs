using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Render;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class AntiAimIndicator : MonoBehaviour
    {
        private void OnGUI()
        {
            if (!Config.AntiAim || PlayerUpdate.LocalEntity == null || PlayerUpdate.LocalEntity.IsDead)
                return;

            // 仅在特定偏航角时显示方向指示
            if (Config.AntiAim_Yaw == 90f ||
                Config.AntiAim_Yaw == -90f ||
                Config.AntiAim_Yaw == -180f)
            {
                DrawDirectionIndicator(Config.AntiAim_Yaw);
            }
        }

        private void DrawDirectionIndicator(float angle)
        {
            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;
            float size = 15f;
            float offset = 30f;

            bool isLeft = angle == -90f;

            Color activeColor = Color.white;
            Color inactiveColor = Color.white * 0.2f;

            // 左侧三角形顶点
            Vector2 leftTip = new Vector2(centerX - offset - size, centerY);
            Vector2 leftTop = new Vector2(centerX - offset, centerY - size / 2);
            Vector2 leftBottom = new Vector2(centerX - offset, centerY + size / 2);

            // 右侧三角形顶点
            Vector2 rightTip = new Vector2(centerX + offset + size, centerY);
            Vector2 rightTop = new Vector2(centerX + offset, centerY - size / 2);
            Vector2 rightBottom = new Vector2(centerX + offset, centerY + size / 2);

            if (angle == -180f)
            {
                // 两侧都暗淡显示
                ImmediateRenderer.DrawFilledTriangle(rightTip, rightBottom, rightTop, inactiveColor);
                ImmediateRenderer.DrawFilledTriangle(leftTip, leftTop, leftBottom, inactiveColor);
            }
            else
            {
                // 根据方向高亮一侧
                ImmediateRenderer.DrawFilledTriangle(
                    rightTip, rightBottom, rightTop,
                    isLeft ? inactiveColor : activeColor
                );
                ImmediateRenderer.DrawFilledTriangle(
                    leftTip, leftTop, leftBottom,
                    isLeft ? activeColor : inactiveColor
                );
            }
        }
    }
}
