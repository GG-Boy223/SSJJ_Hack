using Assets.Sources.Utils.Weapon;
using share;
using SkyDome.Cfg;
using SkyDome.Engine;
using SkyDome.Entity;
using SkyDome.Render;
using SkyDome.Utilities;
using SSJJMath;
using SSJJPhysics;
using System.Linq;
using UnityEngine;
using weapon.utils;
using Vector3 = UnityEngine.Vector3;

namespace SkyDome.Feature.Legit
{
    public class Aimbot : MonoBehaviour
    {
        private double _currentSpreadFactor;
        public static bool _isActive;
        public static PlayerInfo _currentTarget;
        private Vector2 ScreenCenter => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        private void Start()
        {
            MouseSimulator.PreInputCallback += OnPreInput;
        }

        private void OnGUI()
        {
            // 绘制自瞄范围圈
            if (Config.Aimbot && Config.AimRange_Show)
            {
                DrawAimbotFOVCircle();
            }

            // 绘制目标连线
            if (Config.ShowAimLine)
            {
                DrawTargetLine();
            }
        }

        // 绘制自瞄FOV范围圈
        private void DrawAimbotFOVCircle()
        {
            if (PlayerUpdate.MainCamera == null) return;

            // 计算屏幕中心的FOV圆半径
            float fovRadians = Config.AimbotFOV * Mathf.Deg2Rad * 0.5f;
            float screenHeight = Screen.height;

            // 根据相机FOV和自瞄FOV计算屏幕上的像素半径
            float cameraFOV = PlayerUpdate.MainCamera.fieldOfView;
            float radius = Mathf.Tan(fovRadians) / Mathf.Tan(cameraFOV * Mathf.Deg2Rad * 0.5f) * screenHeight * 0.5f;

            // 绘制半透明圆圈
            ImmediateRenderer.DrawCircleOutline(
                ScreenCenter,
                radius,
                64,
                new Color(1f, 1f, 0f, 0.5f) // 黄色半透明
            );
        }

        // 每帧更新扩散系数
        private void Update()
        {
            if (Contexts.sharedInstance?.weapon == null) return;

            var weapon = Contexts.sharedInstance.weapon.currentWeaponEntity;
            if (weapon == null ||
                weapon.basicInfo == null ||
                weapon.basicInfo.Data == null ||
                weapon.basicInfo.Info == null)
                return;

            _currentSpreadFactor = FireUtility.CalShotsFiredSpread(
                weapon.basicInfo.Data.ShotsFiredSpreadMin,
                weapon.basicInfo.Data.ShotsFiredSpreadMax,
                weapon.basicInfo.Data.ShotsFiredSpreadTime,
                weapon.basicInfo.Data.ShotsFired,
                weapon.basicInfo.Info.AttackInterval
            );
        }

        // 输入前处理
        private void OnPreInput()
        {
            if (Config.Aimbot)
            {
                UpdateTarget();
                ProcessAiming();
            }
            else
            {
                _currentTarget = null;
                _isActive = false;
            }
        }

        // 绘制目标连线
        private void DrawTargetLine()
        {
            // 空检查
            if (PlayerUpdate.EntityList == null || PlayerUpdate.MainCamera == null)
                return;

            foreach (var player in PlayerUpdate.EntityList)
            {
                if (player == null || player._entity == null)
                    continue;

                Vector3 worldPos = GetAimPosition(player);
                Vector3 screenPos = PlayerUpdate.MainCamera.WorldToScreenPoint(worldPos);

                if (!ViewportUtility.IsScreenPointVisible(screenPos)) continue;

                Vector2 screenPos2D = new Vector2(screenPos.x, screenPos.y);

                if (_currentTarget != null &&
                    _currentTarget._entity != null &&
                    _currentTarget._entity == player._entity)
                {
                    ImmediateRenderer.DrawLine(ScreenCenter, screenPos2D, Color.magenta, 2f);
                }
            }
        }

        // 可见性检测
        private bool IsVisible(PlayerInfo target)
        {
            if (target == null || PlayerUpdate.MainCamera == null) return false;

            Vector3 targetPos = GetAimPosition(target);
            Vector3 cameraPos = PlayerUpdate.MainCamera.transform.position;
            Vector3 direction = (targetPos - cameraPos).normalized;
            Vector3 dirSsjj = VectorCoordConverter.UnityToSsjj(direction);

            var result = FireUtility.BulletTrace(
                Contexts.sharedInstance.battleRoom.pyEngine.PyEngine,
                PlayerUpdate.LocalEntity._entity,
                Contexts.sharedInstance.player,
                10000f,
                new Vector3D(dirSsjj.x, dirSsjj.y, dirSsjj.z),
                new float[3],
                new float[3],
                false
            );

            return result.EntityId == target.Id;
        }

