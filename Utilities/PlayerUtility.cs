using Assets.Sources.Utils.Weapon;
using share;
using SkyDome.Entity;
using SSJJBase.String;
using SSJJMath;
using SSJJPhysics;
using System;
using System.Collections.Generic;
using UnityEngine;
using weapon;

namespace SkyDome.Utilities
{
    public enum ShotTraceState
    {
        Invalid,
        Clear,
        HitWorld,
        HitPlayer,
        HitOtherEntity
    }

    public struct ShotTraceInfo
    {
        public ShotTraceState State;
        public int EntityId;
        public float Fraction;
    }

    public static class PlayerUtility
    {
        private static readonly float[] SharedTraceMins = new float[3];
        private static readonly float[] SharedTraceMaxs = new float[3];

        public static Dictionary<IgnoreCaseString, Transform> GetPlayerAllTransform(this PlayerInfo player)
        {
            return player.ThirdPersonUnityObjects?.AllPlayerTransforms;
        }
        public static Transform GetPlayerTransform(this PlayerInfo player, string name)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Transform name cannot be null or empty", nameof(name));

            var transforms = player.ThirdPersonUnityObjects?.AllPlayerTransforms;
            if (transforms == null || transforms.Count == 0)
                return null;

            IgnoreCaseString targetBone = name;

            foreach (var kvp in transforms)
            {
                if (kvp.Key.Equals(targetBone))
                {
                    return kvp.Value;
                }
            }

            return null;
        }
        public static Transform GetPlayerTransform(this PlayerEntity player, string name)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Transform name cannot be null or empty", nameof(name));

            var transforms = player.thirdPersonUnityObjects?.AllPlayerTransforms;
            if (transforms == null || transforms.Count == 0)
                return null;

            IgnoreCaseString targetBone = name;

            foreach (var kvp in transforms)
            {
                if (kvp.Key.Equals(targetBone))
                {
                    return kvp.Value;
                }
            }

