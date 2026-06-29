using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Render;
using SkyDome.Utilities;
using UnityEngine;
using Assets.Sources.Utils.Ui;

namespace SkyDome.Feature.Visuals
{
    public class ItemESP : MonoBehaviour
    {
        private void OnGUI()
        {
            if (!Config.ShowItemESP || PlayerUpdate.LocalEntity == null || PlayerUpdate.MainCamera == null)
                return;

            DrawDroppedItems();
        }

        private void DrawDroppedItems()
        {
            var sceneObjectContext = Contexts.sharedInstance?.sceneObject;
            if (sceneObjectContext == null) return;

            // 获取摄像机位置用于计算距离
            Vector3 camPos = PlayerUpdate.MainCamera.transform.position;

            // 遍历所有掉落物
            foreach (SceneObjectEntity sceneObjectEntity in sceneObjectContext.GetGroup(SceneObjectMatcher.SceneWeapon))
            {
                if (sceneObjectEntity == null || !sceneObjectEntity.hasSceneWeapon)
                    continue;

                var weaponData = sceneObjectEntity.sceneWeapon.Current;
                if (weaponData == null) continue;

                Vector3 worldPos = new Vector3(weaponData.X, weaponData.Y, weaponData.Z);
                Vector3 unityPos = SSJJMath.VectorCoordConverter.SsjjToUnity(worldPos);
                Vector3 screenPos = PlayerUpdate.MainCamera.WorldToScreenPoint(unityPos);

                if (screenPos.z <= 0) continue;

                string rawName = weaponData.WeaponName ?? "";
                string displayName = rawName;

                // 调用翻译函数获取中文名称
                if (!string.IsNullOrEmpty(rawName))
                {
                    try
                    {
                        displayName = LanguageUtils.GetWeaponCnName(rawName);
                    }
                    catch
                    {

                    }
                }

                float dist = Vector3.Distance(camPos, unityPos) * 0.01f;
                string showText = Config.ShowItemESP ? $"{displayName} [{dist:F0}m]" : displayName;

                Vector2 dotPos = new Vector2(screenPos.x, screenPos.y);
                ImmediateRenderer.DrawCircleFilled(dotPos, 3f, Color.white, 8);

                Vector2 textPos = new Vector2(screenPos.x, Screen.height - screenPos.y + 10f);
                ImmediateRenderer.DrawString(textPos, showText, Color.white, true, 11);
            }
        }
    }
}
