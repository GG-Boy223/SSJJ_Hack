using UnityEngine;

namespace SkyDome.Cfg
{
    public class Config
    {
        public static bool Aimbot = true;                    // 开启自瞄
        public static KeyCode AimKey = KeyCode.Mouse0;       // 自瞄热键
        public static bool VisibleCheck = true;              // 可见性检查
        public static bool ShowAimLine;                      // 显示自瞄线
        public static bool AimRange_Show;                    // 显示自瞄范围圈
        public static int AimbotFOV;                         // 自瞄范围（FOV角度）
        public static bool Aimbot_Smooth;                    // 平滑自瞄开关
        public static float Aimbot_SmoothFactor = 5f;        // 平滑系数
        public static int AimPos;                            // 瞄准部位索引

        public static bool Triggerbot;                       // 扳机
        public static bool ExcludeSniper;                    // 扳机排除狙击枪
        public static bool AntiMouse1;                       // 屏蔽右键
        public static bool NoRecoil;                         // 无后坐力
        public static bool SmoothControl;                    // 平滑移动控制
        public static bool TriggerbotDelayedActivation;      // 扳机延迟激活
        public static float TriggerbotActiveDuration = 3f;   // 扳机激活持续时间

        public static bool Silentbot;                        // 静默自瞄
        public static bool Silentbot_OnKey;                  // 仅按键触发
        public static KeyCode Silentbot_Key;                 // 静默自瞄热键
        public static bool SpreadPredict;                    // 扩散预测
        public static float Accurary;                        // 精准度

        public static bool BacktrackEnabled = false;         // 开启回溯
        public static int BacktrackMaxMs = 200;              // 最大回溯窗口
        public static bool BacktrackPrioritizeRealBody = true;// 真身优先（当前未使用）
        public static bool BacktrackIgnoreWallShadows = true; // 隔墙不锁影（当前未使用）
        public static bool ShowBacktrack = false;             // 显示回溯残影
        public static bool AutoAttackInBacktrack = false;     // 回溯目标保持

        public static bool Resolver;                         // 开启解析器
        public static bool Resolver_Random;                  // 随机角度解析
        public static KeyCode ResolverKey;                   // 强制解析热键

        public static bool AntiAim;                          // 开启反自瞄
        public static float AntiAim_PitchAngle;              // 俯仰角
        public static float AntiAim_Yaw;                     // 偏航角
        public static int AntiAim_Mode;                      // 反自瞄模式 (0:静态 1:旋转 2:抖动)
        public static float AntiAim_Jitter1;                 // 抖动最小值
        public static float AntiAim_Jitter2;                 // 抖动最大值
        public static int AntiAim_SpinFactor;                // 旋转速度
        public static bool FakeLag;                          // 假延迟开关
        public static int FakeLagChoke = 6;                  // 假延迟阻塞包数量 (Tick)

        public static bool ThirdPerson;                      // 强制第三人称
        public static KeyCode ThirdPersonKey;                // 切换第三人称热键
        public static int ThirdPersonFov;                    // 第三人称FOV
        public static bool Fov;                              // 自定义第一人称FOV
        public static float FirstPersonFov;                  // 第一人称FOV值

        public static bool Bhop = true;                      // 连跳
        public static bool AirStrafe = false;                // 八向连跳
        public static bool AirStrafe_guoji = false;          // 不减速八向(空格连跳)
        public static KeyCode AirStrafe_Key = KeyCode.Mouse4;// 八向连跳热键
        public static bool SildeWalk;                        // 幽灵滑步

        public static bool WallHack = true;                  // 开启透视
        public static bool ShowRect = true;                  // 显示方框
        public static bool ShowDistance = true;              // 显示距离
        public static bool ShowName = true;                  // 显示名字
        public static bool ShowHpBar = true;                 // 显示血条
        public static bool ShowC4;                           // 显示携带C4
        public static bool ShowHp = true;                    // 显示血量数值
        public static bool ShowSkeleton;                     // 显示骨骼
        public static bool ShowWeapon = true;                // 显示武器
        public static bool ShowAirLine;                      // 显示射线
        public static bool ShowYaw;                          // 显示偏航角
        public static bool ShowPitch;                        // 显示俯仰角
        public static int Rect_Style;                        // 方框样式 (0:完整 1:四角)

        public static bool Chams;                            // 人物发光
        public static bool NoFlash;                          // 无视闪光弹
        public static bool ShowDamage = true;                // 显示伤害数字
        public static bool ShowTracers;                      // 弹道轨迹
        public static bool ShowWatcher;                      // 观战列表
        public static bool ShowRadar;                        // 2D雷达
        public static bool ShowCrosshair;                    // 准心
        public static bool Show3DBox;                        // 3D盒子
        public static bool ShowIndicators = true;            // 功能指示器
        public static bool ShowSpeedDashboard = true;        // 速度仪表盘
        public static bool ShowItemESP = true;               // 物品透视(白色)
        public static bool ShowItemOutline = true;           // 物品发光
        public static bool ShowMoveEntityESP = true;         // 移动实体透视(青色)
        public static bool ShowSceneBuffESP = true;          // 场景Buff透视(品红色)

        public static bool Say;                              // 自动喊话
        public static string SendMsg;                        // 喊话内容
        public static bool WindSpiritPath;                   // 风铃

        public static bool ShowC4Timer;                      // 显示C4倒计时
    }
}
