using Assets.Scripts.QuickRuntimeConsole;
using Assets.Sources.Components.Camera;
using Assets.Sources.Components.Interface.Info.Camera;
using Assets.Sources.Components.Snapshot;
using Assets.Sources.Components.UserComand;
using Assets.Sources.Config;
using Assets.Sources.Info.Camera.CameraLogic;
using Assets.Sources.Modules.Player.Orientation;
using Assets.Sources.Modules.Ui.UiEventCondition;
using Assets.Sources.Modules.WorldCamera;
using Assets.Sources.Networking.Server;
using Assets.Sources.Snapshots;
using Assets.Sources.Systems.PacketHandle.Handlers;
using Assets.Sources.Systems.UserCommand;
using Assets.Sources.Ui.Model.Common;
using Assets.Sources.Ui.ViewModel.Common;
using Assets.Sources.Utils;
using Assets.Sources.Utils.Playback;
using config;
using I2.Loc;
using MonoHook;
using NetData;
using physics;
using share;
using SkyDome;
using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Extension;
using SkyDome.Feature;
using SkyDome.Feature.Legit;
using SkyDome.Features;
using SkyDome.Utilities;
using SSJJBase.Singleton;
using SSJJBase.Utility;
using SSJJMath;
using SSJJNetworking.Packet;
using SSJJUserCmd;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using zlib;
using Vector3 = UnityEngine.Vector3;

public class HookManager
{
    private static readonly List<MethodHook> s_monoHooks = new List<MethodHook>();
    private static readonly BindingFlags s_bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    //缓存拦截的UDP包
    public static List<UdpPacket> chokedPackets = new List<UdpPacket>();
    private static bool isSendingChoked = false;

