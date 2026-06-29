using Assets.Sources.Components.Interface.Info.Weapon;
using Assets.Sources.Utils.Player;
using Assets.Sources.Utils.Weapon;
using data;
using physics;
using SkyDome.Cfg;
using SkyDome.Entity;
using SSJJUserCmd;
using System.Linq;
using UnityEngine;

public static class AntiAim
{
    private const float DegreesToRadians = Mathf.PI / 180f;
    private static float WeaponSpread;

    public static float SharedYaw { get; private set; }
    public static float SharedPitch { get; private set; }
    public static bool IsSilentAiming { get; private set; }
    public static void SetPitchAngle(ref float pitch)
    {
        pitch = Config.AntiAim_PitchAngle;
    }

    public static void SetYawAngle()
    {
        if (Input.GetKeyDown(KeyCode.Z)) Config.AntiAim_Yaw = -90f;
        if (Input.GetKeyDown(KeyCode.X)) Config.AntiAim_Yaw = -180f;
        if (Input.GetKeyDown(KeyCode.C)) Config.AntiAim_Yaw = 90f;
    }

    public static void ExecuteAntiAim(ref float pitch, UserCmd userCmd, ref float _pitch, ref float _yaw, ref float _moveforward, ref float _moveright, ref int _buttons, ref bool _silenting)
    {
        if (PlayerUpdate.LocalEntity == null ||
            PlayerUpdate.LocalEntity._entity == null ||
            PlayerUpdate.EntityList == null ||
            Contexts.sharedInstance == null ||
            Contexts.sharedInstance.weapon == null)
        {
            _pitch = userCmd.CameraPitch / 100f;
            _yaw = userCmd.CameraYaw / 100f;
            _moveforward = userCmd.MoveForward;
            _moveright = userCmd.MoveRight;
            _buttons = userCmd.Buttons;
            _silenting = false;
            return;
        }

        SetYawAngle();
        float YawAngle = 0f;
        if (Config.AntiAim_Mode == 2)
        {
            YawAngle = Random.Range(Config.AntiAim_Jitter1, Config.AntiAim_Jitter2);
        }
        float CamYaw = userCmd.CameraYaw / 100f;
        float FixedYaw = (180f + CamYaw - Config.AntiAim_Yaw + YawAngle) % 360f - 180f;
        float CamPitch;
        if (pitch != 0f)
        {
            CamPitch = pitch;
        }
        else
        {
            CamPitch = userCmd.CameraPitch / 100f;
        }

        SetPitchAngle(ref CamPitch);
        if (Config.AntiAim_Mode == 1)
        {
            float YawAddValue = (180f + CamYaw + 180f + (userCmd.Seq * Config.AntiAim_SpinFactor % 360)) % 360f;
            FixedYaw = YawAddValue - 180f;
        }

        float moveforward = userCmd.MoveForward;
        float moveright = userCmd.MoveRight;
        int buttons = userCmd.Buttons;
        bool CanShoot = false;
        bool isweaponnotnull = Contexts.sharedInstance.weapon.currentWeaponEntity != null;

        WeaponSpread = CalculateWeaponSpread(userCmd);

        if (isweaponnotnull)
        {
            bool _canshoot;
            if (WeaponUtility.CanAttack(Contexts.sharedInstance.weapon.currentWeaponEntity, PlayerUpdate.LocalEntity.CilentTime + userCmd.FrameInterval))
            {
                _canshoot = WeaponSpread >= (Config.Accurary / 100f);
            }
            else
            {
                _canshoot = false;
            }
            CanShoot = _canshoot;
        }

        bool silenting = false;
        bool canSilentAim;
        if (CanShoot)
        {
            canSilentAim = Silentbot.SilentAimbot(PlayerUpdate.EntityList, PlayerUpdate.LocalEntity, ref _yaw, ref _pitch);
        }
        else
        {
            canSilentAim = false;
        }

        if (canSilentAim)
        {
            var currentWeapon = Contexts.sharedInstance.weapon.currentWeaponEntity;
            if (currentWeapon != null && currentWeapon.hasClip && currentWeapon.clip != null)
            {
                if (!userCmd.IsAttackOn)
                {
                    if (currentWeapon.clip.Clip > 0)
                    {
                        userCmd.Buttons |= 64;
                        buttons = buttons | 64;
                    }
                    if (currentWeapon.clip.Clip2 > 0)
                    {
                        userCmd.Buttons |= 512;
                        buttons = buttons | 512;
                    }
                }
            }

            FixedYaw = _yaw;
            CamPitch = _pitch;
            silenting = true;
        }

        FixMove(FixedYaw, CamYaw, ref moveforward, ref moveright);

        bool legit;
        if (PlayerUpdate.LocalEntity != null && !PlayerUpdate.LocalEntity.IsDead && (Config.AntiAim || silenting) && (silenting || !CanShoot || (!userCmd.IsAttackOn && !userCmd.IsSecondaryAttackOn)))
        {
            legit = false;
        }
        else
        {
            legit = true;
        }

        if (legit)
        {
            FixedYaw = CamYaw;
            CamPitch = userCmd.CameraPitch / 100f;
            moveforward = userCmd.MoveForward;
            moveright = userCmd.MoveRight;
        }

        SharedYaw = FixedYaw;
        SharedPitch = CamPitch;
        _pitch = CamPitch;
        _yaw = FixedYaw;
        _buttons = buttons;
        _moveforward = moveforward;
        _moveright = moveright;
        _silenting = silenting;
        IsSilentAiming = silenting;
    }


