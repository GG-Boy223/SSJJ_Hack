using Assets.Sources.Components.Player.UnityObjects;
using Entitas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SkyDome.Entity
{
    public class PlayerUpdate : MonoBehaviour
    {
        public static List<PlayerInfo> EntityList;
        public static PlayerInfo LocalEntity;
        public static PlayerInfo CameraEntity;
        public static PlayerInfo PredictionEntity;
        public static Camera MainCamera;
        private void Update()
        {
            try
            {
                var EntityGroup = PlayerEntityUpdate();
                ResetPlayerTransformCache(EntityGroup);
                RetrievePlayerInfo(EntityGroup, out LocalEntity, out CameraEntity, out PredictionEntity, out EntityList);
                MainCamera = Camera.main;
            }
            catch(Exception ex)
            {
#if Debug_Log
                global::System.Console.WriteLine($"[玩家更新] 更新失败: {ex}");
#endif
            }
        }

        private void RetrievePlayerInfo(IGroup<PlayerEntity> playerEntities,
     out PlayerInfo localPlayer,
     out PlayerInfo cameraOwner,
     out PlayerInfo predictionTarget,
     out List<PlayerInfo> entityList)
        {
            localPlayer = null;
            cameraOwner = null;
            predictionTarget = null;
            entityList = new List<PlayerInfo>();

            if (playerEntities == null) return;

            foreach (var player in playerEntities)
            {
                bool isSpecialPlayer = false;

                if (player.isCameraOwner)
                {
                    cameraOwner = new PlayerInfo(player);
                    isSpecialPlayer = true;
                }
                if (player.isMyPlayer)
                {
                    localPlayer = new PlayerInfo(player);
                    isSpecialPlayer = true;
                }
                if (player.isPrediction)
                {
                    predictionTarget = new PlayerInfo(player);
                    isSpecialPlayer = true;
                }

                if (!isSpecialPlayer)
                {
                    entityList.Add(new PlayerInfo(player));
                }
            }
        }

        private void ResetPlayerTransformCache(IGroup<PlayerEntity> playerEntities)
        {
            Type componentType = typeof(ThirdPersonUnityObjectsComponent);
            FieldInfo cachedField = componentType.GetField("_playerCached",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            FieldInfo cacheField = componentType.GetField("_playerCache",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            foreach (PlayerEntity player in playerEntities)
            {
                if (player?.thirdPersonUnityObjects == null) continue;

                cachedField?.SetValue(player.thirdPersonUnityObjects, false);
                cacheField?.SetValue(player.thirdPersonUnityObjects, null);
            }
        }
        private IGroup<PlayerEntity> PlayerEntityUpdate()
        {
            return Contexts.sharedInstance.player.GetGroup(PlayerMatcher.AllOf(new IMatcher<PlayerEntity>[]
            {
                PlayerMatcher.BasicInfo,
                PlayerMatcher.ThirdPersonUnityObjects
            }));
        }
    }
}
