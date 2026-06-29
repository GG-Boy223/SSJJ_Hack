using SkyDome.Entity;
using UnityEngine;
using SSJJPhysics;

namespace SkyDome.Feature.Visuals
{
    public class SpeedDashboard : MonoBehaviour
    {
        private GUIStyle _digitalStyle;
        private GUIStyle _subStyle;
        private Texture2D _pixel;

        // 用于记录最高速度残留
        private float _peakSpeed = 0f;
        private float _peakFadeTime = 0f;

        private void Start()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();

            _digitalStyle = new GUIStyle
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };

            _subStyle = new GUIStyle
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnGUI()
        {
            if (!SkyDome.Cfg.Config.ShowSpeedDashboard) return;
            if (PlayerUpdate.LocalEntity == null || PlayerUpdate.LocalEntity.IsDead) return;

            var localPlayer = PlayerUpdate.LocalEntity;
            float currentSpeed = SkyDome.Utilities.MathUtility.CalculateHorizontalSpeed(localPlayer.Move.Velocity);

            int logicMax = 0;
            if (localPlayer.Move.PyPlayerMove is BasePyPlayerAdapter adapter)
                logicMax = adapter.GetMaxSpeed();
            if (logicMax <= 0) logicMax = 1;

            float physicalMax = logicMax * 1.25f;

            // 更新最高速度记录
            if (currentSpeed > _peakSpeed)
            {
                _peakSpeed = currentSpeed;
                _peakFadeTime = Time.time + 3.0f; // 保持3秒
            }
            else if (Time.time > _peakFadeTime)
            {
                _peakSpeed = Mathf.Lerp(_peakSpeed, currentSpeed, Time.deltaTime * 2f);
            }

            // --- 绘制逻辑 ---
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height - 100f);
            float radius = 60f;

            // 1. 绘制背景圆弧 (分段)
            DrawArc(screenCenter, radius, 0, 1, new Color(1, 1, 1, 0.1f), 20);

            // 2. 绘制最高速记录点 (Peak)
            float peakRatio = _peakSpeed / physicalMax;
            DrawArcPoint(screenCenter, radius + 2, peakRatio, new Color(1, 0.8f, 0, 0.6f));

            // 3. 绘制当前速度圆弧
            float currentRatio = currentSpeed / physicalMax;
            Color themeColor = GetThemeColor(currentSpeed, logicMax, physicalMax);
            DrawArc(screenCenter, radius, 0, currentRatio, themeColor, 20);

            // 4. 中央数字
            _digitalStyle.fontSize = 24; // 删除了脉冲效果
            _digitalStyle.normal.textColor = themeColor;

            // 使用 Mathf.FloorToInt 确保向下取整，解决 769 vs 768 的误差问题
            GUI.Label(new Rect(screenCenter.x - 50, screenCenter.y - 20, 100, 40), $"{Mathf.FloorToInt(currentSpeed)}", _digitalStyle);

            // 5. 底部信息显示
            DrawOutlinedLabel(
                new Rect(screenCenter.x - 50, screenCenter.y + 15, 100, 20),
                $"MAX {Mathf.FloorToInt(physicalMax)}",
                _subStyle,
                themeColor,
                new Color(0f, 0f, 0f, 0.9f)); // 使用加速后的上限

            // 6. 左右两端的装饰线
            DrawRect(new Rect(screenCenter.x - radius - 20, screenCenter.y, 15, 1), new Color(1, 1, 1, 0.3f));
            DrawRect(new Rect(screenCenter.x + radius + 5, screenCenter.y, 15, 1), new Color(1, 1, 1, 0.3f));
        }

        private Color GetThemeColor(float current, int logicMax, float physicalMax)
        {
            // 当速度达到或接近最大物理极限时变成粉色
            if (current >= physicalMax - 1f) return new Color(1f, 0.4f, 0.7f, 1f); // 粉色

            // 速度超过逻辑上限 (100%) 立即变色
            if (current > logicMax) return new Color(0, 1, 1, 1); // 青色

            return Color.white;
        }

        private void DrawArc(Vector2 center, float radius, float startFill, float endFill, Color color, int segments)
        {
            GUI.color = color;
            float step = 1.0f / segments;
            for (float i = startFill; i < endFill; i += step)
            {
                float angle = Mathf.Lerp(180, 0, i);
                float rad = angle * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(rad) * radius, -Mathf.Sin(rad) * radius);

                Matrix4x4 matrixBackup = GUI.matrix;
                GUIUtility.RotateAroundPivot(-angle + 90, center + pos);
                GUI.DrawTexture(new Rect(center.x + pos.x - 2, center.y + pos.y, 4, 2), _pixel);
                GUI.matrix = matrixBackup;
            }
            GUI.color = Color.white;
        }

        private void DrawArcPoint(Vector2 center, float radius, float ratio, Color color)
        {
            GUI.color = color;
            float angle = Mathf.Lerp(180, 0, Mathf.Clamp01(ratio));
            float rad = angle * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(rad) * radius, -Mathf.Sin(rad) * radius);
            GUI.DrawTexture(new Rect(center.x + pos.x - 3, center.y + pos.y - 3, 6, 6), _pixel);
            GUI.color = Color.white;
        }

        private void DrawRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = Color.white;
        }

        private void DrawOutlinedLabel(Rect rect, string text, GUIStyle style, Color textColor, Color outlineColor)
        {
            Color previousTextColor = style.normal.textColor;

            style.normal.textColor = outlineColor;
            GUI.Label(new Rect(rect.x - 1f, rect.y, rect.width, rect.height), text, style);
            GUI.Label(new Rect(rect.x + 1f, rect.y, rect.width, rect.height), text, style);
            GUI.Label(new Rect(rect.x, rect.y - 1f, rect.width, rect.height), text, style);
            GUI.Label(new Rect(rect.x, rect.y + 1f, rect.width, rect.height), text, style);

            style.normal.textColor = textColor;
            GUI.Label(rect, text, style);

            style.normal.textColor = previousTextColor;
        }
    }
}
