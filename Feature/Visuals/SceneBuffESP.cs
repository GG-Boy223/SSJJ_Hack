using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Render;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class SceneBuffESP : MonoBehaviour
    {
        private void OnGUI()
        {
            if (!Config.ShowSceneBuffESP || PlayerUpdate.LocalEntity == null || PlayerUpdate.MainCamera == null)
                return;

            DrawSceneBuffs();
        }

        private void DrawSceneBuffs()
        {
            var sceneObjectContext = Contexts.sharedInstance?.sceneObject;
            if (sceneObjectContext == null) return;

            Vector3 camPos = PlayerUpdate.MainCamera.transform.position;

            foreach (SceneObjectEntity sceneObjectEntity in sceneObjectContext.GetGroup(SceneObjectMatcher.SceneBuff))
            {
                if (sceneObjectEntity == null || !sceneObjectEntity.hasSceneBuff)
                    continue;

                var buffData = sceneObjectEntity.sceneBuff.Data;
                if (buffData == null) continue;

                string buffName = buffData.BufName ?? "";
                if (string.IsNullOrEmpty(buffName)) continue;

                Vector3 worldPos = new Vector3(buffData.X, buffData.Y, buffData.Z);
                Vector3 unityPos = SSJJMath.VectorCoordConverter.SsjjToUnity(worldPos);
                Vector3 screenPos = PlayerUpdate.MainCamera.WorldToScreenPoint(unityPos);

                if (screenPos.z <= 0) continue;

                float dist = Vector3.Distance(camPos, unityPos) * 0.01f;
                string showText = $"{buffName} [{dist:F0}m]";

                Vector2 dotPos = new Vector2(screenPos.x, screenPos.y);
                ImmediateRenderer.DrawCircleFilled(dotPos, 3f, Color.magenta, 8);

                Vector2 textPos = new Vector2(screenPos.x, Screen.height - screenPos.y + 10f);
                ImmediateRenderer.DrawString(textPos, showText, Color.magenta, true, 11);
            }
        }
    }
}