using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Render;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class Crosshair : MonoBehaviour
    {
        private void OnGUI()
        {
            if (!Config.ShowCrosshair) return;

            // 检查玩家是否存在且未死亡
            if (PlayerUpdate.LocalEntity == null || PlayerUpdate.LocalEntity.IsDead)
                return;

            var weaponData = Contexts.sharedInstance.weapon;
            if (weaponData == null || weaponData.currentWeaponEntity == null)
                return;

            // 检查是否为狙击枪 (WeaponType == 5)
            bool isSniper = weaponData.currentWeaponEntity.basicInfo.Info.WeaponType == 5;

            // 检查是否开镜
            bool isZoomed = PlayerUpdate.LocalEntity.Fov.IsZoom();

            // 只有狙击枪且未开镜时才显示准星
            if (isSniper && !isZoomed)
            {
                DrawCrosshair();
            }
        }

        private void DrawCrosshair()
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

            ImmediateRenderer.DrawCrosshair(
                screenCenter,
                new Color(1f, 0.75f, 0.8f),
                size: 15f,
                thickness: 2f,
                gap: 5f,
                dot: false,
                outline: false
        );
        }
    }
}
