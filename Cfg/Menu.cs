using SkyDome.Cfg;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class Configs
{
    public static string Current = "default";
    public static List<string> Names = new List<string>();

    public static void Save(string name)
    {
        if (string.IsNullOrEmpty(name)) return;

        ConfigManager.SaveConfig(name);

        if (!Names.Contains(name)) Names.Add(name);
        Current = name;
    }

    public static void Load(string name)
    {
        if (string.IsNullOrEmpty(name)) return;

        ConfigManager.LoadConfig(name);
        Current = name;
    }

    public static void Delete(string name)
    {
        if (name == "default" || !Names.Contains(name)) return;

        ConfigManager.DeleteConfig(name);
        Names.Remove(name);

        if (Current == name) Current = "default";
    }

    public static void Init()
    {
        // 从文件夹加载所有配置名称
        string[] savedConfigs = ConfigManager.GetAllConfigNames();
        Names = new List<string>(savedConfigs);

        // 确保有 default 配置
        if (!Names.Contains("default"))
        {
            Names.Add("default");
            ConfigManager.SaveConfig("default");
        }

        // 加载 default 配置
        ConfigManager.LoadConfig("default");
    }
}

namespace SkyDome.Features
{
    public class Menu : MonoBehaviour
    {
        // 窗口状态
        private bool show = true;
        private Rect winRect = new Rect(100, 100, 800, 600);
        private int tab;

        // 滚动视图
        private Vector2 scroll, cfgScroll;

        // 按键绑定
        private string bindKey;
        private bool bindWait;

        // 配置管理
        private string newCfg = "", delCfg = "";
        private bool confirmDel;
        private Rect delWinRect;

        // 弹窗提示
        private string popupMsg = "";
        private float popupTime;
        private bool popup;

        // 下拉框状态
        private Dictionary<string, DropdownState> dropdowns = new Dictionary<string, DropdownState>();

        // 下拉框状态类
        private class DropdownState
        {
            public bool IsOpen;
            public Rect Rect;
            public Vector2 Scroll;
            public int SelectedIndex;
        }

        // UI布局常量
        private static readonly Rect R_Header = new Rect(0, 0, 800, 35);
        private static readonly Rect R_NavBg = new Rect(0, 35, 140, 565);
        private static readonly Rect R_NavGroup = new Rect(10, 45, 120, 545);
        private static readonly Rect R_ContentBg = new Rect(140, 35, 660, 565);
        private static readonly Rect R_ContentArea = new Rect(155, 50, 630, 535);

        public static bool forceThirdPerson;

        private static readonly string[] Tabs = { "玩家", "视觉", "自瞄", "暴力", "反自瞄", "视角", "移动", "世界", "杂项", "换肤", "配置" };
        private static readonly string[] Bones = { "头心", "头顶", "脖子", "腹部", "左锁骨", "右锁骨", "左上臂", "右上臂", "左前臂", "右前臂", "左手", "右手", "左指", "右指", "骨盆", "左腿", "右腿", "左膝", "右膝", "左脚", "右脚", "左趾", "右趾" };

        // 样式缓存
        private static class S
        {
            public static bool Init;
            public static GUIStyle Head, Hint, Btn, Sect, Right, Box, CenterLabel;
            public static Texture2D Tex;

