using Assets.Scripts.Input;
using SkyDome.Cfg;
using SkyDome.Engine;
using SkyDome.Entity;
using SkyDome.Feature;
using SkyDome.Feature.Legit;
using SkyDome.Feature.Visuals;
using SkyDome.Feature.AutoTrigger;
using SkyDome.Features;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEngine;
using ThreadPriority = System.Threading.ThreadPriority;

namespace SkyDome
{
    public class Main : MonoBehaviour
    {
        private const string HookObjectName = "HookObject";
        private static GameObject _hookObject;
        private readonly List<GameObject> _hookList = new List<GameObject>();

        private void Awake()
        {
            var thread = new Thread(Init)
            {
                Priority = ThreadPriority.Highest
            };
            thread.Start();
        }

        private void Init()
        {
            if (_hookObject != null) return;

            var existingHook = GameObject.Find(HookObjectName);
            if (existingHook != null)
            {
                _hookObject = existingHook;
                return;
            }

            _hookObject = new GameObject(HookObjectName);
            AddComponent<PlayerUpdate>("PlayerUpdateObject");                    // 玩家实体更新
            AddComponent<WallHack>("WallHackObject");                            // ESP透视
            AddComponent<SpectatorList>("SpectatorListObject");                  // 观战列表
            AddComponent<AntiAimIndicator>("AntiAimIndicatorObject");            // 反自瞄指示器
            AddComponent<Trace>("TraceObject");                                  // 弹道轨迹
            AddComponent<Chams>("ChamsObject");                                  // 人物发光
            AddComponent<Radar>("RadarObject");                                  // 2D雷达
            AddComponent<C4Timer>("C4TimerObject");                              // C4倒计时
            AddComponent<Crosshair>("CrosshairObject");                          // 准星
            AddComponent<Aimbot>("AimbotObject");                                // 合法自瞄
            AddComponent<Triggerbot>("TriggerbotObject");                        // 自动扳机
            AddComponent<NoRecoil>("NoRecoilObject");                            // 无后坐力
            AddComponent<Resolver>("ResolverObject");                            // 角度解析
            AddComponent<Say>("SayObject");                                      // 自动喊话
            AddComponent<Menu>("MenuObject");                                    // 主菜单
            AddComponent<BoundingBox3D>("BoundingBox3DObject");                  // 3D边框
            AddComponent<FeatureIndicator>("FeatureIndicatorObject");            // 功能指示器
            AddComponent<ConsoleManager>("ConsoleManagerObject");                // 日志
            AddComponent<MikadukiSwordDkl>("MikadukiSwordDklAutoSheathObject");  // 幻锋自动收刀
            AddComponent<WindSpiritRecall>("WindSpiritRecallObject");            // 风铃
            AddComponent<NonStopDanceAuto>("NonStopDanceAutoObject");            // 热情舞动自动按键
            AddComponent<DamageDisplay>("DamageDisplayObject");                  // 伤害显示
            AddComponent<ItemESP>("ItemESPObject");                              // 物品透视
            AddComponent<ItemOutline>("ItemOutlineObject");                      // 物品发光
            AddComponent<MoveEntityESP>("MoveEntityESPObject");                  // 移动实体透视（子弹拖尾等）
            AddComponent<SceneBuffESP>("SceneBuffESPObject");                    // 场景Buff透视（图标等）
            AddComponent<SpeedDashboard>("SpeedDashboardObject");                // 速度仪表盘

            //AddComponent<BoneDebug>("BoneDebugObject");                          // 骨骼点调试
            //AddComponent<AIChatBot>("AIChatBotObject");                          // AI聊天机器人
            //AddComponent<QuickRuntimeConsoleGUI>("ConsoleGUIObject");            // 控制台
            // 输入系统
            InputCollector.Instance.SetDeviceInput(new MouseSimulator());
            // Hook系统
            // LoadAllAssembly();
            HookManager.StartHook();
        }

        private void AddComponent<T>(string objectName) where T : Component
        {
            if (FindObjectOfType<T>() is null)
            {
                GameObject gameObject = new GameObject(objectName);
                gameObject.AddComponent<T>();
                DontDestroyOnLoad(gameObject);
                _hookList.Add(gameObject);
            }
        }

        private void Destroy()
        {
            foreach (var hooks in _hookList)
            {
                if (hooks != null) { Destroy(hooks); }
            }
            if (_hookObject != null) { Destroy(_hookObject); }
        }

        //private static void LoadAllAssembly()
        //{
        //    Assembly currentAssembly = Assembly.GetExecutingAssembly();
        //    string[] resources = currentAssembly.GetManifestResourceNames();
        //    foreach (string resource in resources)
        //    {
        //        if (resource.EndsWith(".dll"))
        //        {
        //            using Stream stream = currentAssembly.GetManifestResourceStream(resource);
        //            if (stream != null)
        //            {
        //                byte[] assemblyData = new byte[stream.Length];
        //                stream.Read(assemblyData, 0, assemblyData.Length);
        //                Assembly.Load(assemblyData);
        //            }
        //        }
        //    }
        //}
    }
}
