using SkyDome.Engine;
using SkyDome.Entity;
using UnityEngine;
using System;

namespace SkyDome.Feature.AutoTrigger
{
    public class MikadukiSwordDkl : MonoBehaviour
    {
        private void Update()
        {
            // 空判
            if (PlayerUpdate.LocalEntity == null ||
                PlayerUpdate.LocalEntity._entity == null ||
                PlayerUpdate.LocalEntity.IsDead)
                return;

            if (PlayerUpdate.LocalEntity.CurrentWeaponName != "mikaduki_sword_dkl") // 幻锋ID
                return;

            if (Contexts.sharedInstance == null || Contexts.sharedInstance.weapon == null) return;
            var weaponEntity = Contexts.sharedInstance.weapon.currentWeaponEntity;

            if (weaponEntity == null || weaponEntity.special == null) return;

            if ((weaponEntity.special.EffectLevel & 8192) == 0)
                return;

            var viewAnim = PlayerUpdate.LocalEntity._entity.viewAnim;
            if (viewAnim == null ||
                viewAnim.ViewAnimInfo == null ||
                viewAnim.ViewAnimInfo.StateId != 69) // 69蓄力动作
                return;

            int level = weaponEntity.special.Level;
            if (level <= 0) return;

            int remainTime = viewAnim.RemainTime;
            if (remainTime >= 400) return;

            int clientTime = PlayerUpdate.LocalEntity.CilentTime;

            int timeOffset = clientTime - level;

            if (timeOffset >= -20 && timeOffset <= 75)
            {
                MouseSimulator.ForceKey(KeyCode.E, MouseSimulator.InputState.TrueOnce);
            }
        }
    }
}
