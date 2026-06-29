using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Feature.Legit;
using SkyDome.Render;
using SkyDome.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class FeatureIndicator : MonoBehaviour
    {
        // 指示器规则
        private readonly struct IndicatorRule
        {
            public readonly Func<bool> IsEnabled;
            public readonly Func<string> GetText;
            public readonly Func<Color> GetColor;

            public IndicatorRule(Func<bool> enabled, Func<string> text, Func<Color> color = null)
            {
                IsEnabled = enabled;
                GetText = text;
                GetColor = color ?? (() => Color.white);
            }
        }

        // 定义所有指示器
        private static readonly IndicatorRule[] Rules = {
            // 扳机指示器
            new IndicatorRule(
                enabled: () => Config.TriggerbotDelayedActivation && Triggerbot.IsActive,
                text: () => $"扳机 - {Triggerbot.RemainingTime:F1}s",
                color: () => Color.yellow
            ),

            // 假延迟指示器
            new IndicatorRule(
                enabled: () => Config.FakeLag,
                text: () => $"假延迟 - {Config.FakeLagChoke}",
                color: () => Color.cyan
            )
        };

        // 第一人称位置（屏幕左侧中间）
        private static Vector2 GetFirstPersonCenterPosition()
        {
            return new Vector2(100f, Screen.height / 2f);
        }

        // 第三人称偏移（相对于 Bip01_Spine 骨骼，在左侧）
        private static readonly Vector3 ThirdPersonOffset = new Vector3(-120f, 0f, 0f); // 负值 = 左侧

        private void OnGUI()
        {
            if (PlayerUpdate.LocalEntity == null || PlayerUpdate.LocalEntity.IsDead)
                return;

            // 收集当前激活的指示器
            List<(string text, Color color)> activeIndicators = new List<(string, Color)>();

            foreach (var rule in Rules)
            {
                if (rule.IsEnabled())
                {
                    activeIndicators.Add((rule.GetText(), rule.GetColor()));
                }
            }

            if (activeIndicators.Count == 0) return;

            // 根据视角模式选择绘制方式
            if (Config.ThirdPerson && SkyDome.Features.Menu.forceThirdPerson)
            {
                DrawThirdPersonIndicators(activeIndicators);
            }
            else
            {
                DrawFirstPersonIndicators(activeIndicators);
            }
        }

        // 第一人称绘制（以屏幕中心点向上下扩展）
        private void DrawFirstPersonIndicators(List<(string text, Color color)> indicators)
        {
            Vector2 centerPos = GetFirstPersonCenterPosition();
            float lineHeight = 18f;

            // 计算起始Y坐标（以中心点为基准）
            float totalHeight = indicators.Count * lineHeight;
            float startY = centerPos.y - totalHeight / 2f;

            for (int i = 0; i < indicators.Count; i++)
            {
                var (text, color) = indicators[i];
                Vector2 position = new Vector2(centerPos.x, startY + i * lineHeight);

                ImmediateRenderer.DrawString(position, text, color, false, 14);
            }
        }

        // 第三人称绘制（跟随 Bip01_Spine，在左侧，以骨骼点为中心向上下扩展）
        private void DrawThirdPersonIndicators(List<(string text, Color color)> indicators)
        {
            if (PlayerUpdate.MainCamera == null) return;

            Transform spineTransform = PlayerUpdate.LocalEntity.GetPlayerTransform("Bip01_Spine");
            if (spineTransform == null) return;

            // 计算世界坐标（骨骼点 + 左侧偏移）
            Vector3 worldPos = spineTransform.position;
            Vector3 screenPos = ViewportUtility.WorldPointToScreenPoint(worldPos);

            if (!ViewportUtility.IsScreenPointVisible(screenPos)) return;

            // 应用左侧偏移
            Vector2 centerPos = new Vector2(screenPos.x + ThirdPersonOffset.x, screenPos.y);

            float lineHeight = 18f;

            // 计算起始Y坐标（以骨骼点为中心）
            float totalHeight = indicators.Count * lineHeight;
            float startY = centerPos.y - totalHeight / 2f;

            for (int i = 0; i < indicators.Count; i++)
            {
                var (text, color) = indicators[i];
                Vector2 position = new Vector2(centerPos.x, startY + i * lineHeight);

                ImmediateRenderer.DrawString(position, text, color, false, 14);
            }
        }
    }
}
