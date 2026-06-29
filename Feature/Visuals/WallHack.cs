using Assets.Sources.Utils.Weapon;
using share;
using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Feature.AutoTrigger;
using SkyDome.Feature.Legit;
using SkyDome.Render;
using SkyDome.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class WallHack : MonoBehaviour
    {
        private readonly struct TextRule
        {
            public readonly Func<PlayerInfo, bool> IsEnabled;  // 开关判定
            public readonly Func<PlayerInfo, string> GetText;  // 文本内容
            public readonly Func<PlayerInfo, Color?> GetColor; // 文本颜色

            // 构造函数
            public TextRule(Func<PlayerInfo, bool> enabled, Func<PlayerInfo, string> text, Func<PlayerInfo, Color?> color = null)
            {
                IsEnabled = enabled;
                GetText = text;
                GetColor = color;
            }
        }

        // 上方堆叠规则
        private static readonly TextRule[] TopRules = {
            new TextRule(p => Config.ShowName, p => p.PlayerName),
            new TextRule(p => Config.ShowWeapon, p => GetWeaponDisplayText(p), p => GetWeaponColor(p)),
            new TextRule(p => Config.ShowYaw, p => $"偏航角 {p.ViewYaw}°"),
            new TextRule(p => Config.ShowPitch, p => $"俯仰角 {p.ViewPitch}°"),
            //测试用
            //new TextRule(p => true, p => $"武器ID: {p.CurrentWeaponName}"),
            //new TextRule(p => true, p => $"武器槽位: {p.CurrentWeaponId}"),
            //new TextRule(p => true, p => $"武器类型: {p.WeaponDetailType}"),
            //new TextRule(p => true, p => $"角色模型: {p.Career}")
        };

        // 下方堆叠规则
        private static readonly TextRule[] BottomRules = {
            new TextRule(p => Config.ShowHp, p => {
                string fmt = Mathf.Approximately(p.Hp, Mathf.Round(p.Hp)) ? "F0" : "F2";
                return $"HP {p.Hp.ToString(fmt)}";
            }),
            new TextRule(p => Config.ShowDistance, p => $"{p.Distance:F0}m"),
            new TextRule(p => Config.ShowC4 && p.HasC4, p => "【携带C4】", p => (Color?)Color.red),
            new TextRule(enabled: p => PlayerUpdate.LocalEntity != null &&PlayerUpdate.LocalEntity.CurrentWeaponName == "wind_spirit" &&WindSpiritRecall.EnemiesOnPaths != null &&IsPlayerOnWindSpiritPath(p),text: p => "⚠风铃路径",color: p => (Color?)new Color(1f, 0.5f, 0f))

        };

        private void OnGUI()
        {
            if (!Config.WallHack || PlayerUpdate.EntityList == null) return;

            foreach (var player in PlayerUpdate.EntityList)
            {
                if (player.Team != PlayerUpdate.LocalEntity.Team && !player.IsDead)
                    DrawEnemy(player);
            }
        }

        private void DrawEnemy(PlayerInfo player)
        {
            // 计算包围盒
            if (!TryGetBoundingBox(player, out Rect rect, out Color color)) return;

            // 绘制图形
            DrawVisuals(player, rect, color);

            // 绘制堆叠文字
            DrawStackedText(player, rect, color);
        }

        // 包围盒计算
        private bool TryGetBoundingBox(PlayerInfo player, out Rect rect, out Color color)
        {
            rect = default;
            color = Color.green;

            // 添加空检查
            Transform feetTransform = player.GetPlayerTransform(player.PlayerName);
            Transform headTransform = player.GetValidHeadNub();

            if (feetTransform == null || headTransform == null)
                return false;

            Vector3 screenFeet = ViewportUtility.WorldPointToScreenPoint(feetTransform.position);
            Vector3 screenHead = ViewportUtility.WorldPointToScreenPoint(headTransform.position);

            if (!ViewportUtility.IsScreenPointVisible(screenFeet))
                return false;

            float height = Mathf.Abs(screenHead.y - screenFeet.y);
            float width = height / 2.3f;
            Vector2 center = (screenHead + screenFeet) * 0.5f;

            rect = new Rect(
                center.x - width / 2f - 1f,
                Screen.height - center.y - height / 2f,
                width,
                height
            );

            if (Aimbot._currentTarget?._entity == player._entity)
                color = Color.yellow;
            else if (IsVisible(player))
                color = Color.red;

            return true;
        }

        // 图形绘制 (方框/血条/骨骼/射线)
        private void DrawVisuals(PlayerInfo player, Rect rect, Color color)
        {
            if (Config.ShowRect)
            {
                if (Config.Rect_Style == 0)
                    ImmediateRenderer.DrawBoxOutline(rect, color, 2f);
                else
                    ImmediateRenderer.DrawCornerBox(rect, color, 2f, 10f, true);
            }

            if (Config.ShowHpBar)
                OverLay.DrawVerticalHealthBar(rect, player.HpPercent, 5.3f, 3f);

            if (Config.ShowSkeleton)
                OverLay.DrawSkeleton(player, color, 2f);

            if (Config.ShowAirLine)
            {
                ImmediateRenderer.DrawLine(
                    new Vector2(Screen.width / 2f, Screen.height),
                    new Vector2(rect.center.x, rect.yMax),
                    color, 2f
                );
            }
        }

        // 自动堆叠
        private void DrawStackedText(PlayerInfo player, Rect rect, Color defaultColor)
        {
            float centerX = rect.center.x;
            const float lineHeight = 12f;

            // 上方堆叠
            float topY = 0f;
            bool isFirstTop = true;

            foreach (var rule in TopRules)
            {
                if (!rule.IsEnabled(player)) continue;

                if (isFirstTop)
                {
                    topY = Screen.height - rect.yMax - 15f;
                    isFirstTop = false;
                }
                else
                {
                    topY -= lineHeight;
                }

                Color textColor = rule.GetColor != null ? rule.GetColor(player) ?? defaultColor : defaultColor;

                ImmediateRenderer.DrawString(
                    new Vector2(centerX, topY),
                    rule.GetText(player),
                    textColor,
                    true, 10
                );
            }

            // 下方堆叠
            float botY = 0f;
            bool isFirstBot = true;

            foreach (var rule in BottomRules)
            {
                if (!rule.IsEnabled(player)) continue;

                if (isFirstBot)
                {
                    botY = Screen.height - rect.y;
                    isFirstBot = false;
                }
                else
                {
                    botY += lineHeight;
                }

                Color textColor = rule.GetColor != null ? rule.GetColor(player) ?? defaultColor : defaultColor;

                ImmediateRenderer.DrawString(
                    new Vector2(centerX, botY),
                    rule.GetText(player),
                    textColor,
                    true, 10
                );
            }
        }

        // 可见性检测
        private bool IsVisible(PlayerInfo target)
        {
            var forward = SSJJMath.VectorCoordConverter.UnityToSsjj(Camera.main.transform.forward);
            var result = FireUtility.BulletTrace(
                Contexts.sharedInstance.battleRoom.pyEngine.PyEngine,
                PlayerUpdate.LocalEntity._entity,
                Contexts.sharedInstance.player,
                100000f,
                new Vector3D(forward.x, forward.y, forward.z),
                new float[3], new float[3], false
            );
            return result.EntityId == target.Id;
        }

        // 获取武器显示文本
        private static string GetWeaponDisplayText(PlayerInfo player)
        {
            int slotId = player.CurrentWeaponId;
            int weaponType = player.WeaponDetailType;
            string weaponName = player.Weapon;

            // 槽位1：显示武器类型
            if (slotId == 1)
            {
                return GetWeaponTypeName(weaponType, weaponName);
            }
            // 槽位2：副武器
            else if (slotId == 2)
            {
                return $"[副武器]{weaponName}";
            }
            // 槽位3：近战
            else if (slotId == 3)
            {
                return $"[近战]{weaponName}";
            }
            // 槽位4：投掷物
            else if (slotId == 4)
            {
                return GetThrowableDisplayText(weaponName);
            }
            // 槽位5：战术
            else if (slotId == 5)
            {
                return $"[战术]{weaponName}";
            }
            // 其他槽位
            else
            {
                return $"[{slotId}]{weaponName}";
            }
        }

        // 获取武器类型名称
        private static string GetWeaponTypeName(int weaponType, string weaponName)
        {
            switch (weaponType)
            {
                case 0:
                    return $"[手枪]{weaponName}";
                case 1:
                    return $"[步枪]{weaponName}";
                case 2:
                    return $"[近战]{weaponName}";
                case 3:
                    return $"[投掷物]{weaponName}";
                case 5:
                    return $"[狙击枪]{weaponName}";
                case 6:
                    return $"[霰弹]{weaponName}";
                case 10:
                    return $"[机枪]{weaponName}";
                case 12:
                    return $"[冲锋枪]{weaponName}";
                default:
                    return $"[{weaponType}]{weaponName}";
            }
        }

        // 投掷物名字定义
        private static readonly HashSet<string> SpecialThrowables = new HashSet<string>
        {"闪光弹","FLash-X","烟雾弹","雾藤","万象","镇宇","天枢","玉衡","月隐","胡峰","极光","暗蚀"};

        // 获取投掷物显示文本
        private static string GetThrowableDisplayText(string weaponName)
        {
            foreach (string special in SpecialThrowables)
            {
                if (weaponName.Contains(special))
                    return $"[投掷物]{weaponName}";
            }
            return $"[手雷]{weaponName}";
        }

        // 获取投掷物显示颜色
        private static Color? GetWeaponColor(PlayerInfo player)
        {
            if (player.CurrentWeaponId == 4)
            {
                foreach (string special in SpecialThrowables)
                {
                    if (player.Weapon.Contains(special))
                        return null;
                }
                return Color.red;
            }
            return null;
        }

        // 检查玩家是否在任何风铃路径上
        private static bool IsPlayerOnWindSpiritPath(PlayerInfo player)
        {
            if (WindSpiritRecall.EnemiesOnPaths == null || WindSpiritRecall.EnemiesOnPaths.Count == 0)
                return false;

            foreach (var pathEnemies in WindSpiritRecall.EnemiesOnPaths.Values)
            {
                foreach (var enemyOnPath in pathEnemies)
                {
                    if (enemyOnPath.Player._entity == player._entity)
                        return true;
                }
            }

            return false;
        }

    }
}
