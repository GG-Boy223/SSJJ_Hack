using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Render;
using SkyDome.Utilities;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class Radar : MonoBehaviour
    {
        private Vector2 ScreenCenter => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        private void OnGUI()
        {
            if (!Config.ShowRadar || PlayerUpdate.LocalEntity == null || PlayerUpdate.MainCamera == null)
                return;

            DrawRadarBackground();
            DrawEnemyMarkers();
        }

        private void DrawRadarBackground()
        {
            ImmediateRenderer.DrawCircleOutline(
                ScreenCenter,
                167.25f,
                64,
                Color.gray
            );
        }

        private void DrawEnemyMarkers()
        {
            if (PlayerUpdate.EntityList == null) return;

            Vector3 cameraPosition = PlayerUpdate.MainCamera.transform.position;
            float cameraYaw = PlayerUpdate.LocalEntity.ViewPos.y;
            Quaternion radarRotation = Quaternion.AngleAxis(cameraYaw, Vector3.back);
            int localTeam = PlayerUpdate.LocalEntity.Team;

            foreach (var enemy in PlayerUpdate.EntityList)
            {
                if (ShouldSkipEnemy(enemy, localTeam))
                    continue;

                Vector2 radarPosition = CalculateRadarPosition(
                    enemy,
                    cameraPosition,
                    radarRotation
                );

                float enemyYaw = enemy.ViewPos.y;
                Vector3 arrowDirection = CalculateArrowDirection(enemyYaw, radarRotation);

                DrawRadarMarker(radarPosition, arrowDirection, Color.cyan);
            }
        }

        private bool ShouldSkipEnemy(PlayerInfo enemy, int localTeam)
        {
            return enemy == null ||
                   !enemy._entity.hasBasicInfo ||
                   enemy.IsDead ||
                   enemy.Team == localTeam;
        }

        private Vector2 CalculateRadarPosition(
            PlayerInfo enemy,
            Vector3 cameraPosition,
            Quaternion radarRotation)
        {
            Vector3 enemyPosition = enemy.GetPlayerTransform(enemy.PlayerName).position;
            Vector3 relativePosition = enemyPosition - cameraPosition;

            Vector2 flatPosition = new Vector2(relativePosition.x, relativePosition.z);
            Vector2 rotatedPosition = radarRotation * flatPosition;

            Vector2 scaledPosition = rotatedPosition * Screen.height * 2.4E-07f * 167.25f;
            Vector2 clampedPosition = Vector2.ClampMagnitude(scaledPosition, 167.25f - 8f);

            return clampedPosition + ScreenCenter;
        }

        private Vector3 CalculateArrowDirection(float enemyYaw, Quaternion radarRotation)
        {
            Quaternion enemyRotation = Quaternion.AngleAxis(enemyYaw, Vector3.forward);
            return radarRotation * enemyRotation * Vector3.up;
        }

        private void DrawRadarMarker(Vector2 position, Vector3 direction, Color color)
        {
            Vector2 adjustedPosition = position - new Vector2(direction.x, direction.y) * 3.5f;

            ImmediateRenderer.DrawCircleFilled(adjustedPosition, 7f, color, 16);

            Vector2 arrowTip = adjustedPosition + new Vector2(direction.x, direction.y) * 12f;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            ImmediateRenderer.DrawLine(arrowTip, adjustedPosition, color, 2f);

            for (int i = 1; i <= 4; i++)
            {
                Vector2 leftWing = adjustedPosition + perpendicular * i;
                Vector2 rightWing = adjustedPosition - perpendicular * i;

                ImmediateRenderer.DrawLine(arrowTip, leftWing, color, 1.5f);
                ImmediateRenderer.DrawLine(arrowTip, rightWing, color, 1.5f);
            }
        }
    }
}
