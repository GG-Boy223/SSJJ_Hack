using Assets.Sources.Modules.Player.HitBox;
using Assets.Sources.Utils.Weapon;
using share;
using Sharpen;
using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Utilities;
using SSJJBase.String;
using SSJJMath;
using System.Collections.Generic;
using UnityEngine;
using weapon.utils;
using Vector3 = UnityEngine.Vector3;

public static class Silentbot
{
    private static readonly List<int> BoneHashes = new List<int>();
    private static readonly string[] BoneNames =   
    {
        "Bip01_Head",
        "Bip01_Neck",
        "Bip01_R_Forearm",
        "Bip01_L_Forearm",
        "Bip01_R_Hand",
        "Bip01_L_Hand",
        "Bip01_Pelvis",
        "Bip01_R_Calf",
        "Bip01_L_Calf"
    };

    private static bool checkAllbones;
    private static int _currentTargetId;

    static Silentbot()
    {
        InitializeBones();
    }

    private static void InitializeBones()
    {
        foreach (string boneName in BoneNames)
        {
            BoneHashes.Add(new IgnoreCaseString(boneName).GetHashCode());
        }
    }

    public static Vector3 CalculateAimAngle(Vector3 startPos, Vector3 targetPos)
    {
        Vector3 direction = (targetPos - startPos).normalized;
        Vector3 spreadOffset = CalculateSpreadOffset(direction);
        Vector3 finalDirection = (direction + spreadOffset).normalized;

        float yaw = Mathf.Atan2(finalDirection.z, finalDirection.x) * Mathf.Rad2Deg - 90f;
        float pitch = Mathf.Asin(finalDirection.y / finalDirection.magnitude) * Mathf.Rad2Deg;

        if (yaw < -180f) yaw += 360f;
        if (yaw > 180f) yaw -= 360f;

        yaw = Mathf.Clamp(yaw, -180f, 180f);
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        return new Vector3(pitch, yaw, 0f);
    }

    private static float ClampValue(float value, float min, float max)
    {
        return value > min ? (value < max ? value : max) : min;
    }

    public static Vector3 ClampAngles(Vector3 angles)
    {
        angles.x = ClampValue(angles.x, -89f, 89f);
        angles.y = ClampValue(angles.y, -180f, 180f);
        angles.z = 0f;
        return angles;
    }

    public static Vector3 NormalizeAngles(Vector3 angles)
    {
        if (angles.x > 89f) angles.x -= 180f;
        if (angles.x < -89f) angles.x += 180f;

        angles.y %= 360f;
        if (angles.y > 180f) angles.y -= 360f;

        return angles;
    }

    private static bool CanAim(PlayerInfo shooter, PlayerInfo target, Vector3 startPos, Vector3 endPos)
    {
        Vector3 direction = VectorCoordConverter.UnityToSsjj((endPos - startPos).normalized);
        return FireUtility.BulletTrace(
            Contexts.sharedInstance.battleRoom.pyEngine.PyEngine,
            shooter._entity,
            Contexts.sharedInstance.player,
            100000f,
            new Vector3D(direction.x, direction.y, direction.z),
            new float[3],
            new float[3],
            false
        ).EntityId == target.Id;
    }

    public static Vector3 FixPosition(PlayerInfo shooter, PlayerInfo target, Vector3 shooterPos, Vector3 targetPos)
    {
        float frameInterval = Contexts.sharedInstance.userCommand.commands.CommandToSendList.Last.Value.FrameInterval * 0.001f;

        Vector3 predictedShooterPos = shooterPos + VectorCoordConverter.SsjjToUnity(shooter.Move.Velocity) * frameInterval;
        Vector3 predictedTargetPos = targetPos + VectorCoordConverter.SsjjToUnity(target.Move.Velocity) * frameInterval;

        Vector3 aimAngle = CalculateAimAngle(predictedShooterPos, predictedTargetPos);
        aimAngle = NormalizeAngles(aimAngle);
        aimAngle = ClampAngles(aimAngle);

        ApplyNoRecoil(shooter, ref aimAngle);

        aimAngle = NormalizeAngles(aimAngle);
        return ClampAngles(aimAngle);
    }