        // 获取瞄准位置
        private Vector3 GetAimPosition(PlayerInfo player)
        {
            Transform targetBone = null;

            switch (Config.AimPos)
            {
                case 0: // 头心
                        // 特殊角色使用 Bone05
                    if (player.Career == "rpg_by_parasitism")
                    {
                        targetBone = player.GetPlayerTransform("Bone05");
                        if (targetBone != null)
                            return targetBone.position;
                    }

                    // 普通角色使用 Head 和 HeadNub 的中点
                    Transform headTransform = player.GetPlayerTransform("Bip01_Head");
                    Transform headNubTransform = player.GetValidHeadNub();
                    if (headTransform != null && headNubTransform != null)
                    {
                        return (headTransform.position + headNubTransform.position) * 0.5f;
                    }
                    targetBone = headTransform;
                    break;

                case 1: // 头顶
                    targetBone = player.GetValidHeadNub();
                    break;

                case 2: // 脖子
                    targetBone = player.GetPlayerTransform("Bip01_Neck");
                    break;

                case 3: // 腹部
                    targetBone = player.GetPlayerTransform("Bip01_Spine");
                    break;

                case 4: // 左锁骨
                    targetBone = player.GetPlayerTransform("Bip01_L_Clavicle");
                    break;

                case 5: // 右锁骨
                    targetBone = player.GetPlayerTransform("Bip01_R_Clavicle");
                    break;

                case 6: // 左上臂
                    targetBone = player.GetPlayerTransform("Bip01_L_UpperArm");
                    break;

                case 7: // 右上臂
                    targetBone = player.GetPlayerTransform("Bip01_R_UpperArm");
                    break;

                case 8: // 左前臂
                    targetBone = player.GetPlayerTransform("Bip01_L_Forearm");
                    break;

                case 9: // 右前臂
                    targetBone = player.GetPlayerTransform("Bip01_R_Forearm");
                    break;

                case 10: // 左手
                    targetBone = player.GetPlayerTransform("Bip01_L_Hand");
                    break;

                case 11: // 右手
                    targetBone = player.GetPlayerTransform("Bip01_R_Hand");
                    break;

                case 12: // 左手指
                    targetBone = player.GetPlayerTransform("Bip01_L_Finger0");
                    break;

                case 13: // 右手指
                    targetBone = player.GetPlayerTransform("Bip01_R_Finger0");
                    break;

                case 14: // 骨盆
                    targetBone = player.GetPlayerTransform("Bip01_Pelvis");
                    break;

                case 15: // 左大腿
                    targetBone = player.GetPlayerTransform("Bip01_L_Thigh");
                    break;

                case 16: // 右大腿
                    targetBone = player.GetPlayerTransform("Bip01_R_Thigh");
                    break;

                case 17: // 左小腿
                    targetBone = player.GetPlayerTransform("Bip01_L_Calf");
                    break;

                case 18: // 右小腿
                    targetBone = player.GetPlayerTransform("Bip01_R_Calf");
                    break;

                case 19: // 左脚
                    targetBone = player.GetPlayerTransform("Bip01_L_Foot");
                    break;

                case 20: // 右脚
                    targetBone = player.GetPlayerTransform("Bip01_R_Foot");
                    break;

                case 21: // 左脚趾
                    targetBone = player.GetPlayerTransform("Bip01_L_Toe0");
                    break;

                case 22: // 右脚趾
                    targetBone = player.GetPlayerTransform("Bip01_R_Toe0");
                    break;

                default:
                    // 默认头心
                    Transform defaultHead = player.GetPlayerTransform("Bip01_Head");
                    Transform defaultHeadNub = player.GetValidHeadNub();
                    if (defaultHead != null && defaultHeadNub != null)
                    {
                        return (defaultHead.position + defaultHeadNub.position) * 0.5f;
                    }
                    targetBone = defaultHead;
                    break;
            }

            // 如果目标骨骼为空，则返回玩家位置
            return targetBone != null ? targetBone.position : player.GetPlayerTransform(player.PlayerName).position;
        }

        // 更新目标
        private void UpdateTarget()
        {
            // 空检查
            if (PlayerUpdate.MainCamera == null ||
                PlayerUpdate.EntityList == null ||
                PlayerUpdate.LocalEntity == null)
                return;

            if (_isActive && _currentTarget != null && !_currentTarget.IsDead)
            {
                if (Config.VisibleCheck && !IsVisible(_currentTarget))
                {
                    _currentTarget = null;
                    _isActive = false;
                }
                else
                {
                    return;
                }
            }

            _currentTarget = null;
            float minAngleDelta = float.MaxValue;

            Vector3 cameraForward = PlayerUpdate.MainCamera.transform.forward;
            Vector3 cameraPosition = PlayerUpdate.MainCamera.transform.position;

            foreach (var player in PlayerUpdate.EntityList)
            {
                if (player.Team == PlayerUpdate.LocalEntity.Team || player.IsDead) continue;

                if (Config.VisibleCheck && !IsVisible(player)) continue;

                Vector3 targetPos = GetAimPosition(player);
                Vector3 directionToTarget = (targetPos - cameraPosition).normalized;

                // 计算角度差
                float angleDelta = Vector3.Angle(cameraForward, directionToTarget);

                // 检查是否在FOV范围内
                if (angleDelta > Config.AimbotFOV * 0.5f) continue;

                if (angleDelta < minAngleDelta)
                {
                    minAngleDelta = angleDelta;
                    _currentTarget = player;
                }
            }
        }

