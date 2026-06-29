using SkyDome.Entity;
using SkyDome.Utilities;
using SSJJBase.String;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Render
{
    public class OverLay
    {
        public static void DrawVerticalHealthBar(Rect targetRect, float healthPercent,
                                   float barWidth = 4f, float barSpacing = 2f,
                                   bool onLeft = true)
        {
            Rect healthBarBgRect;

            if (onLeft)
            {
                healthBarBgRect = new Rect(
                    targetRect.x - barWidth - barSpacing,
                    targetRect.y,
                    barWidth,
                    targetRect.height
                );
            }
            else
            {
                healthBarBgRect = new Rect(
                    targetRect.x + targetRect.width + barSpacing,
                    targetRect.y,
                    barWidth,
                    targetRect.height
                );
            }

            float fillHeight = healthBarBgRect.height * healthPercent;
            Rect healthBarFillRect = new Rect(
                healthBarBgRect.x,
                healthBarBgRect.y,
                healthBarBgRect.width,
                fillHeight
            );

            ImmediateRenderer.DrawBoxFilled(healthBarBgRect, new Color(0.3f, 0.3f, 0.3f, 0.7f));
            Color color = Color.Lerp(Color.red, Color.green, healthPercent);
            ImmediateRenderer.DrawBoxFilled(healthBarFillRect, color);
            ImmediateRenderer.DrawBoxOutline(healthBarBgRect, Color.black, 1f);
        }

        public static void DrawSkeleton(PlayerInfo enemy, Color color, float thickness = 1f)
        {
            if (enemy == null) return;

            Dictionary<IgnoreCaseString, Vector3> boneScreenPositions = new Dictionary<IgnoreCaseString, Vector3>();
            var allTransforms = enemy.GetPlayerAllTransform();

            if (allTransforms == null) return;

            foreach (var bonePair in allTransforms)
            {
                if (bonePair.Value == null) continue;

                Vector3 worldPos = bonePair.Value.position;
                Vector3 screenPos = ViewportUtility.WorldPointToScreenPoint(worldPos);
                boneScreenPositions[bonePair.Key] = screenPos;
            }

            const string Pelvis = "Bip01_Pelvis";
            const string Spine = "Bip01_Spine";
            const string Spine1 = "Bip01_Spine1";
            const string Spine2 = "Bip01_Spine2";
            const string Neck = "Bip01_Neck";
            const string Head = "Bip01_Head";
            const string LThigh = "Bip01_L_Thigh";
            const string LCalf = "Bip01_L_Calf";
            const string LFoot = "Bip01_L_Foot";
            const string RThigh = "Bip01_R_Thigh";
            const string RCalf = "Bip01_R_Calf";
            const string RFoot = "Bip01_R_Foot";
            const string LClavicle = "Bip01_L_Clavicle";
            const string LUpperArm = "Bip01_L_UpperArm";
            const string LForearm = "Bip01_L_Forearm";
            const string LHand = "Bip01_L_Hand";
            const string RClavicle = "Bip01_R_Clavicle";
            const string RUpperArm = "Bip01_R_UpperArm";
            const string RForearm = "Bip01_R_Forearm";
            const string RHand = "Bip01_R_Hand";

            DrawBoneConnection(boneScreenPositions, Pelvis, Spine, color, thickness);
            DrawBoneConnection(boneScreenPositions, Spine, Spine1, color, thickness);
            DrawBoneConnection(boneScreenPositions, Spine1, Spine2, color, thickness);
            DrawBoneConnection(boneScreenPositions, Spine2, Neck, color, thickness);
            DrawBoneConnection(boneScreenPositions, Neck, Head, color, thickness);

            // 绘制头部
            if (enemy.Career == "rpg_by_parasitism")
            {
                // 特殊角色直接用 Bone05 画点
                Transform bone05 = enemy.GetPlayerTransform("Bone05");
                if (bone05 != null)
                {
                    Vector3 bone05ScreenPos = ViewportUtility.WorldPointToScreenPoint(bone05.position);
                    Vector2 bone05Pos = new Vector2(bone05ScreenPos.x, Screen.height - bone05ScreenPos.y);
                    ImmediateRenderer.DrawCircleFilled(bone05Pos, 5f, color, 12);
                }
            }
            else if (boneScreenPositions.TryGetValue(Head, out Vector3 headScreenPos) &&
                enemy.GetPlayerTransform("Bip01_Head")?.GetChild(0) != null)
            {
                Vector3 headNubScreenPos = ViewportUtility.WorldPointToScreenPoint(
                    enemy.GetPlayerTransform("Bip01_Head").GetChild(0).position
                );

                Vector3 headCenter = (headScreenPos + headNubScreenPos) * 0.5f;
                Vector2 headCenterPos = new Vector2(headCenter.x, Screen.height - headCenter.y);

                float headRadius = Vector3.Distance(headScreenPos, headNubScreenPos) * 0.5f;

                ImmediateRenderer.DrawCircleOutline(headCenterPos, headRadius, 32, color);
            }

            DrawBoneConnection(boneScreenPositions, Pelvis, LThigh, color, thickness);
            DrawBoneConnection(boneScreenPositions, LThigh, LCalf, color, thickness);
            DrawBoneConnection(boneScreenPositions, LCalf, LFoot, color, thickness);

            DrawBoneConnection(boneScreenPositions, Pelvis, RThigh, color, thickness);
            DrawBoneConnection(boneScreenPositions, RThigh, RCalf, color, thickness);
            DrawBoneConnection(boneScreenPositions, RCalf, RFoot, color, thickness);

            DrawBoneConnection(boneScreenPositions, Spine2, LClavicle, color, thickness);
            DrawBoneConnection(boneScreenPositions, LClavicle, LUpperArm, color, thickness);
            DrawBoneConnection(boneScreenPositions, LUpperArm, LForearm, color, thickness);
            DrawBoneConnection(boneScreenPositions, LForearm, LHand, color, thickness);

            DrawBoneConnection(boneScreenPositions, Spine2, RClavicle, color, thickness);
            DrawBoneConnection(boneScreenPositions, RClavicle, RUpperArm, color, thickness);
            DrawBoneConnection(boneScreenPositions, RUpperArm, RForearm, color, thickness);
            DrawBoneConnection(boneScreenPositions, RForearm, RHand, color, thickness);
        }

        private static void DrawBoneConnection(
            Dictionary<IgnoreCaseString, Vector3> boneScreenPositions,
            string boneName1,
            string boneName2,
            Color color,
            float thickness = 1f)
        {
            IgnoreCaseString key1 = boneName1;
            IgnoreCaseString key2 = boneName2;

            if (boneScreenPositions.TryGetValue(key1, out Vector3 startPos) &&
                boneScreenPositions.TryGetValue(key2, out Vector3 endPos))
            {
                Vector2 start = new Vector2(startPos.x, Screen.height - startPos.y);
                Vector2 end = new Vector2(endPos.x, Screen.height - endPos.y);

                ImmediateRenderer.DrawLine(start, end, color, thickness);
            }
        }
    }
}
