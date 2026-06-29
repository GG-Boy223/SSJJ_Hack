using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Render;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class MoveEntityESP : MonoBehaviour
    {
        private void OnGUI()
        {
            if (!Config.ShowMoveEntityESP || PlayerUpdate.LocalEntity == null || PlayerUpdate.MainCamera == null)
                return;

            DrawMoveEntities();
        }

        private void DrawMoveEntities()
        {
            var sceneObjectContext = Contexts.sharedInstance?.sceneObject;
            if (sceneObjectContext == null) return;

            Vector3 camPos = PlayerUpdate.MainCamera.transform.position;

            foreach (SceneObjectEntity sceneObjectEntity in sceneObjectContext.GetGroup(SceneObjectMatcher.MoveObject))
            {
                if (sceneObjectEntity == null || !sceneObjectEntity.hasMoveObject)
                    continue;

                var moveData = sceneObjectEntity.moveObject.Current;
                if (moveData == null) continue;

                string entityName = moveData.Name ?? "";
                if (string.IsNullOrEmpty(entityName)) continue;

                Vector3 worldPos = new Vector3(moveData.X, moveData.Y, moveData.Z);
                Vector3 unityPos = SSJJMath.VectorCoordConverter.SsjjToUnity(worldPos);
                Vector3 screenPos = PlayerUpdate.MainCamera.WorldToScreenPoint(unityPos);

                if (screenPos.z <= 0) continue;

                float dist = Vector3.Distance(camPos, unityPos) * 0.01f;
                string showText = $"{entityName} [{dist:F0}m]";

                Vector2 dotPos = new Vector2(screenPos.x, screenPos.y);
                ImmediateRenderer.DrawCircleFilled(dotPos, 3f, Color.cyan, 8);

                Vector2 textPos = new Vector2(screenPos.x, Screen.height - screenPos.y + 10f);
                ImmediateRenderer.DrawString(textPos, showText, Color.cyan, true, 11);
            }
        }
    }
}
