using Assets.Sources.Components.Snapshot;
using Assets.Sources.Components.UserComand;
using Assets.Sources.Snapshots;
using Assets.Sources.Systems.UserCommand;
using SSJJNetworking.BattleServer;
using SSJJUserCmd;
using System.Runtime.CompilerServices;

internal static class hook
{
    public sealed class Class87
    {
        // SendUserCommandSystem.SendUserCommands
        public static void 加速Hook(
            SendUserCommandSystem sendUserCommandSystem_0,
            CommandsComponent commandsComponent_0,
            SnapshotsComponent snapshotsComponent_0)
        {
            if (加速.加速开关 && commandsComponent_0.CommandToSendList.Count != 0)
            {
                try
                {
                    IBattleServer server =
                        Contexts.sharedInstance.battleServer.battleServer.Server;
                    if (server.State != BattleServerState.Connected)
                    {
                        Class87.smethod_1(
                            sendUserCommandSystem_0,
                            commandsComponent_0,
                            snapshotsComponent_0);
                        return;
                    }

                    foreach (UserCmd commandToSend in commandsComponent_0.CommandToSendList)
                    {
                        if (commandToSend.Seq > 加速.int_2)
                        {
                            byte[] array = 加速.smethod_2(
                                commandToSend,
                                snapshotsComponent_0,
                                out int _);
                            server.SendTcpData(31, array);
                            加速.int_2 = commandToSend.Seq;
                            NetByteFactory.Instance.AddNormal(array);
                        }
                    }

                    return;
                }
                catch
                {
                    Class87.smethod_1(
                        sendUserCommandSystem_0,
                        commandsComponent_0,
                        snapshotsComponent_0);
                    return;
                }
            }

            Class87.smethod_1(
                sendUserCommandSystem_0,
                commandsComponent_0,
                snapshotsComponent_0);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void smethod_1(
            SendUserCommandSystem sendUserCommandSystem_0,
            CommandsComponent commandsComponent_0,
            SnapshotsComponent snapshotsComponent_0)
        {
        }
    }
}