    // 代理方法类 - 存放所有会被MonoHook修改的方法
    public static class OriginalProxies
    {
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void ScreenNow_Original(AbstractCaptureSnapshot self)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void ExclusiveUpdateScreen_Original(ExclusiveCaptureSnapshot self)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void WindowUpdateScreen_Original(WindowCaptureSnapshot self)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void WindowHdcUpdateScreen_Original(WindowHdcCaptureSnapshot self)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static IEnumerator CaptureCamera_Original(CaptureCameraManager self)
        {
            yield break;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void Send_Original(NetEaseCloudManager instance, byte[] bytes, int methodId)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void SetStartupLanguage_Original()
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static int GetPlayerSpeed_Original(IPyPlayerMove playerMove, IPyUserCmd userCmd)
        {
            return 0;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void InterceptNew_Original(PostProcessUserCommandSystem commandSystem, UserCmd userCommand)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void OnAfterPredication_Original(CameraLogicToTransformSystem cameraSystem)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static float GetCurrentCmdYaw_Original(ICameraLogic cameraLogic)
        {
            return 0f;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static float GetCurrentCmdPitch_Original(ICameraLogic cameraLogic)
        {
            return 0f;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static bool IsActive_Original(TpsCameraLogic cameraLogic)
        {
            return false;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void TpsCameraUpdate_Original(TpsCameraLogic cameraLogic)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static short LastCameraYaw_Original(CommandsComponent commandsComponent)
        {
            return 0;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static short LastCameraPitch_Original(CommandsComponent commandsComponent)
        {
            return 0;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void MakeCommand_Original(ComputeUserCommandSystem system, UserCmd command, PlayerEntity player)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void OnPlayback_Original(PlayerOrientationPlabackSystem system)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void OnPredicate_Original(PlayerOrientationPredicationSystem system, PlayerEntity player, IUserCmd command)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void PredictCmdOnCamera_Original(PlayerOrientationPredicationSystem system, PlayerEntity player, IUserCmd command)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static byte[] GetUserCmdBytes_Original(SendUserCommandSystem self, LinkedList<UserCmd> sendCmdList, SnapshotsComponent snapshots, out int datalen, bool isTcp)
        {
            //var firstCmd = sendCmdList.First.Value;
            //firstCmd.RenderTime = timeRestore[i];
            datalen = 0;
            return null;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void FpsDisplayUpdate_Original(FpsDisplay instance)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void SendUdpData_Original(BattleServer server, int methodId, byte[] data = null)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void HitPlayerHandler_Original(HitPlayerHandler self, GameServerSetupData data)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void FlashExplosionSetData_Original(FlashExplosionModel self, GrenadeExplosionEventEntityData ov, GrenadeExplosionEventEntityData data)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void PackChatMsg_Original(UnityProxyHandler handler, UnityProxyData data, string message)
        {
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void QuickRuntimeConsoleUpdate_Original(QuickRuntimeConsole console)
        {
        }
    }

    // 创建Hook方法
    public static bool CreateMonoHook(Type targetType, string originalMethodName, MethodInfo hookMethod, MethodInfo proxyMethod)
    {
        return CreateMonoHook(targetType, originalMethodName, null, hookMethod, proxyMethod);
    }

    public static bool CreateMonoHook(Type targetType, string originalMethodName, Type[] parameterTypes, MethodInfo hookMethod, MethodInfo proxyMethod)
    {
        if (targetType is null || string.IsNullOrEmpty(originalMethodName) || hookMethod is null || proxyMethod is null)
            return false;

        MethodInfo originalMethod = parameterTypes == null
            ? targetType.GetMethod(originalMethodName, s_bindingFlags)
            : targetType.GetMethod(originalMethodName, s_bindingFlags, null, parameterTypes, null);
        if (originalMethod is null)
            return false;

        try
        {
            var hook = new MethodHook(
                targetMethod: originalMethod,
                replacementMethod: hookMethod,
                proxyMethod: proxyMethod,
                data: $"{targetType.Name}.{originalMethodName}"
            );

            hook.Install();
            s_monoHooks.Add(hook);
            return true;
        }
        catch (Exception ex)
        {
            #if Debug_Log
            global::System.Console.WriteLine($"创建Hook失败 ({targetType.Name}.{originalMethodName}): {ex.Message}");
            #endif
            return false;
        }
    }

    // 移除所有Hook
    public static void RemoveAllHooks()
    {
        if (s_monoHooks is null)
            return;

        foreach (MethodHook hook in s_monoHooks)
        {
            try
            {
                if (hook.isHooked)
                {
                    hook.Uninstall();
                }
            }
            catch (Exception ex)
            {
                #if Debug_Log
                global::System.Console.WriteLine($"撤销钩子时出错: {ex.Message}");
                #endif
            }
        }

        s_monoHooks.Clear();
    }

    #region Hook初始化
    public static void StartHook()
    {
        // 反截图相关Hook
        CreateMonoHook(typeof(AbstractCaptureSnapshot), "ScreenNow",
            typeof(HookManager).GetMethod(nameof(ScreenNow_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.ScreenNow_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(ExclusiveCaptureSnapshot), "UpdateScreen",
            typeof(HookManager).GetMethod(nameof(UpdateScreen_Hook1), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.ExclusiveUpdateScreen_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(WindowCaptureSnapshot), "UpdateScreen",
            typeof(HookManager).GetMethod(nameof(UpdateScreen_Hook2), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.WindowUpdateScreen_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(WindowHdcCaptureSnapshot), "UpdateScreen",
            typeof(HookManager).GetMethod(nameof(UpdateScreen_Hook3), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.WindowHdcUpdateScreen_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(CaptureCameraManager), "CaptureCamera",
            typeof(HookManager).GetMethod(nameof(CaptureCamera_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.CaptureCamera_Original), s_bindingFlags)
        );

        // 截图上传Hook
        CreateMonoHook(typeof(NetEaseCloudManager), "Send",
            typeof(HookManager).GetMethod(nameof(Send_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.Send_Original), s_bindingFlags)
        );

        // 本地化相关Hook
        CreateMonoHook(typeof(LocalizationManager), "SelectStartupLanguage",
            typeof(HookManager).GetMethod(nameof(SetStartupLanguage_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.SetStartupLanguage_Original), s_bindingFlags)
        );

        // 玩家移动相关Hook
        CreateMonoHook(typeof(PlayerSpeedUtil), "GetPlayerSpeed",
            typeof(HookManager).GetMethod(nameof(GetPlayerSpeed_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.GetPlayerSpeed_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(PostProcessUserCommandSystem), "InterceptNew",
            typeof(HookManager).GetMethod(nameof(InterceptNew_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.InterceptNew_Original), s_bindingFlags)
        );

        // 相机逻辑相关Hook
        CreateMonoHook(typeof(CameraLogicToTransformSystem), "OnAfterPredication",
            typeof(HookManager).GetMethod(nameof(AfterPrediction_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.OnAfterPredication_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(CameraFunction), "GetCurrentCmdYaw",
            typeof(HookManager).GetMethod(nameof(GetCurrentCmdYaw_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.GetCurrentCmdYaw_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(CameraFunction), "GetCurrentCmdPitch",
            typeof(HookManager).GetMethod(nameof(GetCurrentCmdPitch_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.GetCurrentCmdPitch_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(TpsCameraLogic), "IsActive",
            typeof(HookManager).GetMethod(nameof(IsActive_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.IsActive_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(TpsCameraLogic), "Update",
            typeof(HookManager).GetMethod(nameof(TpsCameraUpdate_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.TpsCameraUpdate_Original), s_bindingFlags)
        );

        // 命令组件相关Hook
        CreateMonoHook(typeof(CommandsComponent), "LastCameraYaw",
            typeof(HookManager).GetMethod(nameof(LastCameraYaw_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.LastCameraYaw_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(CommandsComponent), "LastCameraPitch",
            typeof(HookManager).GetMethod(nameof(LastCameraPitch_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.LastCameraPitch_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(ComputeUserCommandSystem), "MakeCommand",
            typeof(HookManager).GetMethod(nameof(MakeCommand_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.MakeCommand_Original), s_bindingFlags)
        );

        // 玩家朝向相关Hook
        CreateMonoHook(typeof(PlayerOrientationPlabackSystem), "OnPlayback",
            typeof(HookManager).GetMethod(nameof(OnPlayback_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.OnPlayback_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(PlayerOrientationPredicationSystem), "OnPredicate",
            typeof(HookManager).GetMethod(nameof(OnPredicate_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.OnPredicate_Original), s_bindingFlags)
        );
        CreateMonoHook(typeof(PlayerOrientationPredicationSystem), "PredictCmdOnCamera",
            typeof(HookManager).GetMethod(nameof(PredictCameraCommand_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.PredictCmdOnCamera_Original), s_bindingFlags)
        );

        // 用户命令发送相关Hook
        CreateMonoHook(typeof(SendUserCommandSystem), "GetUserCmdBytes",
            typeof(HookManager).GetMethod(nameof(GetUserCmdBytes_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.GetUserCmdBytes_Original), s_bindingFlags)
        );

        // 帧率显示相关Hook
        CreateMonoHook(typeof(FpsDisplay), "Update",
            typeof(HookManager).GetMethod(nameof(FpsDisplay_Update_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.FpsDisplayUpdate_Original), s_bindingFlags)
        );

        // 假延迟相关Hook
        CreateMonoHook(typeof(BattleServer), "SendUdpData",
            typeof(HookManager).GetMethod(nameof(SendUdpData_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.SendUdpData_Original), s_bindingFlags)
        );

        // 击中反馈相关Hook
        CreateMonoHook(typeof(HitPlayerHandler), "Handle",
            typeof(HookManager).GetMethod(nameof(HitPlayerHandler_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.HitPlayerHandler_Original), s_bindingFlags)
        );

        // 闪光弹相关Hook
        CreateMonoHook(typeof(FlashExplosionModel), "SetData",
            typeof(HookManager).GetMethod(nameof(FlashExplosion_SetData_Hook), s_bindingFlags),
            typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.FlashExplosionSetData_Original), s_bindingFlags)
        );

        // 聊天消息相关Hook
        //CreateMonoHook(typeof(UnityProxyHandler), "PackChatMsg", typeof(HookManager).GetMethod(nameof(PackChatMsg_Hook), s_bindingFlags),typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.PackChatMsg_Original), s_bindingFlags));

        // 控制台相关Hook
        //CreateMonoHook(typeof(QuickRuntimeConsole), "Update",typeof(HookManager).GetMethod(nameof(QuickRuntimeConsole_Update_Hook), s_bindingFlags),typeof(OriginalProxies).GetMethod(nameof(OriginalProxies.QuickRuntimeConsoleUpdate_Original), s_bindingFlags));
    }
    #endregion

    #region 反截图相关Hook

    private static int _screenshotBlockCount = 0;

    // 基础截图
    public static void ScreenNow_Hook(AbstractCaptureSnapshot self)
    {
        _screenshotBlockCount++;
        #if Debug_Log
        global::System.Console.WriteLine($"[反截图] 拦截基础截图请求 (AbstractCaptureSnapshot) - 已拦截 {_screenshotBlockCount} 次");
        #endif
        return;
    }

    // 全屏模式截图
    public static void UpdateScreen_Hook1(ExclusiveCaptureSnapshot self)
    {
        _screenshotBlockCount++;
        #if Debug_Log
        global::System.Console.WriteLine($"[反截图] 拦截全屏模式截图 (ExclusiveCaptureSnapshot) - 已拦截 {_screenshotBlockCount} 次");
        #endif
        return;
    }

    // 窗口模式截图 (GDI)
    public static void UpdateScreen_Hook2(WindowCaptureSnapshot self)
    {
        _screenshotBlockCount++;
        #if Debug_Log
        global::System.Console.WriteLine($"[反截图] 拦截窗口GDI截图 (WindowCaptureSnapshot) - 已拦截 {_screenshotBlockCount} 次");
        #endif
        return;
    }

    // 底层窗口截图 (HDC/DllImport)
    public static void UpdateScreen_Hook3(WindowHdcCaptureSnapshot self)
    {
        _screenshotBlockCount++;
        #if Debug_Log
        global::System.Console.WriteLine($"[反截图] 拦截底层HDC截图 (WindowHdcCaptureSnapshot) - 已拦截 {_screenshotBlockCount} 次");
        #endif
        return;
    }

    // 防透视检测
    public static IEnumerator CaptureCamera_Hook(CaptureCameraManager self)
    {
        _screenshotBlockCount++;
        #if Debug_Log
        global::System.Console.WriteLine($"[反截图] 拦截透视染色检测 (CaptureCameraManager) - 已拦截 {_screenshotBlockCount} 次");
        #endif
        yield break;
    }
    #endregion

    #region 截图上传相关Hook
    public static void Send_Hook(NetEaseCloudManager instance, byte[] bytes, int methodId)
    {
        try
        {
            // 发送假截图数据
            SendFakeScreenshot(methodId);
        }
        catch (Exception ex)
        {
            #if Debug_Log
            global::System.Console.WriteLine($"[反截图] 发送假数据失败: {ex.Message}");
            #endif
        }
    }

    // 发送假的空白截图数据
    private static void SendFakeScreenshot(int methodId)
    {
        try
        {
            // 创建空白截图
            byte[] blankScreenshot = new byte[4194304];

            // 获取必要的配置数据
            var roomData = Contexts.sharedInstance?.battleRoom?.roomData?.Data;
            var gameBootConfig = TplManager.Instance?.GameBootConfig;

            if (roomData == null || gameBootConfig == null)
            {
                #if Debug_Log
                global::System.Console.WriteLine("[反截图] 无法获取配置数据，跳过发送");
                #endif
                return;
            }

            // 构建请求字符串
            string requestString = string.Concat(new object[]
            {
            "&platform=", gameBootConfig.Platform,
            "&serverId=", gameBootConfig.ServerId,
            "&uid=", gameBootConfig.UserId,
            "&charId=", gameBootConfig.CharId,
            "&ruleType=", 1,
            "&gamePlugFlag=", 1,
            "&raceType=", roomData.RaceType,
            "&sceneId=", roomData.SceneId
            });

            // 计算 MD5 哈希
            string md5Hash = Md5Utility.GetMD5HashFromFile(
                Encoding.Default.GetBytes(requestString + "adf35b91c956e63f7de79c5513f5823e")
            );

            // 构建数据包
            BinaryDataWriter writer = new BinaryDataWriter();
            WriteString(writer, gameBootConfig.ExternalUrl);
            WriteString(writer, requestString);
            WriteString(writer, md5Hash);
            writer.WriteByteArray(blankScreenshot, 0, blankScreenshot.Length);

            byte[] finalData = writer.GetBytes();

            // 压缩数据
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (ZOutputStream zOutputStream = new ZOutputStream(memoryStream, -1))
                {
                    zOutputStream.Write(finalData, 0, finalData.Length);
                    zOutputStream.finish();
                }

                // 发送到服务器
                Contexts.sharedInstance.battleServer.battleServer.Server.SendTcpMessage(
                    methodId,
                    new ImgData
                    {
                        Content = memoryStream.GetBuffer(),
                        Type = MonoBehaviourSingleton<ScreenShotManager>.Instance.ScreenImageReason
                    }
                );
            }

            #if Debug_Log

            global::System.Console.WriteLine($"[反截图] 已发送假数据 (MethodId: {methodId})");

            #endif
        }
        catch (Exception ex)
        {
            #if Debug_Log
            global::System.Console.WriteLine($"[反截图] SendFakeScreenshot 异常: {ex}");
            #endif
        }
    }

    // 写入字符串到 BinaryDataWriter
    private static void WriteString(BinaryDataWriter writer, string data)
    {
        writer.WriteShort((short)data.Length);
        writer.WriteUtf(data, 0);
    }
    #endregion

    #region 本地化相关Hook
    public delegate void SetStartupLanguageDelegate();

    public static void SetStartupLanguage_Hook()
    {
        string savedLanguage = PlayerPrefs.GetString("I2 Language", string.Empty);
        string defaultLanguage = "ChineseSimplified";

        if (defaultLanguage == "ChineseSimplified")
        {
            defaultLanguage = "Chinese (Simplified)";
        }
        else if (defaultLanguage == "ChineseTraditional")
        {
            defaultLanguage = "Chinese (Traditional)";
        }

        if (LocalizationManager.HasLanguage(savedLanguage, true, false))
        {
            LocalizationManager.CurrentLanguage = savedLanguage;
            return;
        }

        string supportedLanguage = LocalizationManager.GetSupportedLanguage(defaultLanguage);
        if (!string.IsNullOrEmpty(supportedLanguage))
        {
            string languageCode = LocalizationManager.GetLanguageCode(supportedLanguage);
            LocalizationManager.SetLanguageAndCode(supportedLanguage, languageCode, false, false);
            return;
        }

        foreach (var source in LocalizationManager.Sources)
        {
            if (source.mLanguages.Count > 0)
            {
                var firstLanguage = source.mLanguages[0];
                LocalizationManager.SetLanguageAndCode(firstLanguage.Name, firstLanguage.Code, false, false);
                return;
            }
        }

        #if Debug_Log

        global::System.Console.WriteLine("在本地化管理器中未找到可用语言");

        #endif
    }
    #endregion

    #region 玩家移动相关Hook
    public static int GetPlayerSpeed_Hook(IPyPlayerMove playerMove, IPyUserCmd userCmd)
    {
        return OriginalProxies.GetPlayerSpeed_Original(playerMove, userCmd);
    }

    public static void InterceptNew_Hook(PostProcessUserCommandSystem commandSystem, UserCmd userCommand)
    {
        if (Config.SildeWalk)
        {
            if ((userCommand.Buttons & 1) > 0)
            {
                userCommand.CleanButtonFlag(1);
            }
            if ((userCommand.Buttons & 2) > 0)
            {
                userCommand.CleanButtonFlag(2);
            }
            if ((userCommand.Buttons & 4) > 0)
            {
                userCommand.CleanButtonFlag(4);
            }
            if ((userCommand.Buttons & 8) > 0)
            {
                userCommand.CleanButtonFlag(8);
            }
        }
    }
    #endregion

    #region 相机逻辑相关Hook
    public static void AfterPrediction_Hook(CameraLogicToTransformSystem cameraSystem)
    {
        WorldCameraContext cameraContext = cameraSystem.GetFieldValue<WorldCameraContext>("_worldCameraContext");
        ICameraLogic cameraLogic = cameraContext.cameraLogic.CameraLogic;

        bool canUpdateCamera = cameraLogic != null &&
                              Contexts.sharedInstance.player.myPlayerEntity != null;

        if (!canUpdateCamera)
        {
            return;
        }

        PlayerEntity localPlayer = Contexts.sharedInstance.player.myPlayerEntity;
        float viewPitch = localPlayer.GetViewPitch();
        float punchPitch = localPlayer.GetPunchPitch();
        float viewYaw = localPlayer.GetViewYaw();
        float punchYaw = localPlayer.GetPunchYaw();

        Vector3 finalViewAngles = new Vector3(
            viewPitch + punchPitch,
            viewYaw + punchYaw,
            0f
        );

        cameraContext.cameraMode.Mode = cameraLogic.CameraMode();
        cameraContext.cameraTransform.Fov = Config.Fov ? Config.FirstPersonFov : cameraLogic.Fov();

        cameraContext.cameraTransform.Pitch = localPlayer.IsDead() ?
            cameraLogic.Pitch() : finalViewAngles.x;

        cameraContext.cameraTransform.Roll = cameraLogic.Roll();

        cameraContext.cameraTransform.Yaw = localPlayer.IsDead() ?
            cameraLogic.Yaw() : finalViewAngles.y;

        cameraContext.cameraTransform.position = cameraLogic.Position();
    }

    public static float GetCurrentCmdYaw_Hook(ICameraLogic cameraLogic)
    {
        PlayerEntity localPlayer = Contexts.sharedInstance.player.myPlayerEntity;
        if (localPlayer != null && !localPlayer.IsDead())
        {
            return Contexts.sharedInstance.worldCamera.cameraTransform.Yaw;
        }
        return OriginalProxies.GetCurrentCmdYaw_Original(cameraLogic);
    }

    public static float GetCurrentCmdPitch_Hook(ICameraLogic cameraLogic)
    {
        PlayerEntity localPlayer = Contexts.sharedInstance.player.myPlayerEntity;
        if (localPlayer != null && !localPlayer.IsDead())
        {
            return Contexts.sharedInstance.worldCamera.cameraTransform.Pitch;
        }
        return OriginalProxies.GetCurrentCmdPitch_Original(cameraLogic);
    }

    public static bool IsActive_Hook(TpsCameraLogic cameraLogic)
    {
        OriginalProxies.IsActive_Original(cameraLogic);
        CameraDataComponent cameraData = Contexts.sharedInstance.worldCamera.cameraData;

        cameraData.Fov = Config.ThirdPersonFov;
        cameraData.CameraYawAddValue = cameraLogic.GetFieldValue<float>("_yaw");
        cameraData.CameraPitchAddValue = cameraLogic.GetFieldValue<float>("_pitch");

        int frameInterval = Contexts.sharedInstance.time.time.FrameInterval;
        cameraData.TransTime = Mathf.Max(230, cameraData.TransTime + frameInterval);

        cameraData.IsTps = Config.ThirdPerson && Menu.forceThirdPerson;

        return cameraData.IsTps;
    }

    public static void TpsCameraUpdate_Hook(TpsCameraLogic cameraLogic)
    {
        OriginalProxies.TpsCameraUpdate_Original(cameraLogic);
        PlayerEntity localPlayer = Contexts.sharedInstance.player.myPlayerEntity;
        if (localPlayer is null || localPlayer.IsDead())
        {
            return;
        }

        Contexts contexts = cameraLogic.GetFieldValue<Contexts>("Contexts");
        CameraDataComponent cameraData = contexts.worldCamera.cameraData;
        PlayerEntity myPlayerEntity = contexts.player.myPlayerEntity;
        Vector3 viewOriginPosition = cameraLogic.GetFieldValue<Vector3>("_viewOrgPosition");
        Vector3 cameraEndPos = default;

        if (cameraData.IsTps)
        {
            cameraEndPos = cameraLogic.GetCalculateCameraEndPos(
                viewOriginPosition,
                cameraData.CameraYawAddValue,
                cameraData.CameraPitchAddValue,
                cameraLogic.GetFieldValue<float>("_distance"),
                10f
            );

            Vector3D forward = new Vector3D();
            Vector3D right = new Vector3D();
            Vector3D up = new Vector3D();

            AngleUtility.AnglesToVectors2(
                cameraLogic.GetFieldValue<float>("_yaw"),
                cameraLogic.GetFieldValue<float>("_pitch"),
                forward, right, up
            );

            forward.Normalize();
            right.Normalize();
            up.Normalize();

            right.ScaleBy(50f);

            cameraEndPos = cameraLogic.GetCalculateCameraEndPos(
                cameraEndPos,
                cameraData.CameraYawAddValue,
                0f,
                50f,
                10f
            );

            if (myPlayerEntity != null && myPlayerEntity.fov.Fov != cameraData.Fov)
            {
                myPlayerEntity.fov.Fov = cameraData.Fov;
                myPlayerEntity.fov.DelayFov = cameraData.Fov;
            }
        }

        if (cameraData.TransTime != 0)
        {
            Vector3 viewOrigin = cameraLogic.GetFieldValue<Vector3>("_viewOrgPosition");
            cameraLogic.InterpolateCamareDeadEndPos(viewOrigin, cameraEndPos, cameraData.TransTime);
        }
    }

    public delegate float CameraYawDelegate(Func<float> originalMethod);

    public static float GetCameraOwnerYaw_Hook(Func<float> originalMethod)
    {
        PlayerEntity localPlayer = Contexts.sharedInstance.player.myPlayerEntity;
        if (localPlayer != null && !localPlayer.IsDead())
        {
            return Contexts.sharedInstance.worldCamera.cameraTransform.Yaw;
        }

        return originalMethod();
    }

    public static float GetControlEntityYaw_Hook(Func<float> originalMethod)
    {
        PlayerEntity localPlayer = Contexts.sharedInstance.player.myPlayerEntity;
        if (localPlayer != null && !localPlayer.IsDead())
        {
            return UiIEventCondition.Get_cameraOwnerData_Yaw();
        }

        return originalMethod();
    }
    #endregion

    #region 命令组件相关Hook
    public static short LastCameraYaw_Hook(CommandsComponent commandsComponent)
    {
        return GetCameraAngleValue(
            commandsComponent,
            () => Contexts.sharedInstance.worldCamera.cameraTransform.Yaw,
            OriginalProxies.LastCameraYaw_Original
        );
    }

    public static short LastCameraPitch_Hook(CommandsComponent commandsComponent)
    {
        return GetCameraAngleValue(
            commandsComponent,
            () => Contexts.sharedInstance.worldCamera.cameraTransform.Pitch,
            OriginalProxies.LastCameraPitch_Original
        );
    }

    private static short GetCameraAngleValue(
        CommandsComponent commandsComponent,
        Func<float> angleGetter,
        Func<CommandsComponent, short> originalMethod
    )
    {
        PlayerEntity myPlayer = Contexts.sharedInstance.player.myPlayerEntity;
        if (myPlayer is null || myPlayer.IsDead())
        {
            return originalMethod(commandsComponent);
        }

        float angleValue = angleGetter();
        return (short)(angleValue * 100f);
    }

    public static void MakeCommand_Hook(ComputeUserCommandSystem system, UserCmd command, PlayerEntity player)
    {
        OriginalProxies.MakeCommand_Original(system, command, player);
    }
    #endregion
    
    #region 玩家朝向相关Hook
    public static void OnPlayback_Hook(PlayerOrientationPlabackSystem system)
    {
        OriginalProxies.OnPlayback_Original(system);

        PlayerEntity localPlayer = Contexts.sharedInstance.player.myPlayerEntity;
        if (localPlayer is null || localPlayer.IsDead() ||
            Contexts.sharedInstance is null ||
            Contexts.sharedInstance.player.cameraOwnerEntity is null)
            return;

        PlayerEntity cameraOwner = Contexts.sharedInstance.player.cameraOwnerEntity;
        if (cameraOwner is null ||
            cameraOwner.orientation is null ||
            cameraOwner.basicInfo is null ||
            cameraOwner.punchOrientation is null)
            return;

        cameraOwner.orientation.Pitch = AntiAim.SharedPitch;
        cameraOwner.orientation.Yaw = AntiAim.SharedYaw;
        cameraOwner.orientation.MoveYaw = AntiAim.SharedYaw;
        cameraOwner.orientation.ActThirdMoveInterYaw = AntiAim.SharedYaw;

        if (cameraOwner.basicInfo.Next == null)
            return;

        PlayerEntityData playerData = cameraOwner.basicInfo.Next;
        cameraOwner.punchOrientation.PunchPitch = playerData.PunchPitch;
        cameraOwner.punchOrientation.PunchYaw = playerData.PunchYaw;
    }

    public static void OnPredicate_Hook(PlayerOrientationPredicationSystem predictionSystem, PlayerEntity targetPlayer, IUserCmd userCommand)
    {
        var context = Contexts.sharedInstance;
        if (context?.player?.cameraOwnerEntity?.orientation is null)
        {
            OriginalProxies.OnPredicate_Original(predictionSystem, targetPlayer, userCommand);
            return;
        }

        var localPlayer = Contexts.sharedInstance.player.myPlayerEntity;
        if (localPlayer != null && !localPlayer.IsDead())
        {
            context.player.cameraOwnerEntity.orientation.Pitch = AntiAim.SharedPitch;
            context.player.cameraOwnerEntity.orientation.Yaw = AntiAim.SharedYaw;
        }

        OriginalProxies.OnPredicate_Original(predictionSystem, targetPlayer, userCommand);
    }

    public static void PredictCameraCommand_Hook(PlayerOrientationPredicationSystem predictionSystem, PlayerEntity targetPlayer, IUserCmd userCommand)
    {
        var localPlayer = Contexts.sharedInstance.player.myPlayerEntity;
        if (localPlayer is null || localPlayer.IsDead())
        {
            OriginalProxies.PredictCmdOnCamera_Original(predictionSystem, targetPlayer, userCommand);
        }
    }
    #endregion

    #region 用户命令发送相关Hook
    private static readonly BinaryDataWriter s_commandWriter = new BinaryDataWriter();
    private static float s_currentPitch;


    private static System.Collections.Generic.List<int> timeRestore = new System.Collections.Generic.List<int>();

    public static byte[] GetUserCmdBytes_Hook(
      SendUserCommandSystem sendSystem,
      LinkedList<UserCmd> userCommands,
      SnapshotsComponent snapshots,
      out int outputLength,
      bool isFirstCommand)
    {
        outputLength = 0;

        bool isDead = Contexts.sharedInstance.player.myPlayerEntity?.IsDead() ?? true;
        if (isDead)
        {
            return OriginalProxies.GetUserCmdBytes_Original(sendSystem, userCommands, snapshots, out outputLength, isFirstCommand);
        }

        if (userCommands.Count == 0) return null;
        var firstCmd = userCommands.First.Value;
        float yaw = 0f, pitch = 0f;
        float moveForward = 0f, moveRight = 0f;
        int buttons = 0;
        bool isSelfMoving = SendUserCommandSystem.Record?.IsSelfMove() ?? true;
        bool antiAimApplied = false;
        AntiAim.SetPitchAngle(ref pitch);
        Bhop(ref firstCmd);
        AntiAim.ExecuteAntiAim(
                    ref s_currentPitch, firstCmd, ref pitch,
                    ref yaw, ref moveForward, ref moveRight, ref buttons,
                    ref antiAimApplied
                );
        s_commandWriter.Reset();
        if (isFirstCommand)
        {
            s_commandWriter.WriteByte(31);
        }

        int latency = Math.Min(snapshots.ReceiveSnapshotLatency, 255);
        s_commandWriter.WriteByte((byte)latency);
        s_commandWriter.WriteInt(firstCmd.Seq);
        s_commandWriter.WriteInt(firstCmd.RenderTime);
        s_commandWriter.WriteInt(snapshots.LatestSnapshotSeqId);

        const int baseFlags = 0x0F | 0x20 | 0x10 | 0x80;
        s_commandWriter.WriteByte(baseFlags);
        s_commandWriter.WriteByte((byte)firstCmd.FrameInterval);

        s_commandWriter.WriteByte((byte)(isSelfMoving ? moveForward : 0));
        s_commandWriter.WriteByte((byte)(isSelfMoving ? moveRight : 0));


        s_commandWriter.WriteInt(buttons);

        int equipmentData = (firstCmd.BagId << 4) | firstCmd.Weapon;
        s_commandWriter.WriteByte((byte)equipmentData);

        s_commandWriter.WriteShort((short)(yaw * 100f));
        s_commandWriter.WriteShort((short)(pitch * 100f));

        var currentNode = userCommands.First.Next;
        while (currentNode != null)
        {

            var cmd = currentNode.Value;
            Bhop(ref cmd);
            AntiAim.ExecuteAntiAim(
                ref s_currentPitch, cmd, ref pitch,
                ref yaw, ref moveForward, ref moveRight, ref buttons,
                ref antiAimApplied
            );

            int positionMarker = s_commandWriter.Position;
            s_commandWriter.WriteByte(0);


            s_commandWriter.WriteByte((byte)cmd.FrameInterval);
            s_commandWriter.WriteByte((byte)moveForward);
            s_commandWriter.WriteByte((byte)moveRight);
            s_commandWriter.WriteInt(buttons);

            equipmentData = (cmd.BagId << 4) | cmd.Weapon;
            s_commandWriter.WriteByte((byte)equipmentData);

            const int movementFlags = 0x0F | 0x10;
            s_commandWriter.WriteShort((short)(yaw * 100f));
            s_commandWriter.WriteShort((short)(pitch * 100f));

            int finalFlags = movementFlags | 0x20;
            int endPosition = s_commandWriter.Position;
            s_commandWriter.Position = positionMarker;
            s_commandWriter.WriteByte((byte)finalFlags);
            s_commandWriter.Position = endPosition;

            timeRestore.Add(cmd.RenderTime);
            if (RuntimeState.BacktrackEnabled)
                cmd.RenderTime -= RuntimeState.BacktrackMs;
            currentNode = currentNode.Next;
        }

        byte[] resultBuffer = NetByteFactory.Instance.GetOrCreateNormalByte(
            s_commandWriter.Length,
            true
        );
        s_commandWriter.SetBytes(resultBuffer);
        outputLength = resultBuffer.Length;

        return resultBuffer;
    }

    #endregion

    #region 帧率显示相关Hook
    private static bool _isFpsTextModified = false;
    private static string _cachedOriginalFpsText = "";

    // 修改帧率显示文本
    public static void FpsDisplay_Update_Hook(FpsDisplay instance)
    {
        // 先调用原始方法
        OriginalProxies.FpsDisplayUpdate_Original(instance);

        // 获取Text组件
        Text textComponent = instance.GetFieldValue<Text>("_text");
        if (textComponent == null) return;

        // 确保只修改一次文本属性
        if (!_isFpsTextModified)
        {
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;
            _isFpsTextModified = true;
        }

        // 检查是否启用显示扩展信息
        bool isDisplayEnabled = instance.GetFieldValue<bool>("flag");
        if (!isDisplayEnabled)
        {
            _cachedOriginalFpsText = "";
            return;
        }

        //缓存原始FPS文本
        string currentText = textComponent.text;
        if (string.IsNullOrEmpty(currentText)) return;

        int customIndex = currentText.IndexOf("\nLuo - ");
        if (customIndex != -1)
        {
            _cachedOriginalFpsText = currentText.Substring(0, customIndex);
        }
        else
        {
            _cachedOriginalFpsText = currentText;
        }

        // 获取地图信息
        string mapInfoText = "";
        try
        {
            var battleRoom = Contexts.sharedInstance?.battleRoom;
            if (battleRoom != null && battleRoom.hasRoomData && battleRoom.roomData.Data != null)
            {
                var roomData = battleRoom.roomData.Data;

                // 主模式ID
                string raceType = roomData.RaceType.ToString();

                // 子模式ID
                string subRaceType = roomData.SubRaceTypeSpecified ? roomData.SubRaceType.ToString() : "未知";

                // 地图ID
                string sceneId = roomData.SceneIdSpecified ? roomData.SceneId.ToString() : "未知";

                // 胜利规则
                string winCondition = roomData.WinConditionSpecified ? roomData.WinCondition.ToString() : "未知";

                // 胜利规则值
                string winScore = roomData.WinScoreSpecified ? roomData.WinScore.ToString() : "未知";

                // 回合时长（不转换）
                string sectionTime = roomData.SectionTimeSpecified ? roomData.SectionTime.ToString() : "未知";

                // 复活时长（不转换）
                string reLiveTime = roomData.ReLiveTimeSpecified ? roomData.ReLiveTime.ToString() : "未知";

                // 房间最大人数
                string playerNum = roomData.PlayerNumSpecified ? roomData.PlayerNum.ToString() : "未知";

                // 是否为匹配模式
                string isMatch = roomData.IsMatchSpecified ? (roomData.IsMatch ? "是" : "否") : "未知";

                // Pve难度
                string pveLevel = roomData.PveLevelSpecified ? roomData.PveLevel.ToString() : "未知";

                // AI难度
                string botLevel = roomData.BotLevelSpecified ? roomData.BotLevel.ToString() : "未知";

                // 房间标识
                string roomId = roomData.RoomIdSpecified ? roomData.RoomId : "未知";

                mapInfoText = $"主模式ID:{raceType} | 子模式ID:{subRaceType} | 地图ID:{sceneId} | 胜利规则:{winCondition} | 胜利规则值:{winScore} | 回合时长:{sectionTime} | 复活时长:{reLiveTime} | 最大人数:{playerNum} | 匹配模式:{isMatch} | Pve难度:{pveLevel} | AI难度:{botLevel} | 房间ID:{roomId}";
            }
        }
        catch { }

        //获取系统时间
        string currentTime = DateTime.Now.ToString("yyyy年MM月dd日 HH:mm:ss");

        //获取玩家速度和坐标
        string speedText = "未知";
        string gameCoordText = "未知";
        string unityCoordText = "未知";

        try
        {
            var player = Contexts.sharedInstance?.player?.myPlayerEntity;
            if (player != null && player.hasMove)
            {
                // 计算速度
                Vector3 velocity = player.move.Velocity;
                float speed = MathUtility.CalculateHorizontalSpeed(velocity);
                speedText = Mathf.Floor(speed).ToString();

                // 获取游戏坐标
                if (PlayerUpdate.LocalEntity != null)
                {
                    Vector3 gameCoord = PlayerUpdate.LocalEntity.Position;
                    gameCoordText = $"X:{gameCoord.x:F1} Y:{gameCoord.y:F1} Z:{gameCoord.z:F1}";

                    // 转换为Unity坐标
                    Vector3 unityCoord = VectorCoordConverter.SsjjToUnity(gameCoord);
                    unityCoordText = $"X:{unityCoord.x:F1} Y:{unityCoord.y:F1} Z:{unityCoord.z:F1}";
                }
            }
        }
        catch { }

        // 获取武器信息
        string weaponText = "未知";
        try
        {
            if (PlayerUpdate.LocalEntity != null)
            {
                string weaponName = PlayerUpdate.LocalEntity.Weapon;
                string weaponId = PlayerUpdate.LocalEntity.CurrentWeaponName;
                weaponText = $"{weaponName}（{weaponId}）";
            }
        }
        catch{ }

        // Anti-Aim 调试信息
        string aaDebugText = "";
        try
        {
            if (PlayerUpdate.LocalEntity != null)
            {
                // 1. 设置的角度 (Input)
                float setAngle = Config.AntiAim_PitchAngle;

                // 2. 下发的角度 (Visual - 别人看到的)
                float serverAngle = PlayerUpdate.LocalEntity.ViewPitch;

                // --- 算法推导部分 ---

                // A. 模拟网络层 (Short 溢出)
                int packedInt = (int)(setAngle * 100f);
                short packedShort = (short)packedInt;
                float rawPitch = packedShort / 100f; // 服务器收到的原始值

                // B. 模拟视觉层 (预期下发值)
                float predictedVisual = rawPitch;
                while (predictedVisual > 180f) predictedVisual -= 360f;
                while (predictedVisual < -180f) predictedVisual += 360f;

                // C. 模拟物理层 (Hitbox - 真实判定值)
                // 核心规则：原始值绝对值 > 180 则反转，否则保持
                float predictedHitbox;
                if (Mathf.Abs(rawPitch) > 180f)
                {
                    predictedHitbox = -predictedVisual;
                }
                else
                {
                    predictedHitbox = predictedVisual;
                }
                // 物理引擎钳制
                predictedHitbox = Mathf.Clamp(predictedHitbox, -89f, 89f);

                // D. 判断是否去同步 (Desync)
                bool isDesync = Mathf.Sign(predictedVisual) != Mathf.Sign(predictedHitbox)
                                && Mathf.Abs(predictedVisual - predictedHitbox) > 10f;
                string status = isDesync ? " [去同步√]" : " [同步]";

                aaDebugText = $"设置的角度:{setAngle} | 预期的角度:{predictedVisual} | 下发的角度:{serverAngle} | 判定的角度:{predictedHitbox}{status}";
            }
        }
        catch { }

        //每帧都重新组合文本
        textComponent.text = $"{_cachedOriginalFpsText}\nLuo - {currentTime}\n\n\n\n\n\n\n\n\n\n\n\n\n\n速度：{speedText} | 坐标：{gameCoordText} | 武器：{weaponText}\n{mapInfoText}\n{aaDebugText}";
    }
    #endregion

    #region 假卡相关Hook
    public static void SendUdpData_Hook(BattleServer server, int packetId, byte[] data = null)
    {
        // 检查是否开启假卡，或者玩家是否无效/死亡
        if (!Config.FakeLag ||
            Contexts.sharedInstance.player.myPlayerEntity == null ||
            Contexts.sharedInstance.player.cameraOwnerEntity == null ||
            Contexts.sharedInstance.player.myPlayerEntity.IsDead())
        {
            OriginalProxies.SendUdpData_Original(server, packetId, data);
            return;
        }

        // 创建 UDP 包并加入缓存列表
        UdpPacket udpPacket = UdpPacket.CreateUdpPacket(server.ConnectionId, packetId, data);
        chokedPackets.Add(udpPacket);

        // 获取玩家当前速度
        Vector3 velocity = Contexts.sharedInstance.player.cameraOwnerEntity.move.Velocity;
        // 计算水平速度
        float currentSpeed = MathUtility.CalculateHorizontalSpeed(velocity);

        // 判断是否释放数据包
        bool shouldSend = chokedPackets.Count >= Config.FakeLagChoke ||
                          isSendingChoked ||
                          currentSpeed <= 0.1f;

        if (shouldSend)
        {
            isSendingChoked = true;

            // 一次性把存的包都发出去
            foreach (UdpPacket packet in chokedPackets)
            {
                server.UdpSocket.Send(packet.FinalData, packet.FinalLength);
            }

            // 清理缓存
            chokedPackets.Clear();
            isSendingChoked = false;
        }
    }
    #endregion

    #region 击中反馈相关Hook
    public static void HitPlayerHandler_Hook(HitPlayerHandler self, GameServerSetupData data)
    {
        OriginalProxies.HitPlayerHandler_Original(self, data);

        // 广播事件
        GlobalEvents.InvokePlayerHit(data);
    }
    #endregion

    #region 无视闪光弹相关Hook
    public static void FlashExplosion_SetData_Hook(FlashExplosionModel self, GrenadeExplosionEventEntityData ov, GrenadeExplosionEventEntityData data)
    {
        // 如果没开启无视闪光弹，正常执行
        if (!Config.NoFlash)
        {
            OriginalProxies.FlashExplosionSetData_Original(self, ov, data);
            return;
        }

        // 开启无视闪光弹时，强制隐藏闪光效果
        try
        {
            // 获取 _viewModel 私有字段
            Type modelType = typeof(FlashExplosionModel);
            FieldInfo viewModelField = modelType.GetField("_viewModel", BindingFlags.NonPublic | BindingFlags.Instance);

            if (viewModelField != null)
            {
                var viewModel = (FlashexplosionViewModel)viewModelField.GetValue(self);
                if (viewModel != null)
                {
                    // 强制隐藏闪光效果
                    viewModel.ShowRootshow = false;
                }
            }
        }
        catch (Exception ex)
        {
            #if Debug_Log
            global::System.Console.WriteLine($"[无视闪光弹] Hook执行失败: {ex.Message}");
            #endif
            // 出错时调用原始方法
            OriginalProxies.FlashExplosionSetData_Original(self, ov, data);
        }
    }
    #endregion

    //#region 聊天消息相关Hook
    //public static void PackChatMsg_Hook(UnityProxyHandler handler, UnityProxyData data, string message)
    //{
    //    // 先调用原始方法
    //    OriginalProxies.PackChatMsg_Original(handler, data, message);

    //    // 处理聊天消息
    //    if (!string.IsNullOrEmpty(message) && !message.StartsWith("Logger"))
    //    {
    //        try
    //        {
    //            // 解析消息
    //            string sender = ExtractXmlAttribute(message, "from");
    //            string msgType = ExtractXmlAttribute(message, "type");
    //            string content = ExtractXmlContent(message, "body");

    //            // 只处理全体和队伍频道
    //            if (msgType == "battle_all" || msgType == "battle_team")
    //            {
    //                string typeText = GetMessageTypeText(msgType);
    //                ////global::System.Console.WriteLine($"[聊天] [{typeText}] {sender}: {content}");

    //                // 自动查找并传递给 AI 处理
    //                var aiChatBot = UnityEngine.Object.FindObjectOfType<AIChatBot>();
    //                if (aiChatBot != null)
    //                {
    //                    aiChatBot.ProcessChatMessage(sender, content, msgType);
    //                }
    //            }
    //        }
    //        catch (System.Exception ex)
    //        {
    //            ////global::System.Console.WriteLine($"[聊天] 解析失败: {ex.Message}");
    //        }
    //    }
    //}

    //// 提取XML属性值
    //private static string ExtractXmlAttribute(string xml, string attributeName)
    //{
    //    string pattern = $"{attributeName}=\"";
    //    int startIndex = xml.IndexOf(pattern);
    //    if (startIndex == -1) return "";

    //    startIndex += pattern.Length;
    //    int endIndex = xml.IndexOf("\"", startIndex);
    //    if (endIndex == -1) return "";

    //    return xml.Substring(startIndex, endIndex - startIndex);
    //}

    //// 提取XML标签内容
    //private static string ExtractXmlContent(string xml, string tagName)
    //{
    //    string startTag = $"<{tagName}>";
    //    string endTag = $"</{tagName}>";

    //    int startIndex = xml.IndexOf(startTag);
    //    if (startIndex == -1) return "";

    //    startIndex += startTag.Length;
    //    int endIndex = xml.IndexOf(endTag, startIndex);
    //    if (endIndex == -1) return "";

    //    return xml.Substring(startIndex, endIndex - startIndex);
    //}

    //// 消息类型转换
    //private static string GetMessageTypeText(string msgType)
    //{
    //    switch (msgType)
    //    {
    //        case "battle_all": return "全体";
    //        case "battle_team": return "队伍";
    //        case "team": return "小队";
    //        case "personal": return "私聊";
    //        case "system": return "系统";
    //        case "tacticsSound": return "战术";
    //        default: return msgType;
    //    }
    //}
    //#endregion

    #region 控制台相关Hook
    // 改为只设置 _startConsole，不自动打开
    public static void QuickRuntimeConsole_Update_Hook(QuickRuntimeConsole console)
    {
        // 先调用原始方法
        OriginalProxies.QuickRuntimeConsoleUpdate_Original(console);

        try
        {
            // 只强制设置 _startConsole 为 true，允许输入 [cmd]
            FieldInfo startConsoleField = typeof(QuickRuntimeConsole).GetField(
                "_startConsole",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (startConsoleField != null)
            {
                startConsoleField.SetValue(console, true);
            }
        }
        catch (Exception ex)
        {
            #if Debug_Log
            global::System.Console.WriteLine($"快速运行时控制台钩子错误: {ex.Message}");
            #endif
        }
    }
    #endregion

    #region 连跳
    static float old_yaw = 0.0f;
    static bool flip = false;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const byte VK_SPACE = 0x20;

    private static void Bhop(ref UserCmd userCmd)
    {
        if (PlayerUpdate.LocalEntity.OnGround && Config.AirStrafe && !Config.AirStrafe_guoji)
        {
            keybd_event(VK_SPACE, 0, 0, UIntPtr.Zero);
            keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        if (!PlayerUpdate.LocalEntity.OnGround && (Config.AirStrafe || Config.AirStrafe_guoji))
        {
            if (userCmd.IsJump && Config.AirStrafe_guoji)
            {
                userCmd.Buttons &= -33;
            }

            var velocity = PlayerUpdate.LocalEntity.Move.Velocity;
            velocity.z = 0.0f;

            var forwardmove = userCmd.MoveForward;
            var sidemove = userCmd.MoveRight;

            if (PlayerUpdate.LocalEntity.Move.Velocity.Length() < 5.0f && forwardmove == 0f && sidemove == 0f)
                return;

            flip = !flip;

            var turn_direction_modifier = flip ? 1.0f : -1.0f;
            var viewangles = new Vector3(userCmd.CameraPitch / 100f, userCmd.CameraYaw / 100f);

            if (forwardmove != 0f || sidemove != 0f)
            {
                userCmd.MoveForward = 0.0f;
                userCmd.MoveRight = 0.0f;

                var turn_angle = Mathf.Atan2(-sidemove, forwardmove);
                viewangles.y += turn_angle * Mathf.Rad2Deg;
            }
            else if (forwardmove != 0f)
                userCmd.MoveForward = 0.0f;

            var strafe_angle = Mathf.Rad2Deg * (Mathf.Atan(15.0f / PlayerUpdate.LocalEntity.Move.Velocity.Length()));

            if (strafe_angle > 90.0f)
                strafe_angle = 90.0f;
            else if (strafe_angle < 0.0f)
                strafe_angle = 0.0f;

            var temp = new Vector3(0.0f, viewangles.y - old_yaw, 0.0f);
            temp.y = normalize_yaw(temp.y);

            var yaw_delta = temp.y;
            old_yaw = viewangles.y;

            var abs_yaw_delta = Mathf.Abs(yaw_delta);

            if (abs_yaw_delta <= strafe_angle || abs_yaw_delta >= 30.0f)
            {
                Vector3 velocity_angles = new Vector3();
                vector_angles(velocity, ref velocity_angles);

                temp = new Vector3(0.0f, viewangles.y - velocity_angles.y, 0.0f);
                temp.y = normalize_yaw(temp.y);

                var velocityangle_yawdelta = temp.y;
                var velocity_degree = get_velocity_degree(PlayerUpdate.LocalEntity.Move.Velocity.Length()) * 0.01f;

                if (velocityangle_yawdelta <= velocity_degree || PlayerUpdate.LocalEntity.Move.Velocity.Length() <= 15.0f)
                {
                    if (-velocity_degree <= velocityangle_yawdelta || PlayerUpdate.LocalEntity.Move.Velocity.Length() <= 15.0f)
                    {
                        viewangles.y += strafe_angle * turn_direction_modifier;
                        userCmd.MoveRight = 100f * turn_direction_modifier;
                    }
                    else
                    {
                        viewangles.y = velocity_angles.y - velocity_degree;
                        userCmd.MoveRight = 100f;
                    }
                }
                else
                {
                    viewangles.y = velocity_angles.y + velocity_degree;
                    userCmd.MoveRight = -100f;
                }
            }
            else if (yaw_delta > 0.0f)
                userCmd.MoveRight = -100f;
            else if (yaw_delta < 0.0f)
                userCmd.MoveRight = 100f;

            var move = new Vector3(userCmd.MoveForward, userCmd.MoveRight, 0.0f);
            var speed = move.Length();

            Vector3 angles_move = new Vector3();
            vector_angles(move, ref angles_move);

            var normalized_x = ((userCmd.CameraPitch / 100f + 180.0f) % 360.0f) - 180.0f;
            var normalized_y = ((userCmd.CameraYaw / 100f + 180.0f) % 360.0f) - 180.0f;

            var yaw = Mathf.Deg2Rad * (normalized_y - viewangles.y + angles_move.y);

            if (normalized_x >= 90.0f || normalized_x <= -90.0f || (userCmd.CameraPitch / 100f) >= 90.0f && (userCmd.CameraPitch / 100f) <= 200.0f || (userCmd.CameraPitch / 100f) <= -90.0f && (userCmd.CameraPitch / 100f) <= 200.0f)
                userCmd.MoveForward = -Mathf.Cos(yaw) * speed;
            else
                userCmd.MoveForward = Mathf.Cos(yaw) * speed;

            userCmd.MoveRight = Mathf.Sin(yaw) * speed;

            float get_velocity_degree(float velocity)
            {
                float tmp = Mathf.Rad2Deg * Mathf.Atan(30f / velocity);

                if (tmp > 90.0f)
                    return 90.0f;
                else if (tmp < 0.0f)
                    return 0.0f;
                else
                    return tmp;
            }
        }
    }

    static void vector_angles(Vector3 forward, ref Vector3 angles)
    {
        Vector3 view = new Vector3();

        if (forward.x == 0f && forward.y == 0f)
        {
            view[0] = 0.0f;
            view[1] = 0.0f;
        }
        else
        {
            view[1] = Mathf.Atan2(forward[1], forward[0]) * 180.0f / Mathf.PI;

            if (view[1] < 0.0f)
                view[1] += 360.0f;

            view[2] = Mathf.Sqrt(forward[0] * forward[0] + forward[1] * forward[1]);
            view[0] = Mathf.Atan2(forward[2], view[2]) * 180.0f / Mathf.PI;
        }

        angles[0] = -view[0];
        angles[1] = view[1];
        angles[2] = 0.0f;
    }

    static float normalize_yaw(float f)
    {
        if (float.IsInfinity(f))
            f = 0.0f;

        if (f > 9999999.0f)
            f = 0.0f;

        if (f < -9999999.0f)
            f = 0.0f;

        while (f < -180.0f)
            f += 360.0f;

        while (f > 180.0f)
            f -= 360.0f;

        return f;
    }

    #endregion
}