            return null;
        }
        public static Transform GetValidHeadNub(this PlayerInfo player)
        {
            Transform headNub = player.GetPlayerTransform("Bip01_HeadNub");
            if (headNub != null)
                return headNub;

            Transform head = player.GetPlayerTransform("Bip01_Head");
            if (head == null)
                return null; 

            if (head.childCount == 0)
            {
                var fakeHeadNub = new GameObject("fake_HeadNub")
                {
                    transform =
            {
                parent = head,
                localPosition = new Vector3(-21.7f, 0f, 0f)
            }
                };
                return fakeHeadNub.transform;
            }
            return head.GetChild(0);
        }
        public static float Length(this Vector3 vector)
        {
            return Mathf.Sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
        }


        public static ShotTraceInfo GetCurrentShotTraceInfo(bool useSpreadPrediction = true, float attackDistance = 100000f)
        {
            PlayerEntity localEntity = PlayerUpdate.LocalEntity?._entity;
            PlayerContext playerContext = Contexts.sharedInstance?.player;

            if (localEntity == null || playerContext == null)
            {
                return default;
            }

            if (!TryGetCurrentShotDirection(useSpreadPrediction, out Vector3D shotDirection))
            {
                return default;
            }

            return TraceDirection(localEntity, playerContext, shotDirection, attackDistance);
        }

        public static ShotTraceInfo GetShotTraceInfoToTarget(PlayerInfo shooter, PlayerInfo target, Vector3 startPos, Vector3 endPos, float attackDistance = 100000f)
        {
            if (shooter == null || shooter._entity == null || target == null)
            {
                return default;
            }

            Vector3 direction = endPos - startPos;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return default;
            }

            Vector3 convertedDirection = VectorCoordConverter.UnityToSsjj(direction.normalized);
            return TraceDirection(
                shooter._entity,
                Contexts.sharedInstance?.player,
                new Vector3D(convertedDirection.x, convertedDirection.y, convertedDirection.z),
                attackDistance);
        }

        public static bool TryGetCurrentShotDirection(bool useSpreadPrediction, out Vector3D shotDirection)
        {
            shotDirection = default;

            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            Vector3 cameraForward = VectorCoordConverter.UnityToSsjj(camera.transform.forward);
            shotDirection = new Vector3D(cameraForward.x, cameraForward.y, cameraForward.z);

            if (!useSpreadPrediction)
            {
                return true;
            }

            Contexts contexts = Contexts.sharedInstance;
            PlayerInfo localPlayer = PlayerUpdate.LocalEntity;
            if (contexts == null ||
                localPlayer == null ||
                contexts.weapon == null ||
                contexts.userCommand == null ||
                contexts.userCommand.commands == null ||
                contexts.userCommand.commands.CommandToSendList == null ||
                contexts.userCommand.commands.CommandToSendList.Last == null)
            {
                return true;
            }

            WeaponEntity currentWeapon = contexts.weapon.currentWeaponEntity;
            if (currentWeapon == null)
            {
                return true;
            }

            var weaponBasicInfo = currentWeapon.basicInfo;
            if (weaponBasicInfo == null || weaponBasicInfo.Info == null || weaponBasicInfo.Data == null)
            {
                return true;
            }

            float yaw = localPlayer.ViewPos.y + 2f * localPlayer.Punch.y;
            float pitch = localPlayer.ViewPos.x + 2f * localPlayer.Punch.x;
            int seed = contexts.userCommand.commands.CommandToSendList.Last.Value.Seq + 1;
            float spread = currentWeapon.spread.Spread;
            float spreadScaleY = currentWeapon.spread.SpreadScaleY;
            double shotsFiredSpread = FireUtility.CalShotsFiredSpread(
                weaponBasicInfo.Data.ShotsFiredSpreadMin,
                weaponBasicInfo.Data.ShotsFiredSpreadMax,
                weaponBasicInfo.Data.ShotsFiredSpreadTime,
                weaponBasicInfo.Data.ShotsFired,
                weaponBasicInfo.Info.AttackInterval);

            shotDirection = ShootingDirUtils.CalculateShotingDir(seed, yaw, pitch, spread, spreadScaleY, shotsFiredSpread);
            return true;
        }

        public static bool CanAim(PlayerInfo shooter, PlayerInfo target, Vector3 startPos, Vector3 endPos)
        {
            if (target == null)
            {
                return false;
            }

            return GetShotTraceInfoToTarget(shooter, target, startPos, endPos).EntityId == target.Id;
        }

        private static ShotTraceInfo TraceDirection(PlayerEntity shooter, PlayerContext otherPlayers, Vector3D shotDirection, float attackDistance)
        {
            Contexts contexts = Contexts.sharedInstance;
            if (contexts == null ||
                contexts.battleRoom == null ||
                contexts.battleRoom.pyEngine == null ||
                contexts.battleRoom.pyEngine.PyEngine == null ||
                shooter == null ||
                otherPlayers == null)
            {
                return default;
            }

            TraceResult trace = FireUtility.BulletTrace(
                contexts.battleRoom.pyEngine.PyEngine,
                shooter,
                otherPlayers,
                attackDistance,
                shotDirection,
                SharedTraceMins,
                SharedTraceMaxs,
                false);

            return BuildShotTraceInfo(trace, otherPlayers);
        }

        private static ShotTraceInfo BuildShotTraceInfo(TraceResult trace, PlayerContext otherPlayers)
        {
            if (trace.EntityId > 0)
            {
                return new ShotTraceInfo
                {
                    State = otherPlayers.GetEntityWithEntityId(trace.EntityId) != null
                        ? ShotTraceState.HitPlayer
                        : ShotTraceState.HitOtherEntity,
                    EntityId = trace.EntityId,
                    Fraction = trace.Fraction
                };
            }

            return new ShotTraceInfo
            {
                State = trace.Fraction < 0.999f ? ShotTraceState.HitWorld : ShotTraceState.Clear,
                EntityId = -1,
                Fraction = trace.Fraction
            };
        }
    }
}