        // 处理瞄准逻辑
        private void ProcessAiming()
        {
            if (!Input.GetKey(Config.AimKey) || _currentTarget == null)
            {
                _isActive = false;
                return;
            }

            // 排除投掷物/战术道具（槽位大于3的武器不自瞄）
            var weapon = Contexts.sharedInstance.weapon.currentWeaponEntity;
            if (weapon != null && weapon.slot.Slot > 3)
            {
                _isActive = false;
                return;
            }

            _isActive = true;
            Vector3 targetPosition = GetAimPosition(_currentTarget);

            if (Config.Aimbot_Smooth)
            {
                Vector3 screenPos = PlayerUpdate.MainCamera.WorldToScreenPoint(targetPosition);

                if (screenPos.z < 0) return;

                // 计算屏幕中心到目标的偏移量 (Delta)
                Vector2 targetScreen = new Vector2(screenPos.x, screenPos.y);
                Vector2 currentScreen = ScreenCenter;
                Vector2 delta = targetScreen - currentScreen;

                // 计算移动量
                float smooth = Mathf.Max(1f, Config.Aimbot_SmoothFactor);

                // 灵敏度补偿系数，用于将像素距离转换为合适的鼠标轴输入量
                float sensitivity = 0.15f;

                // 计算本次应该移动的鼠标 Delta
                Vector2 mouseDelta = (delta / smooth) * sensitivity;

                // 注入模拟输入
                MouseSimulator.ForceAxisDelta += mouseDelta;
            }
            else
            {
                Vector2 aimAngles = CalculateAimAngles(targetPosition);

                if (Config.NoRecoil)
                {
                    // 开启无后座：需要减去 Punch * 2 (抵消后坐力上抬)
                    Contexts.sharedInstance.userCommand.input.Pitch = aimAngles.x - PlayerUpdate.LocalEntity.Punch.x * 2f;
                    Contexts.sharedInstance.userCommand.input.Yaw = aimAngles.y - PlayerUpdate.LocalEntity.Punch.y * 2f;
                    return;
                }

                Contexts.sharedInstance.userCommand.input.Pitch = aimAngles.x;
                Contexts.sharedInstance.userCommand.input.Yaw = aimAngles.y;
            }
        }

        // 计算瞄准角度 (仅用于暴力锁死模式)
        private Vector2 CalculateAimAngles(Vector3 targetPosition)
        {
            Vector3 cameraPosition = PlayerUpdate.MainCamera.transform.position;
            Vector3 selfVelocity = VectorCoordConverter.SsjjToUnity(PlayerUpdate.LocalEntity.Move.Velocity);

            float frameInterval = Contexts.sharedInstance.userCommand.commands.CommandToSendList.Last.Value.FrameInterval * 0.001f;
            Vector3 predictedPosition = cameraPosition + selfVelocity * frameInterval;

            Vector3 direction = (targetPosition - predictedPosition).normalized;

            if (Config.SpreadPredict && Input.GetKey(KeyCode.Mouse0))
            {
                direction = ApplySpreadOffset(direction);
            }

            float pitch = Mathf.Asin(direction.y) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg - 90f;

            pitch = Mathf.Clamp(pitch, -89f, 89f);
            yaw %= 360f;
            yaw = yaw > 180f ? yaw - 360f : yaw;

            return new Vector2(pitch, yaw);
        }

        // 应用扩散偏移
        private Vector3 ApplySpreadOffset(Vector3 baseDirection)
        {
            int seed = Contexts.sharedInstance.userCommand.commands.CommandToSendList.Last.Value.Seq + 1;

            double randX = UniformRandom.RandomFloat(seed, -0.5, 0.5) +
                          UniformRandom.RandomFloat(seed + 1, -0.5, 0.5);

            double randY = UniformRandom.RandomFloat(seed + 2, -0.5, 0.5) +
                          UniformRandom.RandomFloat(seed + 3, -0.5, 0.5);

            float spread = Contexts.sharedInstance.weapon.currentWeaponEntity.spread.Spread;
            float spreadScaleY = Contexts.sharedInstance.weapon.currentWeaponEntity.spread.SpreadScaleY;

            double offsetX = spread * randX * _currentSpreadFactor;
            double offsetY = spread * randY * _currentSpreadFactor * spreadScaleY;

            Vector3 right = Vector3.Cross(baseDirection, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, baseDirection).normalized;

            return (baseDirection + right * (float)offsetX + up * (float)offsetY).normalized;
        }
    }
}
