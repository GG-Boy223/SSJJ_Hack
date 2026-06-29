using SkyDome.Cfg;
using SkyDome.Engine;
using SkyDome.Entity;
using UnityEngine;

namespace SkyDome.Feature.AutoTrigger
{
    public class NonStopDanceAuto : MonoBehaviour
    {
        // 上次按键的位置索引
        private int _lastPressedIndex = -1;

        private void Update()
        {
            // 空判
            if (PlayerUpdate.LocalEntity == null || PlayerUpdate.LocalEntity.IsDead) return;
            
            // 检查背包中是否有NonStopDance
            if (!HasNonStopDanceInBag()) return;

            var nonStopData = Contexts.sharedInstance?.gameRule?.nonStopData;
            if (nonStopData == null || !nonStopData._show || nonStopData._showOver) return;

            var positions = nonStopData._positionList;
            var results = nonStopData._positionResultList;
            if (positions == null || results == null) return;

            // 找到第一个未完成的位置
            for (int i = 0; i < positions.Count && i < results.Count; i++)
            {
                if (results[i] || string.IsNullOrEmpty(positions[i])) continue;

                // 如果是新的位置（上一个已确认），才按键
                if (i != _lastPressedIndex)
                {
                    PressKey(positions[i]);
                    _lastPressedIndex = i;
                }
                return;
            }

            // 全部完成，重置
            _lastPressedIndex = -1;
        }

        // 检查当前背包的主武器是否是NonStopDance
        private bool HasNonStopDanceInBag()
        {
            try
            {
                var playerEntity = PlayerUpdate.LocalEntity?._entity;
                if (playerEntity == null) return false;

                // 获取当前背包ID
                int currentBagId = playerEntity.GetCurrentBagId();
                
                // 获取背包数据
                var playerBagContext = Contexts.sharedInstance?.playerBag;
                if (playerBagContext == null) return false;

                var bagEntity = playerBagContext.GetEntityWithBagId(currentBagId);
                if (bagEntity == null || !bagEntity.hasBasicInfo) return false;

                var bagData = bagEntity.basicInfo.Data;
                if (bagData == null) return false;

                // 检查主武器
                return bagData.FirstWeapon != null &&
                       !string.IsNullOrEmpty(bagData.FirstWeapon.WeaponName) &&
                       bagData.FirstWeapon.WeaponName.ToLower().Contains("nonstopdance");
            }
            catch
            {
                return false;
            }
        }

        // 解析并按下对应的键
        private void PressKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length != 1) return;

            char c = char.ToUpper(key[0]);
            if (c >= 'A' && c <= 'Z')
            {
                KeyCode keyCode = (KeyCode)((int)KeyCode.A + (c - 'A'));
                MouseSimulator.ForceKey(keyCode, MouseSimulator.InputState.TrueOnce);
            }
        }
    }
}