            public static void Setup()
            {
                if (Init) return;
                Tex = Texture2D.whiteTexture;

                Head = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 16, fontStyle = FontStyle.Bold, padding = new RectOffset(15, 0, 0, 0), normal = { textColor = Color.white } };
                Hint = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 12, fontStyle = FontStyle.Italic, padding = new RectOffset(0, 15, 0, 0), normal = { textColor = Color.gray } };
                Btn = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.MiddleCenter, fixedHeight = 42 };
                Sect = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } };
                Right = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
                CenterLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, normal = { textColor = Color.white } };
                Box = new GUIStyle(GUI.skin.box);

                Init = true;
            }
        }

        private void Start()
        {
            Configs.Init();
            SkyDome.Feature.SkinChanger.Initialize();

            // 初始化下拉框状态
            dropdowns["aim"] = new DropdownState();
            dropdowns["backAccessory"] = new DropdownState();
            dropdowns["character"] = new DropdownState();
            dropdowns["weapon"] = new DropdownState();
        }

        private void Update()
        {
            // 切换菜单显示
            if (Input.GetKeyDown(KeyCode.Home))
            {
                show = !show;
                this.useGUILayout = show;
            }

            // 第三人称切换
            if (Input.GetKeyDown(Config.ThirdPersonKey)) forceThirdPerson = !forceThirdPerson;

            if (Input.GetKeyDown(Config.AirStrafe_Key))
            {
                Config.AirStrafe = !Config.AirStrafe;
            }

            // 弹窗自动隐藏
            if (popup && Time.time - popupTime > 3f) popup = false;

            // 按键绑定逻辑
            if (bindWait) HandleKeyBinding();
        }

        private void HandleKeyBinding()
        {
            foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(kc))
                {
                    if (kc == KeyCode.Escape)
                        AssignKey(bindKey, KeyCode.None);
                    else if (kc != KeyCode.Home)
                        AssignKey(bindKey, kc);
                    else
                        continue;

                    bindWait = false;
                    bindKey = null;
                    break;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                if (Input.GetMouseButtonDown(i))
                {
                    AssignKey(bindKey, i == 0 ? KeyCode.Mouse0 : i == 1 ? KeyCode.Mouse1 : KeyCode.Mouse2);
                    bindWait = false;
                    bindKey = null;
                    break;
                }
            }
        }

        private void AssignKey(string id, KeyCode k)
        {
            switch (id)
            {
                case "Aim": Config.AimKey = k; break;
                case "Rage": Config.Silentbot_Key = k; break;
                case "Res": Config.ResolverKey = k; break;
                case "3rd": Config.ThirdPersonKey = k; break;
                case "Air": Config.AirStrafe_Key = k; break;
            }
        }

        private void OnGUI()
        {
            if (!show) return;
            S.Setup();

            var e = Event.current;

            // 处理下拉框外部点击
            if (e.type == EventType.MouseDown)
            {
                bool clickedInside = false;
                foreach (var dd in dropdowns.Values)
                {
                    if (dd.IsOpen && dd.Rect.Contains(e.mousePosition))
                    {
                        clickedInside = true;
                        break;
                    }
                }

                if (!clickedInside)
                {
                    CloseAllDropdowns();
                    e.Use();
                }
            }

            var skin = GUI.skin.window;
            skin.normal.background = skin.onNormal.background = S.Tex;

            GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            winRect = GUI.Window(0, winRect, DoWin, "");

            if (confirmDel)
            {
                delWinRect = new Rect(winRect.x + (winRect.width - 240) / 2, winRect.y + (winRect.height - 100) / 2, 240, 100);
                GUI.backgroundColor = new Color(0.2f, 0.1f, 0.1f, 1f);
                GUI.Window(1, delWinRect, DoDeleteWindow, "确认删除");
                GUI.BringWindowToFront(1);
            }
        }

        private void CloseAllDropdowns()
        {
            foreach (var dd in dropdowns.Values)
                dd.IsOpen = false;
        }

        private void DoDeleteWindow(int id)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label($"确定要永久删除配置\n[{delCfg}] 吗?", S.CenterLabel);
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("取消", GUILayout.Height(25)))
            {
                confirmDel = false;
                delCfg = "";
            }
            GUILayout.Space(10);
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("删除", GUILayout.Height(25)))
            {
                Configs.Delete(delCfg);
                confirmDel = false;
                delCfg = "";
                Pop("配置已删除");
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUI.DragWindow(new Rect(0, 0, 1000, 20));
        }

        private void DoWin(int id)
        {
            // 标题栏
            GUI.backgroundColor = new Color(0.2f, 0.4f, 0.7f, 1f);
            GUI.Box(R_Header, "");
            GUI.Label(R_Header, "SkyDome", S.Head);
            GUI.Label(R_Header, "[Home] 隐藏", S.Hint);

            // 导航栏背景
            GUI.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            GUI.Box(R_NavBg, "", GUI.skin.box);

            // 导航按钮
            GUI.BeginGroup(R_NavGroup);
            for (int i = 0; i < Tabs.Length; i++)
            {
                GUI.backgroundColor = i == tab ? new Color(0.25f, 0.45f, 0.75f) : new Color(0.2f, 0.2f, 0.2f);
                if (GUI.Button(new Rect(0, i * 47, 120, 42), Tabs[i], S.Btn))
                {
                    tab = i;
                    CloseAllDropdowns();
                }
            }
            GUI.EndGroup();

            // 内容区域背景
            GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.98f);
            GUI.Box(R_ContentBg, "", S.Box);
            GUI.backgroundColor = Color.white;

            // 内容渲染
            GUILayout.BeginArea(R_ContentArea);
            scroll = GUILayout.BeginScrollView(scroll, false, false);

            Action[] draws = { DrawVisuals, DrawEsp, DrawAimbot, DrawRage, DrawAntiAim, DrawView, DrawMovement, DrawWorldSettings, DrawMisc, DrawSkinChanger, DrawConfig };
            draws[tab]();

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUI.DragWindow(R_Header);
        }

        // 简化的UI辅助方法
        private void Sect(string t) { GUILayout.Space(15); GUILayout.Label(t, S.Sect); GUILayout.Space(2); }
        private void Grp(Action a) { GUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f); GUILayout.BeginVertical("box"); GUI.backgroundColor = Color.white; GUILayout.Space(5); a(); GUILayout.Space(5); GUILayout.EndVertical(); }
        private void Tog(ref bool v, string t) => v = GUILayout.Toggle(v, t, GUILayout.Height(22));

        private void Sli(string t, ref int v, int min, int max, string s = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(t, GUILayout.Width(100));
            v = (int)GUILayout.HorizontalSlider(v, min, max);
            GUILayout.Label($"{v}{s}", S.Right, GUILayout.Width(40));
            GUILayout.EndHorizontal();
        }

        private void Sli(string t, ref float v, float min, float max, string f = "F1")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(t, GUILayout.Width(100));
            v = GUILayout.HorizontalSlider(v, min, max);
            GUILayout.Label(v.ToString(f), S.Right, GUILayout.Width(40));
            GUILayout.EndHorizontal();
        }

        private void Key(string id, string t, KeyCode k)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(t, GUILayout.Width(100));

            bool active = bindWait && bindKey == id;
            GUI.backgroundColor = active ? new Color(1f, 0.8f, 0f) : Color.white;

            string btnText = active ? "按任意键" : k == KeyCode.None ? "点击绑定" : $"{k}";

            if (GUILayout.Button(btnText, GUILayout.Width(120)))
            {
                bindKey = active ? null : id;
                bindWait = !active;
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }

        // 通用下拉框组件
        private void Dropdown(string key, string label, List<string> items, Action<string> onSelect = null)
        {
            if (items == null || items.Count == 0) return;
            if (!dropdowns.ContainsKey(key)) dropdowns[key] = new DropdownState();

            var dd = dropdowns[key];

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));

            string displayText = dd.SelectedIndex >= 0 && dd.SelectedIndex < items.Count ? items[dd.SelectedIndex] : "请选择";

            if (GUILayout.Button(displayText, GUILayout.Height(22)))
            {
                dd.IsOpen = !dd.IsOpen;
                if (dd.IsOpen)
                {
                    foreach (var kvp in dropdowns)
                        if (kvp.Key != key) kvp.Value.IsOpen = false;
                }
            }

            if (Event.current.type == EventType.Repaint && dd.IsOpen)
            {
                Rect r = GUILayoutUtility.GetLastRect();
                dd.Rect = new Rect(r.x, r.y + r.height, r.width, Mathf.Min(150, items.Count * 22));
            }
            GUILayout.EndHorizontal();

            if (dd.IsOpen)
            {
                GUI.Box(dd.Rect, "", S.Box);
                GUILayout.BeginArea(dd.Rect);
                dd.Scroll = GUILayout.BeginScrollView(dd.Scroll);

                for (int i = 0; i < items.Count; i++)
                {
                    if (GUILayout.Button(items[i], GUILayout.Height(20)))
                    {
                        dd.SelectedIndex = i;
                        onSelect?.Invoke(items[i]);
                        dd.IsOpen = false;
                    }
                }

                GUILayout.EndScrollView();
                GUILayout.EndArea();
                GUILayout.Space(dd.Rect.height - 5);
            }
        }

        private void Pop(string m) { popup = true; popupMsg = m; popupTime = Time.time; }

        // 页面绘制
        private void DrawVisuals()
        {
            Sect("基础透视");
            Grp(() =>
            {
                Tog(ref Config.WallHack, " 开启透视");

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                Tog(ref Config.ShowRect, " 显示方框");
                Tog(ref Config.ShowSkeleton, " 显示骨骼");
                Tog(ref Config.ShowHpBar, " 显示血条");
                GUILayout.EndVertical();

                GUILayout.BeginVertical();
                Tog(ref Config.ShowHp, " 显示血量");
                Tog(ref Config.ShowName, " 显示名字");
                Tog(ref Config.ShowDistance, " 显示距离");
                GUILayout.EndVertical();

                GUILayout.BeginVertical();
                Tog(ref Config.ShowWeapon, " 显示武器");
                Tog(ref Config.ShowC4, " 显示C4");
                Tog(ref Config.ShowAirLine, " 射线示踪");
                GUILayout.EndVertical();

                GUILayout.BeginVertical();
                Tog(ref Config.ShowYaw, " 显示偏航角");
                Tog(ref Config.ShowPitch, " 显示俯仰角");
                Tog(ref Config.Show3DBox, " 显示3D框");
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
            });

            Sect("样式设置");
            Grp(() =>
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("方框类型:", GUILayout.Width(80));
                Config.Rect_Style = GUILayout.Toolbar(Config.Rect_Style, new[] { "完整框", "四角框" });
                GUILayout.EndHorizontal();
            });
        }

        private void DrawEsp()
        {
            Sect("视觉效果");
            Grp(() =>
            {
                Tog(ref Config.Chams, " 人物发光");
                Tog(ref Config.NoFlash, " 无视闪光弹");
                Tog(ref Config.ShowTracers, " 弹道轨迹");
                Tog(ref Config.ShowMoveEntityESP, " 子弹拖尾透视(青色)");
                Tog(ref Config.ShowSceneBuffESP, " 图标透视(品红色)");
                Tog(ref Config.ShowItemESP, " 物品透视(白色)");
                Tog(ref Config.ShowItemOutline, " 物品发光");
            });

            Sect("全局辅助");
            Grp(() =>
            {
                Tog(ref Config.ShowWatcher, " 观战列表");
                Tog(ref Config.ShowRadar, " 2D 雷达");
                Tog(ref Config.ShowSpeedDashboard, " 速度仪表盘");
                Tog(ref Config.ShowIndicators, " 开启功能指示器");
            });
        }

        private void DrawAimbot()
        {
            Sect("基础设置");
            Grp(() =>
            {
                GUILayout.BeginHorizontal();
                Tog(ref Config.Aimbot, " 开启自瞄");
                Tog(ref Config.AimRange_Show, " 绘制范围");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                Tog(ref Config.VisibleCheck, " 可见性检查");
                Tog(ref Config.ShowAimLine, " 自瞄线");
                GUILayout.EndHorizontal();

                Key("Aim", "自瞄热键", Config.AimKey);
            });

            if (!Config.Aimbot) return;

            Sect("详细参数");
            Grp(() =>
            {
                Sli("自瞄范围", ref Config.AimbotFOV, 0, 180, "°");

                Tog(ref Config.Aimbot_Smooth, " 平滑自瞄");
                if (Config.Aimbot_Smooth)
                {
                    Sli("平滑系数", ref Config.Aimbot_SmoothFactor, 1f, 30f, "F1");
                }

                Dropdown("aim", "瞄准部位:", new List<string>(Bones), (name) =>
                {
                    Config.AimPos = Array.IndexOf(Bones, name);
                });
            });
        }

        private void DrawRage()
        {
            Sect("暴力功能");
            Grp(() =>
            {
                Tog(ref Config.Silentbot, " 开启静默自瞄");
                if (!Config.Silentbot) return;

                Tog(ref Config.Silentbot_OnKey, " 仅按键触发");
                if (Config.Silentbot_OnKey) Key("Rage", "触发热键", Config.Silentbot_Key);
            });

            Sect("解析器");
            Grp(() =>
            {
                Tog(ref Config.Resolver, " 开启解析");
                Tog(ref Config.Resolver_Random, " 随机角度");
                Key("Res", "强制解析热键", Config.ResolverKey);
            });
        }

        private void DrawAntiAim()
        {
            Sect("反自瞄设置");
            Grp(() => Tog(ref Config.AntiAim, " 开启反自瞄"));

            Sect("假延迟设置");
            Grp(() =>
            {
                Tog(ref Config.FakeLag, " 开启假卡 (Fake Lag)");
                if (Config.FakeLag) Sli("阻塞 Tick", ref Config.FakeLagChoke, 0, 100);
            });

            if (!Config.AntiAim) return;

            Sect("角度参数");
            Grp(() =>
            {
                if (Config.AntiAim_Mode == 0)
                {
                    Sli("俯仰角 (Pitch)", ref Config.AntiAim_PitchAngle, -360, 360);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("输入俯仰角:", GUILayout.Width(100));
                    string pitchStr = Config.AntiAim_PitchAngle.ToString();
                    pitchStr = GUILayout.TextField(pitchStr);
                    float.TryParse(pitchStr, out Config.AntiAim_PitchAngle);
                    GUILayout.EndHorizontal();

                    Sli("偏航角 (Yaw)", ref Config.AntiAim_Yaw, -180, 180);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("输入偏航角:", GUILayout.Width(100));
                    string yawStr = Config.AntiAim_Yaw.ToString();
                    yawStr = GUILayout.TextField(yawStr);
                    float.TryParse(yawStr, out Config.AntiAim_Yaw);
                    GUILayout.EndHorizontal();
                }
                else if (Config.AntiAim_Mode == 1)
                {
                    Sli("俯仰角 (Pitch)", ref Config.AntiAim_PitchAngle, -360, 360);
                    Sli("旋转速度", ref Config.AntiAim_SpinFactor, 0, 100);
                }
                else if (Config.AntiAim_Mode == 2)
                {
                    Sli("俯仰角 (Pitch)", ref Config.AntiAim_PitchAngle, -360, 360);
                    Sli("偏航角 (Yaw)", ref Config.AntiAim_Yaw, -180, 180);
                    Sli("抖动最小值", ref Config.AntiAim_Jitter1, -180, 180);
                    Sli("抖动最大值", ref Config.AntiAim_Jitter2, -180, 180);
                }

                GUILayout.Space(10);
                GUILayout.Label("反自瞄模式:");
                Config.AntiAim_Mode = GUILayout.Toolbar(Config.AntiAim_Mode, new[] { "静态", "旋转", "抖动" });
            });
        }

        private void DrawView()
        {
            Sect("视角模式");
            Grp(() =>
            {
                GUILayout.BeginHorizontal();
                Tog(ref Config.ThirdPerson, " 强制第三人称");
                Key("3rd", "切换热键", Config.ThirdPersonKey);
                GUILayout.EndHorizontal();

                Tog(ref Config.Fov, " 第一人称FOV自定义");
            });

            Sect("视野范围 (FOV)");
            Grp(() =>
            {
                Sli("第三人称 FOV", ref Config.ThirdPersonFov, 0, 150);
                Sli("第一人称 FOV", ref Config.FirstPersonFov, 0, 150);
            });
        }

        private void DrawMovement()
        {
            Sect("身法移动");
            Grp(() =>
            {
                Tog(ref Config.Bhop, " 连跳 (Bhop)");
                Tog(ref Config.AirStrafe, " 八向连跳");
                Tog(ref Config.AirStrafe_guoji, " 不减速八向(空格连跳)");
                Key("Air", "八向热键", Config.AirStrafe_Key);
                Tog(ref Config.SildeWalk, " 滑步");
                Tog(ref SkyDome.RuntimeState.BacktrackEnabled, " 回溯 (Backtrack)");
                if (SkyDome.RuntimeState.BacktrackEnabled)
                {
                    Sli("回溯毫秒", ref SkyDome.RuntimeState.BacktrackMs, 0, 500, "ms");
                }
            });
        }

        private void DrawWorldSettings()
        {
            Sect("世界设置");
            Grp(() =>
            {
                if (GUILayout.Button("最低画质", GUILayout.Height(35)))
                    SkyDome.Feature.WorldSettings.SetLowestQuality();

                if (GUILayout.Button("解锁帧数", GUILayout.Height(35)))
                    SkyDome.Feature.WorldSettings.UnlockFrameRate();
            });
        }

        private void DrawMisc()
        {
            Sect("后座力控制");
            Grp(() =>
            {
                Tog(ref Config.NoRecoil, " 无后坐力");
                if (Config.NoRecoil) Tog(ref Config.SmoothControl, " 平滑移动");
            });

            Sect("其他");
            Grp(() =>
            {
                Tog(ref Config.AntiMouse1, " 屏蔽右键");
                Tog(ref Config.SpreadPredict, " 扩散预测");
            });

            Sect("扳机设置");
            Grp(() =>
            {
                Tog(ref Config.Triggerbot, " 自动扳机");
                Tog(ref Config.ExcludeSniper, " 扳机排除狙击");
                Tog(ref Config.TriggerbotDelayedActivation, " 延迟扳机");

                if (Config.TriggerbotDelayedActivation)
                {
                    Sli("持续时长", ref Config.TriggerbotActiveDuration, 0f, 10f, "F1");
                }
            });

            Sect("Auto");
            Grp(() =>
            {
                Tog(ref Config.WindSpiritPath, " 风铃");
            });

            Sect("自动喊话");
            Grp(() =>
            {
                Tog(ref Config.Say, " 开启刷屏");
                Config.SendMsg = GUILayout.TextField(Config.SendMsg);
            });
        }

        private Vector2 skinScroll;

        private void DrawSkinChanger()
        {
            Sect("属性修改 & 换肤");

            skinScroll = GUILayout.BeginScrollView(skinScroll);

            var localPlayer = SkyDome.Entity.PlayerUpdate.LocalEntity;
            if (localPlayer?._entity != null && localPlayer._entity.hasBasicInfo)
            {
                var info = localPlayer._entity.basicInfo.Current;

                Grp(() =>
                {
                GUILayout.Label("玩家属性", S.Sect);

                // 模型大小
                GUILayout.BeginHorizontal();
                GUILayout.Label($"模型大小: {info.Scale:F2}", GUILayout.Width(80));
                float newScale = GUILayout.HorizontalSlider(info.Scale, -5.0f, 5.0f);
                if (Math.Abs(newScale - info.Scale) > 0.01f)
                    SkyDome.Feature.SkinChanger.ChangeScale(newScale);
                GUILayout.EndHorizontal();

                // 头部大小
                GUILayout.BeginHorizontal();
                GUILayout.Label($"头部大小: {info.HeadEnlarge:F2}", GUILayout.Width(80));
                float newHead = GUILayout.HorizontalSlider(info.HeadEnlarge, -5.0f, 5.0f);
                if (Math.Abs(newHead - info.HeadEnlarge) > 0.01f)
                    SkyDome.Feature.SkinChanger.ChangeHeadEnlarge(newHead);
                GUILayout.EndHorizontal();

                // 阵营
                GUILayout.BeginHorizontal();
                GUILayout.Label($"阵营ID: {info.Team}", GUILayout.Width(80));
                int newTeam = (int)GUILayout.HorizontalSlider(info.Team, 0, 13);
                if (newTeam != info.Team)
                    SkyDome.Feature.SkinChanger.ChangeTeam(newTeam);
                GUILayout.EndHorizontal();

                // 透明度
                GUILayout.BeginHorizontal();
                GUILayout.Label($"透明度: {info.Alpha}", GUILayout.Width(80));
                int newAlpha = (int)GUILayout.HorizontalSlider(info.Alpha, 0, 100);
                if (newAlpha != info.Alpha)
                    SkyDome.Feature.SkinChanger.ChangeAlpha(newAlpha);
                GUILayout.EndHorizontal();

                // 自身透明
                GUILayout.BeginHorizontal();
                GUILayout.Label($"自身透明: {info.SelfAlpha}", GUILayout.Width(80));
                    int newSelfAlpha = (int)GUILayout.HorizontalSlider(info.SelfAlpha, 0, 100);
                    if (newSelfAlpha != info.SelfAlpha)
                        SkyDome.Feature.SkinChanger.ChangeSelfAlpha(newSelfAlpha);
                    GUILayout.EndHorizontal();
                });
            }
            else
            {
                GUILayout.Label("等待玩家进入游戏...", S.Hint);
            }

            GUILayout.Space(10);

            // 背饰选择
            if (SkyDome.Feature.SkinChanger.BackAccessoryNames.Count > 0)
            {
                GUILayout.Label($"背饰选择 ({SkyDome.Feature.SkinChanger.BackAccessoryNames.Count})", S.Sect);
                Grp(() => Dropdown("backAccessory", "背饰:", SkyDome.Feature.SkinChanger.BackAccessoryNames,
                    (name) => SkyDome.Feature.SkinChanger.ChangeBackAccessory(name)));
            }

            GUILayout.Space(10);

            // 角色选择
            if (SkyDome.Feature.SkinChanger.CharacterNames.Count > 0)
            {
                GUILayout.Label($"角色选择 ({SkyDome.Feature.SkinChanger.CharacterNames.Count})", S.Sect);
                Grp(() => Dropdown("character", "角色:", SkyDome.Feature.SkinChanger.CharacterNames,
                    (name) => SkyDome.Feature.SkinChanger.ChangeCharacter(name)));
            }

            GUILayout.Space(10);

            // 武器选择
            if (SkyDome.Feature.SkinChanger.WeaponNames.Count > 0)
            {
                GUILayout.Label($"武器选择 ({SkyDome.Feature.SkinChanger.WeaponNames.Count})", S.Sect);
                Grp(() => Dropdown("weapon", "武器:", SkyDome.Feature.SkinChanger.WeaponNames,
                    (name) => SkyDome.Feature.SkinChanger.ChangeWeapon(name)));
            }

            GUILayout.EndScrollView();
        }

        private void DrawConfig()
        {
            Sect($"当前配置: {Configs.Current}");
            Grp(() =>
            {
                GUILayout.BeginHorizontal();
                newCfg = GUILayout.TextField(newCfg, GUILayout.Width(120));

                if (GUILayout.Button("保存/新建") && newCfg != "")
                {
                    Configs.Save(newCfg);
                    Pop($"已保存配置: {newCfg}");
                    newCfg = "";
                }
                GUILayout.EndHorizontal();
            });

            Sect("已保存配置列表");
            Grp(() =>
            {
                cfgScroll = GUILayout.BeginScrollView(cfgScroll, GUILayout.Height(200));
                foreach (var c in Configs.Names)
                {
                    GUILayout.BeginHorizontal("box");
                    GUILayout.Label(c, GUILayout.Width(120));

                    if (GUILayout.Button("载入"))
                    {
                        Configs.Load(c);
                        Pop($"已载入配置: {c}");
                    }

                    if (c != "default" && GUILayout.Button("删除"))
                    {
                        delCfg = c;
                        confirmDel = true;
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            });

            if (popup) GUI.Box(new Rect(300, 560, 200, 30), popupMsg);
        }
    }
}
