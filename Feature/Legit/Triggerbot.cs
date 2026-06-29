using Assets.Sources.Components.Interface.Info.Weapon;
using Assets.Sources.Components.Weapon;
using Assets.Sources.Utils.Weapon;
using NetData;
using share;
using SkyDome.Cfg;
using SkyDome.Engine;
using SkyDome.Entity;
using UnityEngine;
using weapon;

namespace SkyDome.Feature.Legit
{
    public class Triggerbot : MonoBehaviour
    {
        // 公有属性
        public static bool IsActive => _isTriggerbotActive;
        public static float ActivatedTime => _triggerbotActivatedTime;
        public static float RemainingTime
        {
            get
            {
                if (!_isTriggerbotActive) return 0f;
                float elapsed = Time.time - _triggerbotActivatedTime;
                return Mathf.Max(0f, Config.TriggerbotActiveDuration - elapsed);
            }
        }

        // 私有字段
        private float _lastTriggerTime; // 上次触发时间
        private static float _triggerbotActivatedTime; // 扳机激活时间
        private static bool _isTriggerbotActive;  // 扳机是否已激活
        private int _lastShotsFired; // 上次开火次数
        private int _lastWeaponSlot; // 上次武器槽位

        private void Update()
        {
            if (!Config.Triggerbot)
            {
                _isTriggerbotActive = false;
                _lastShotsFired = 0;
                _lastWeaponSlot = 0;
                return;
            }

            if (PlayerUpdate.LocalEntity == null || PlayerUpdate.LocalEntity.IsDead)
            {
                _isTriggerbotActive = false;
                _lastShotsFired = 0;
                _lastWeaponSlot = 0;
                return;
            }

            var weaponData = Contexts.sharedInstance.weapon;
            if (weaponData == null || weaponData.currentWeaponEntity == null)
            {
                return;
            }

            // 检测武器槽位变化
            int currentSlot = PlayerUpdate.LocalEntity.CurrentWeaponId;
            if (currentSlot != _lastWeaponSlot)
            {
                // 切换武器时重置扳机状态
                _isTriggerbotActive = false;
                _lastShotsFired = 0;
                _lastWeaponSlot = currentSlot;
                return;
            }

            // 是否为狙击枪
            bool isSniper = weaponData.currentWeaponEntity.basicInfo.Info.WeaponType == 5;

            // 武器槽位是否为1
            bool isSlot1 = currentSlot == 1;

            bool shouldUseDelayedActivation = Config.TriggerbotDelayedActivation && isSlot1 && !isSniper;

            if (isSniper && !PlayerUpdate.LocalEntity.Fov.IsZoom() && Config.ExcludeSniper)
            {
                return;
            }

            // 玩家是否开火
            int currentShotsFired = weaponData.currentWeaponEntity.basicInfo.Data.ShotsFired;

            if (currentShotsFired > _lastShotsFired)
            {
                if (shouldUseDelayedActivation)
                {
                    _isTriggerbotActive = true;
                    _triggerbotActivatedTime = Time.time;
                }
            }

            _lastShotsFired = currentShotsFired;

            if (shouldUseDelayedActivation)
            {
                if (_isTriggerbotActive)
                {
                    float elapsedTime = Time.time - _triggerbotActivatedTime;
                    if (elapsedTime > Config.TriggerbotActiveDuration)
                    {
                        _isTriggerbotActive = false;
                    }
                }

                if (!_isTriggerbotActive)
                {
                    return;
                }
            }

            var forward = SSJJMath.VectorCoordConverter.UnityToSsjj(Camera.main.transform.forward);
            var shotDirection = Config.SpreadPredict ? CalculateShotDirection() : new Vector3D(forward.x, forward.y, forward.z);
            var result = FireUtility.BulletTrace(
                Contexts.sharedInstance.battleRoom.pyEngine.PyEngine,
                PlayerUpdate.LocalEntity._entity,
                Contexts.sharedInstance.player,
                100000f,
                shotDirection,
                new float[3],
                new float[3],
                false
            );

            if (result.EntityId <= 0) return;

            foreach (var player in PlayerUpdate.EntityList)
            {
                if (player.Team == PlayerUpdate.LocalEntity.Team) continue;
                if (player.Id != result.EntityId) continue;

                if (IsValidTarget(player))
                {
                    TryTriggerShooting();
                }
                break;
            }
        }

        private void TryTriggerShooting()
        {
            if (Time.time - _lastTriggerTime >= 0.01f)
            {
                MouseSimulator.ForceMouseButton(0, MouseSimulator.InputState.TrueOnce);
                _lastTriggerTime = Time.time;
            }
        }

        private bool IsValidTarget(PlayerInfo target)
        {
            return target != null &&
                   !target.IsDead &&
                   target.Team != PlayerUpdate.LocalEntity.Team &&
                   !target.State.GetPlayerStateType(1);
        }

        private Vector3D CalculateShotDirection()
        {
            float yaw = PlayerUpdate.LocalEntity.ViewPos.y + 2f * PlayerUpdate.LocalEntity.Punch.y;
            float pitch = PlayerUpdate.LocalEntity.ViewPos.x + 2f * PlayerUpdate.LocalEntity.Punch.x;
            int seed = Contexts.sharedInstance.userCommand.commands.CommandToSendList.Last.Value.Seq + 1;
            float spread = Contexts.sharedInstance.weapon.currentWeaponEntity.spread.Spread;
            float spreadScaleY = Contexts.sharedInstance.weapon.currentWeaponEntity.spread.SpreadScaleY;

            BasicInfoComponent weaponEntity = Contexts.sharedInstance.weapon.currentWeaponEntity.basicInfo;
            IEntitsWeaponInfo info = weaponEntity.Info;
            BaseWeaponData data = weaponEntity.Data;
            double shotsFiredSpread = FireUtility.CalShotsFiredSpread(data.ShotsFiredSpreadMin, data.ShotsFiredSpreadMax, data.ShotsFiredSpreadTime, data.ShotsFired, info.AttackInterval);
            return ShootingDirUtils.CalculateShotingDir(seed, yaw, pitch, spread, spreadScaleY, shotsFiredSpread);
        }
    }
}