    private static void FixMove(
        float targetYaw,
        float originalYaw,
        ref float forwardMove,
        ref float rightMove)
    {
        float normalizedOriginalYaw = originalYaw >= 0f ? originalYaw : originalYaw + 360f;
        float normalizedTargetYaw = targetYaw >= 0f ? targetYaw : targetYaw + 360f;

        float angleDifference = CalculateAngleDifference(normalizedTargetYaw, normalizedOriginalYaw);
        float correctedAngle = 360f - angleDifference;

        float originalForward = forwardMove;
        float originalRight = rightMove;

        float cosAngle = Mathf.Cos(correctedAngle * DegreesToRadians);
        float sinAngle = Mathf.Sin(correctedAngle * DegreesToRadians);
        float cosAngle90 = Mathf.Cos((correctedAngle + 90f) * DegreesToRadians);
        float sinAngle90 = Mathf.Sin((correctedAngle + 90f) * DegreesToRadians);

        forwardMove = cosAngle * originalForward + cosAngle90 * originalRight;
        rightMove = sinAngle * originalForward + sinAngle90 * originalRight;

        forwardMove = Mathf.Clamp(forwardMove, -100f, 100f);
        rightMove = Mathf.Clamp(rightMove, -100f, 100f);
    }

    private static float CalculateAngleDifference(float angle1, float angle2)
    {
        return angle1 >= angle2
            ? 360f - Mathf.Abs(angle1 - angle2)
            : Mathf.Abs(angle1 - angle2);
    }
    public static float CalculateWeaponSpread(UserCmd userCommand)
    {
        bool isContextInvalid =
            Contexts.sharedInstance == null ||
            Contexts.sharedInstance.weapon == null ||
            Contexts.sharedInstance.battleRoom == null ||
            Contexts.sharedInstance.weapon.currentWeaponEntity == null ||
            Contexts.sharedInstance.player == null;

        if (isContextInvalid)
            return 0f;

        var weaponEntity = Contexts.sharedInstance.weapon.currentWeaponEntity;
        if (weaponEntity.basicInfo == null || weaponEntity.basicInfo.Info == null)
            return 0f;

        if (!weaponEntity.hasSpread || weaponEntity.spread == null ||
            !weaponEntity.hasAccuracy || weaponEntity.accuracy == null)
            return 0f;

        if (PlayerUpdate.LocalEntity == null || PlayerUpdate.LocalEntity._entity == null)
            return 0f;

        IEntitsWeaponInfo weaponInfo = weaponEntity.basicInfo.Info;

        if (Contexts.sharedInstance.battleRoom.pyEngine == null)
            return 0f;

        IPyEngine physicsEngine = Contexts.sharedInstance.battleRoom.pyEngine.PyEngine;
        PlayerEntity player = PlayerUpdate.LocalEntity._entity;
        WeaponEntity currentWeapon = Contexts.sharedInstance.weapon.currentWeaponEntity;

        if (physicsEngine == null)
            return 0f;

        if (!player.hasClientTime || player.clientTime == null)
            return 0f;

        bool isWeightless = (physicsEngine.GetWorld().GetSceneMoveData() as SceneMoveData)?.isWeightlessness ?? false;

        bool shouldProcessSpread =
            !userCommand.PredicatedOnce &&
            weaponInfo.AccuracyLogic != null &&
            weaponInfo.SpreadLogic != null;

        if (shouldProcessSpread)
        {
            weaponInfo.SpreadLogic.BeforeFire(out currentWeapon.spread.Spread, player, currentWeapon, userCommand, isWeightless);
            weaponInfo.AccuracyLogic.BeforeFire(userCommand.Seq, player, currentWeapon, player.clientTime.ClientTime);
        }

        float baseSpread;
        float spreadModifier = currentWeapon.spread.Spread;

        switch (weaponInfo.WeaponType)
        {
            case 0:
                baseSpread = currentWeapon.accuracy.Accuracy * 100f / 92f;
                break;

            case 1:
            case 6:
            case 14:
                baseSpread = 1f - (currentWeapon.accuracy.Accuracy - weaponInfo.DefaultAccuracy) * 100f
                             / ((weaponInfo.MaxInaccuracy - weaponInfo.DefaultAccuracy) * 100f);
                spreadModifier = currentWeapon.spread.Spread;
                break;

            case 5:
                baseSpread = 1f;
                float playerSpeed = PlayerUtility.PlayerLength2D(player);
                spreadModifier = playerSpeed > 350f ? 0.4f : (playerSpeed > 25f ? 0.7f : 0f);
                break;

            case 10:
            case 12:
                baseSpread = 1f - (currentWeapon.accuracy.Accuracy - weaponInfo.AccuracyOffset) * 100f
                             / ((weaponInfo.MaxInaccuracy - weaponInfo.AccuracyOffset) * 100f);
                spreadModifier = currentWeapon.spread.Spread;
                break;

            default:
                baseSpread = 0f;
                spreadModifier = currentWeapon.spread.Spread;
                break;
        }

        float spreadDelta = Mathf.Clamp(baseSpread - spreadModifier, 0f, 1f);
        return spreadDelta;
    }
}
