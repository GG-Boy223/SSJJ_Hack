using NetData;
using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Feature.Legit;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Feature
{
    public class Resolver : MonoBehaviour
    {
        public static Dictionary<int, (float originalPitch, bool isFakeApplied, float lastActualPitch)>
         targetResolverDict = new Dictionary<int, (float, bool, float)>();

        public static Dictionary<int, float> _lastExecutionTimes = new Dictionary<int, float>();
        private void Update()
        {
            if (Config.Resolver_Random)
            {
                RandomPlayerViewPitch();
                return;
            }
            ResolverAngle();
        }

        private void RandomPlayerViewPitch()
        {
            foreach (var player in PlayerUpdate.EntityList)
            {
                if (player.Team != PlayerUpdate.LocalEntity.Team && isAA(player))
                {
                    int playerId = player.Id;
                    float currentTime = Time.time;
                    if (_lastExecutionTimes.TryGetValue(playerId, out float lastTime) &&
            currentTime - lastTime < 0.05f)
                    {
                        return;
                    }
                    _lastExecutionTimes[playerId] = currentTime;

                    player._entity.basicInfo.Current.ViewPitch = -player._entity.basicInfo.Current.ViewPitch;
                }
            }
        }
        private bool isAA(PlayerInfo playerEntity)
        {
            PlayerEntityData basic = playerEntity._entity.basicInfo.Current;
            if (basic.ViewPitch > 30f || basic.ViewPitch < -30f)
            {
                return true;
            }
            return false;
        }
        private void ResolverAngle()
        {
            if (!Config.Resolver && Aimbot._currentTarget != null)
            {
                return;
            }
            if (Aimbot._currentTarget != null)
            {
                bool isKeyDown = Input.GetKey(Config.ResolverKey);
                bool isKeyUp = Input.GetKeyUp(Config.ResolverKey);

                int targetID = Aimbot._currentTarget.Id;

                if (!targetResolverDict.ContainsKey(targetID))
                {
                    targetResolverDict[targetID] = (
                        originalPitch: Aimbot._currentTarget._entity.basicInfo.Current.ViewPitch,
                        isFakeApplied: false,
                        lastActualPitch: Aimbot._currentTarget._entity.basicInfo.Current.ViewPitch
                    );
                }

                var (originalPitch, isFakeApplied, lastActualPitch) = targetResolverDict[targetID];

                if (!isFakeApplied)
                {
                    lastActualPitch = Aimbot._currentTarget._entity.basicInfo.Current.ViewPitch;
                }

                if (isKeyDown)
                {
                    if (!isFakeApplied)
                    {
                        originalPitch = Aimbot._currentTarget._entity.basicInfo.Current.ViewPitch;
                        isFakeApplied = true;
                    }

                    Aimbot._currentTarget._entity.basicInfo.Current.ViewPitch = -originalPitch;
                }
                else if (isKeyUp && isFakeApplied)
                {
                    Aimbot._currentTarget._entity.basicInfo.Current.ViewPitch = lastActualPitch;
                    isFakeApplied = false;
                }
                else if (!isKeyDown && isFakeApplied)
                {
                    Aimbot._currentTarget._entity.basicInfo.Current.ViewPitch = -originalPitch;
                }

                targetResolverDict[targetID] = (originalPitch, isFakeApplied, lastActualPitch);
            }
        }
    }
}
