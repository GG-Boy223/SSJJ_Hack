using Assets.Sources.Components.Snapshot;
using Assets.Sources.Components.UserComand;
using SkyDome.Cfg;
using SkyDome.Feature.Backtrack;
using SSJJBase.Utility;
using SSJJUserCmd;
using System;

internal static class 加速
{
    public static bool 加速开关
    {
        get => Config.CommandAccelerationEnabled;
        set => Config.CommandAccelerationEnabled = value;
    }

    public static int 加速级别
    {
        get => Math.Max(3, Config.CommandAccelerationLevel);
        set => Config.CommandAccelerationLevel = value;
    }

    public static bool bool_1 = true;

    public static int int_1;

    public static int int_2;

    public static readonly BinaryDataWriter binaryDataWriter_0 = new BinaryDataWriter();

    public static void smethod_0(CommandsComponent commandsComponent_0, UserCmd userCmd_0)
    {
        if (加速.bool_1)
        {
            加速.int_1 = commandsComponent_0.Counter;
            加速.bool_1 = false;
        }

        加速.int_1 += new Random().Next(2, 加速.加速级别);
        commandsComponent_0.Counter = 加速.int_1;
        userCmd_0.Seq = 加速.int_1;
    }

    public static void smethod_1()
    {
        加速.int_2 = 0;
        加速.bool_1 = true;
        加速.int_1 = 0;
    }

    public static byte[] smethod_2(
        UserCmd userCmd_0,
        SnapshotsComponent snapshotsComponent_0,
        out int int_3)
    {
        int_3 = 0;
        加速.binaryDataWriter_0.Reset();

        UserCmd preparedCommand = userCmd_0;
        float yaw = 0f;
        float pitch = 0f;
        float moveForward = 0f;
        float moveRight = 0f;
        int buttons = 0;
        bool antiAimApplied = false;
        HookManager.PrepareOutgoingCommand(
            ref preparedCommand,
            true,
            ref yaw,
            ref pitch,
            ref moveForward,
            ref moveRight,
            ref buttons,
            ref antiAimApplied);

        bool backtrackActive =
            BacktrackManager.Enabled &&
            BacktrackAimState.TargetEntityId != -1 &&
            BacktrackAimState.AgeMilliseconds > 0;

        int renderTime = Contexts.sharedInstance.time.time.RenderTime;
        if (backtrackActive)
        {
            renderTime -= BacktrackAimState.AgeMilliseconds;
            preparedCommand.PredicatedOnce = false;
        }

        short cameraYaw = (short)(yaw * 100f);
        short cameraPitch = (short)(pitch * 100f);
        bool useFullCommandFields =
            antiAimApplied ||
            Config.AntiAim ||
            backtrackActive ||
            Config.AirStrafe ||
            Config.AirStrafe_guoji;

        加速.binaryDataWriter_0.WriteByte(0);
        加速.binaryDataWriter_0.WriteByte(0);
        加速.binaryDataWriter_0.WriteInt(preparedCommand.Seq);
        加速.binaryDataWriter_0.WriteInt(renderTime);
        加速.binaryDataWriter_0.WriteInt(snapshotsComponent_0.LatestSnapshotSeqId);

        // 原加速尾部仍用于生成额外命令；静默、反自瞄或回溯实际参与时，
        // 首条命令必须使用当前协议的完整字段掩码，否则服务器会忽略角度等字段。
        加速.binaryDataWriter_0.WriteByte(useFullCommandFields ? (byte)0xBF : (byte)4);
        加速.binaryDataWriter_0.WriteByte(
            useFullCommandFields ? (byte)preparedCommand.FrameInterval : (byte)0);
        加速.binaryDataWriter_0.WriteByte((byte)moveForward);
        加速.binaryDataWriter_0.WriteByte((byte)moveRight);
        加速.binaryDataWriter_0.WriteInt(buttons);

        int bagShift = useFullCommandFields ? 4 : 5;
        int num2 = (preparedCommand.BagId << bagShift) | preparedCommand.Weapon;
        加速.binaryDataWriter_0.WriteByte((byte)num2);
        加速.binaryDataWriter_0.WriteShort(cameraYaw);
        加速.binaryDataWriter_0.WriteShort(cameraPitch);

        if (backtrackActive)
        {
            加速.binaryDataWriter_0.WriteShort(4608);
            加速.binaryDataWriter_0.WriteInt(4608);
            加速.binaryDataWriter_0.WriteInt(536870913);
            加速.binaryDataWriter_0.WriteShort(short.MinValue);
        }
        else
        {
            加速.binaryDataWriter_0.WriteShort(4508);
            加速.binaryDataWriter_0.WriteInt(4452);
            加速.binaryDataWriter_0.WriteShort(cameraYaw);
            加速.binaryDataWriter_0.WriteInt(0);
        }

        byte[] bytes = 加速.binaryDataWriter_0.GetBytes();
        加速.binaryDataWriter_0.SetBytes(bytes);
        int_3 = bytes.Length;
        return bytes;
    }
}