    public static void ApplyNoRecoil(PlayerInfo player, ref Vector3 angles)
    {
        if (player != null)
        {
            angles.x -= 2f * player.Punch.x;
            angles.y -= 2f * player.Punch.y;
        }
    }
    private static Vector3 CalculateSpreadOffset(Vector3 baseDirection)
    {
        int seed = Contexts.sharedInstance.userCommand.commands.CommandToSendList.Last.Value.Seq;

        var weapon = Contexts.sharedInstance.weapon.currentWeaponEntity;
        double spreadFactor = FireUtility.CalShotsFiredSpread(
            weapon.basicInfo.Data.ShotsFiredSpreadMin,
            weapon.basicInfo.Data.ShotsFiredSpreadMax,
            weapon.basicInfo.Data.ShotsFiredSpreadTime,
            weapon.basicInfo.Data.ShotsFired,
            weapon.basicInfo.Info.AttackInterval
        );

        double randX = UniformRandom.RandomFloat(seed, -0.5, 0.5) +
                      UniformRandom.RandomFloat(seed + 1, -0.5, 0.5);

        double randY = UniformRandom.RandomFloat(seed + 2, -0.5, 0.5) +
                      UniformRandom.RandomFloat(seed + 3, -0.5, 0.5);

        float spread = Contexts.sharedInstance.weapon.currentWeaponEntity.spread.Spread;
        float spreadScaleY = Contexts.sharedInstance.weapon.currentWeaponEntity.spread.SpreadScaleY;

        double spreadX = spread * randX * spreadFactor;
        double spreadY = spread * randY * spreadFactor * spreadScaleY;

        Vector3 right = Vector3.Cross(baseDirection, Vector3.up).normalized;
        Vector3 up = Vector3.Cross(right, baseDirection).normalized;

        return right * (float)spreadX + up * (float)spreadY;
    }

    public static bool SilentAimbot(List<PlayerInfo> players, PlayerInfo localPlayer, ref float yaw, ref float pitch)
    {
        if (!Config.Silentbot) return false;

        int targetId = 0;
        float minDistance = float.MaxValue;
        Transform bestBone = null;
        PlayerInfo bestTarget = null;

        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        Vector3 localEyePos = VectorCoordConverter.SsjjToUnity(
            localPlayer.Position +
            new Vector3(0, 0, (float)localPlayer.Move.PyPlayerMove.GetViewHeight())
        );

        foreach (PlayerInfo target in players)
        {
            if (target == null ||
                target == localPlayer ||
                !target._entity.hasHitBox ||
                !target._entity.hasThirdPersonUnityObjects ||
                target.Team == localPlayer.Team ||
                target.HpPercent < 0.0 ||
                target.IsDead ||
                target.State.GetPlayerStateType(1))
            {
                continue;
            }

            if (target._entity.hitBox.HitBoxBrushDirty)
            {
                PlayerHitBoxBrushUtility.UpdatePlayerAllHitBoxBrush(target._entity);
            }

            foreach (int boneHash in BoneHashes)
            {
                if (!target._entity.hitBox.BonetTransform.TryGetValue(boneHash, out Transform boneTransform))
                    continue;

                if (Camera.main == null) continue;

                Vector3 screenPos = ViewportUtility.WorldPointToScreenPoint(boneTransform.position);
                if (screenPos.z <= 0.01f) continue;

                Vector2 screenPoint = new Vector2(screenPos.x, screenPos.y);
                float distance = Vector2.Distance(screenCenter, screenPoint);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestBone = boneTransform;
                    bestTarget = target;
                    targetId = target.Id;
                }

                if (CanAim(localPlayer, target, localEyePos, boneTransform.position))
                {
                    Vector3 aimAngle = FixPosition(localPlayer, target, localEyePos, boneTransform.position);
                    yaw = aimAngle.y;
                    pitch = aimAngle.x;
                    return true;
                }

                if (!checkAllbones)
                {
                    break;
                }
            }
        }

        _currentTargetId = targetId;

        if (Config.AntiAim && bestTarget != null && CanAim(localPlayer, bestTarget, localEyePos, bestBone.position))
        {
            Vector3 aimAngle = FixPosition(localPlayer, bestTarget, localEyePos, bestBone.position);
            yaw = aimAngle.y;
            pitch = aimAngle.x;
            return true;
        }

        return false;
    }
}