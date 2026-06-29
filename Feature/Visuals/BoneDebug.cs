using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Render;
using SkyDome.Utilities;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    //public class BoneDebug : MonoBehaviour
    //{
    //    private void OnGUI()
    //    {
    //        if (!Config.WallHack || PlayerUpdate.EntityList == null || PlayerUpdate.MainCamera == null)
    //            return;

    //        foreach (var player in PlayerUpdate.EntityList)
    //        {
    //            if (player.Team != PlayerUpdate.LocalEntity?.Team && !player.IsDead)
    //            {
    //                DrawAllBones(player);
    //            }
    //        }
    //    }

    //    private void DrawAllBones(PlayerInfo player)
    //    {
    //        var allTransforms = player.GetPlayerAllTransform();
    //        if (allTransforms == null) return;

    //        foreach (var bone in allTransforms)
    //        {
    //            if (bone.Value == null) continue;

    //            Vector3 screenPos = PlayerUpdate.MainCamera.WorldToScreenPoint(bone.Value.position);

    //            if (screenPos.z <= 0) continue;

    //            // 圆点位置 (GL坐标，Y不翻转)
    //            Vector2 dotPos = new Vector2(screenPos.x, screenPos.y);
    //            ImmediateRenderer.DrawCircleFilled(dotPos, 1.5f, Color.yellow, 6);

    //            // 文字位置 (GUI坐标，Y翻转)
    //            Vector2 textPos = new Vector2(screenPos.x + 3f, Screen.height - screenPos.y);
    //            ImmediateRenderer.DrawString(textPos, bone.Key.ToString(), Color.white, false, 8);
    //        }
    //    }
    //}
}
