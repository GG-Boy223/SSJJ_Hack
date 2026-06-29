using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Render;
using SkyDome.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public class BoundingBox3D : MonoBehaviour
    {
        // 关键骨骼列表
        private static readonly string[] KeyBones = new string[]
        {
            "Bip01",                  // 根骨骼
            "Bip01_Pelvis",           // 盆骨
            "Bip01_Spine",            // 脊柱 (下腰)
            "Bip01_Spine1",           // 脊柱1 (胸部)
            "Bip01_Neck",             // 脖子
            "Bip01_Head",             // 头
            "Bip01_HeadNub",          // 头顶末端
            "Bip01_L_Thigh",          // 左大腿
            "Bip01_L_Calf",           // 左小腿 (膝盖)
            "Bip01_L_Foot",           // 左脚
            "Bip01_L_Toe0",           // 左脚趾
            //"Bip01_L_Toe0Nub",        // 左脚趾末端
            "Bip01_R_Thigh",          // 右大腿
            "Bip01_R_Calf",           // 右小腿 (膝盖)
            "Bip01_R_Foot",           // 右脚
            "Bip01_R_Toe0",           // 右脚趾
            //"Bip01_R_Toe0Nub",        // 右脚趾末端
            "Bip01_L_Clavicle",       // 左锁骨/肩膀
            "Bip01_L_UpperArm",       // 左大臂
            "Bip01_L_Forearm",        // 左小臂
            "Bip01_L_Hand",           // 左手掌
            "Bip01_L_Finger0",        // 左大拇指
            "Bip01_L_Finger01",       // 左大拇指关节1
            "Bip01_L_Finger0Nub",     // 左大拇指末端
            "Bip01_L_Finger1",        // 左食指
            "Bip01_L_Finger11",       // 左食指关节1
            "Bip01_L_Finger1Nub",     // 左食指末端
            "Bip01_L_Finger2",        // 左中指
            "Bip01_L_Finger21",       // 左中指关节1
            "Bip01_L_Finger2Nub",     // 左中指末端
            "Bip01_R_Clavicle",       // 右锁骨/肩膀
            "Bip01_R_UpperArm",       // 右大臂
            "Bip01_R_Forearm",        // 右小臂
            "Bip01_R_Hand",           // 右手掌
            "Bip01_R_Finger0",        // 右大拇指
            "Bip01_R_Finger01",       // 右大拇指关节1
            "Bip01_R_Finger0Nub",     // 右大拇指末端
            "Bip01_R_Finger1",        // 右食指
            "Bip01_R_Finger11",       // 右食指关节1
            "Bip01_R_Finger1Nub",     // 右食指末端
            "Bip01_R_Finger2",        // 右中指
            "Bip01_R_Finger21",       // 右中指关节1
            "Bip01_R_Finger2Nub",     // 右中指末端
            //"Bip01_Footsteps",        // 脚步声触发点
        };

        private void OnGUI()
        {
            if (!Config.Show3DBox || !Config.WallHack || PlayerUpdate.EntityList == null)
                return;

            if (PlayerUpdate.MainCamera == null) return;

            foreach (var player in PlayerUpdate.EntityList)
            {
                if (player.Team != PlayerUpdate.LocalEntity.Team && !player.IsDead)
                {
                    Draw3DBoundingBox(player);
                }
            }
        }

        private void Draw3DBoundingBox(PlayerInfo player)
        {
            // 1. 获取紧凑的包围盒 (无Padding)
            Bounds bounds = CalculateTightBounds(player);
            if (bounds.size == Vector3.zero) return;

            // 2. 获取8个顶点
            Vector3[] worldCorners = GetBoundsCorners(bounds);
            Vector2[] screenCorners = new Vector2[8];

            // 3. 转换坐标
            for (int i = 0; i < 8; i++)
            {
                Vector3 screenPos = WorldToScreenPoint(worldCorners[i]);

                // 如果有点在相机后面 (Z < 0)，则不绘制
                if (screenPos.z < 0) return;

                screenCorners[i] = new Vector2(screenPos.x, screenPos.y);
            }

            // 4. 绘制 (固定青色)
            DrawBox3DEdges(screenCorners, Color.cyan);
        }

        // 坐标转换：适配 GL 绘图 (原点在左下角，Y轴向上)，不反转Y轴
        private Vector3 WorldToScreenPoint(Vector3 worldPoint)
        {
            Camera cam = PlayerUpdate.MainCamera;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPoint);

            // 处理分辨率缩放
            float screenScaleFactor = (float)Screen.height / cam.scaledPixelHeight;

            screenPos.x *= screenScaleFactor;
            screenPos.y *= screenScaleFactor;

            return screenPos;
        }

        // 计算紧凑的包围盒 (无Padding，无回退)
        private Bounds CalculateTightBounds(PlayerInfo player)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool hasData = false;

            // 只遍历关键骨骼
            foreach (string boneName in KeyBones)
            {
                Transform bone = player.GetPlayerTransform(boneName);
                if (bone == null) continue;

                Vector3 pos = bone.position;

                // 扩展边界
                min = Vector3.Min(min, pos);
                max = Vector3.Max(max, pos);
                hasData = true;
            }

            if (!hasData) return new Bounds();

            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;

            return new Bounds(center, size);
        }

        private Vector3[] GetBoundsCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            return new Vector3[]
            {
                new Vector3(min.x, min.y, min.z), // 0
                new Vector3(max.x, min.y, min.z), // 1
                new Vector3(max.x, min.y, max.z), // 2
                new Vector3(min.x, min.y, max.z), // 3
                new Vector3(min.x, max.y, min.z), // 4
                new Vector3(max.x, max.y, min.z), // 5
                new Vector3(max.x, max.y, max.z), // 6
                new Vector3(min.x, max.y, max.z)  // 7
            };
        }

        private void DrawBox3DEdges(Vector2[] p, Color color)
        {
            float w = 1.5f; // 线宽

            // 底部矩形
            ImmediateRenderer.DrawLine(p[0], p[1], color, w);
            ImmediateRenderer.DrawLine(p[1], p[2], color, w);
            ImmediateRenderer.DrawLine(p[2], p[3], color, w);
            ImmediateRenderer.DrawLine(p[3], p[0], color, w);

            // 顶部矩形
            ImmediateRenderer.DrawLine(p[4], p[5], color, w);
            ImmediateRenderer.DrawLine(p[5], p[6], color, w);
            ImmediateRenderer.DrawLine(p[6], p[7], color, w);
            ImmediateRenderer.DrawLine(p[7], p[4], color, w);

            // 垂直连接线
            ImmediateRenderer.DrawLine(p[0], p[4], color, w);
            ImmediateRenderer.DrawLine(p[1], p[5], color, w);
            ImmediateRenderer.DrawLine(p[2], p[6], color, w);
            ImmediateRenderer.DrawLine(p[3], p[7], color, w);
        }
    }
}
