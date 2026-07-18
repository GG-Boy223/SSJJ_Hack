# SSJJ_Hack 项目说明与逐类分析

> 本文以 `SkyDome.csproj` 当前显式编译项和现有源码为准。它描述的是代码实际行为，不把菜单文字、注释设想或未接入方法当成已经生效的逻辑。

## 项目定位

SSJJ_Hack 是一个面向 Unity/Mono 运行时研究的 C# 类库，目标框架为 .NET Framework 4.8。程序集通过 `Loader.Load()` 创建常驻 Unity 对象，由 `Main` 注册实体同步、输入替换、功能组件和本地方法 Hook。

本仓库仅用于学习、研究和经过授权的测试。使用者应自行遵守法律法规、目标软件服务条款和测试环境授权边界。

## 工程状态

| 项目项 | 当前值 |
| --- | --- |
| 输出类型 | C# Class Library |
| 程序集名 | `SkyDome` |
| 目标框架 | .NET Framework 4.8 |
| C# 版本 | 8.0 |
| 默认配置 | `Debug|AnyCPU` |
| 主要发布配置 | `Release|x64` |
| Unsafe | 已启用 |
| Hook 实现 | `MonoMod_Hook/MethodHook.cs` + `CodePatcher` |
| 游戏依赖 | `引用/` 下的 Unity、Entitas、物理、网络和游戏程序集 |
| 主入口 | `Loader.Load()` |
| 回溯入口 | `Main.Awake()` 调用 `BacktrackManager.Initialize()` |
| 配置目录 | `Application.persistentDataPath/SkyConfigs` |

## 事实边界

- `RuntimeState.cs` 已从项目删除，当前不存在该共享状态层。
- `Feature/AIChatBot.cs` 和 `Feature/Visuals/BoneDebug.cs` 虽在 `.csproj` 中，但类实现被整段注释，最终程序集没有 `AIChatBot` 或 `BoneDebug` 类型。
- `QuickRuntimeConsoleGUI` 可以编译，但 `Main` 中的注册被注释；其 Hook 注册也被注释，默认运行链不会创建它。
- `SkinChanger` 不由 `Main.AddComponent` 创建。它通过 `Menu.Start()` 调用静态 `Initialize()`，再由菜单按钮调用静态修改方法。
- `WorldSettings` 继承 `MonoBehaviour`，但当前只使用静态方法，没有运行时组件注册。
- `BacktrackPrioritizeRealBody` 和 `BacktrackIgnoreWallShadows` 会显示并持久化，但当前运行逻辑不读取它们。
- 回溯的 `Fire_Hook` 只覆盖 `TraceResult.EntityId`，不会把 Unity 当前碰撞体移动回历史位置，也不会直接把 Fire 射线终点改成历史坐标。

## 总体架构

```text
Loader.Load
  -> SkyDome_HookObject + Main
      -> Main.Awake
          -> BacktrackManager.Initialize
          -> Init 线程
              -> PlayerUpdate 与各 MonoBehaviour
              -> InputCollector.SetDeviceInput(MouseSimulator)
              -> HookManager.StartHook

PlayerUpdate
  -> LocalEntity / CameraEntity / PredictionEntity / EntityList / MainCamera
      -> Aimbot、Silentbot、ESP、Radar、Triggerbot 等读取

BacktrackManager.Update
  -> 每敌人 128 条历史记录
      -> Aimbot 或 Silentbot 选择
          -> BacktrackAimState
              -> AntiAim 写角度/攻击位
              -> GetUserCmdBytes 调整首命令时间
              -> Fire_Hook 覆盖返回目标 ID
```

## 运行时组件注册

`Main.Init()` 会为下列类型分别创建常驻 `GameObject`。`AddComponent<T>` 先通过 `FindObjectOfType<T>()` 去重。

| 类型 | 主要职责 |
| --- | --- |
| `PlayerUpdate` | 同步本地、相机、预测和普通玩家实体 |
| `WallHack` | 玩家 ESP 与回溯残影 |
| `SpectatorList` | 观战者列表 |
| `AntiAimIndicator` | 反自瞄方向提示 |
| `Trace` | 客户端弹道线 |
| `Chams` | 玩家轮廓 |
| `Radar` | 屏幕中心雷达 |
| `C4Timer` | C4 倒计时 |
| `Crosshair` | 狙击未开镜准星 |
| `Aimbot` | 普通自瞄与历史候选 |
| `Triggerbot` | 射线命中后的输入触发 |
| `NoRecoil` | 命令与相机后坐力补偿 |
| `Resolver` | 目标 ViewPitch 修改 |
| `Say` | 服务端喊话与本地命中消息 |
| `Menu` | IMGUI 菜单、配置和换肤入口 |
| `BoundingBox3D` | 骨骼范围 3D 框 |
| `FeatureIndicator` | 扳机和 FakeLag 状态提示 |
| `ConsoleManager` | Debug 控制台 |
| `MikadukiSwordDkl` | 特定武器自动按键 |
| `WindSpiritRecall` | 风铃路径检测与绘制 |
| `NonStopDanceAuto` | 特定玩法自动按键 |
| `DamageDisplay` | 基于 HP 差值的伤害数字 |
| `ItemESP` | 掉落武器文字 |
| `ItemOutline` | 掉落武器轮廓 |
| `MoveEntityESP` | 移动物体文字 |
| `SceneBuffESP` | 场景 Buff 文字 |
| `SpeedDashboard` | 速度仪表 |

注意：`Main.Init()` 在显式创建的高优先级 `Thread` 中调用多项 Unity API。Unity API 通常要求主线程；本文只记录当前实现，不将其视为线程安全保证。`Main` 的清理方法名为 `Destroy()`，不是 Unity 生命周期方法 `OnDestroy()`，因此 Unity 不会自动按常规生命周期调用该方法。

## 启动与生命周期类

### `SkyDome.Loader`

文件：`Loader.cs`

- 这是公开装载入口。`Load()` 在 Debug 构建先尝试建立控制台，然后查找或创建名为 `SkyDome_HookObject` 的根对象。
- `EnsureMainComponent()` 保证根对象上存在 `Main`，随后通过 `DontDestroyOnLoad` 保持跨场景生命周期。
- `_hookObject` 是静态去重引用；引用存在时只补 `Main`，不重复创建根对象。
- 初始化异常时，仅当对象是本次新建才 `DestroyImmediate`，然后清空静态引用并重新抛出异常。
- `Unload()` 销毁根对象并清引用，但没有显式调用 `HookManager.RemoveAllHooks()`；实际 Hook 是否恢复取决于对象销毁链中是否另有调用。

### `t.u`

文件：`Loader.cs`

- 这是短名称兼容入口。`i()` 直接转发到 `SkyDome.Loader.Load()`，`Unload()` 转发到 `Loader.Unload()`。
- 类本身没有实例状态，也没有 Unity 生命周期函数；存在意义是为外部加载器提供更短、固定的方法名。

### `SkyDome.Main`

文件：`Main.cs`

- `Awake()` 首先创建常驻 `BacktrackManager_Core`，随后启动最高优先级线程执行 `Init()`。
- `Init()` 通过静态 `_hookObject` 和场景中名为 `HookObject` 的对象双重去重。若场景已经存在同名对象，会直接返回，不验证组件是否齐全。
- 它为每个功能创建独立常驻对象，然后将游戏输入实现替换为 `MouseSimulator`，最后调用 `HookManager.StartHook()`。
- `_hookList` 只记录本实例新建的功能对象；已存在的组件不会加入列表。
- `Destroy()` 试图销毁 `_hookList` 和 `HookObject`，但方法名不符合 Unity 的 `OnDestroy` 约定。

## 配置与菜单类

### `SkyDome.Cfg.Config`

文件：`Cfg/Config.cs`

- 这是纯静态字段配置容器，类本身不需要实例。所有菜单、功能和 Hook 直接读取这些字段。
- 字段按普通自瞄、扳机、Silentbot、Backtrack、Resolver、AntiAim/FakeLag、视角、移动、ESP、视觉和杂项分组。
- 默认值直接决定首次运行行为，例如 `Aimbot=true`、`Bhop=true`、`WallHack=true`、`BacktrackEnabled=false`、`BacktrackMaxMs=200`。
- 类不做范围校验；滑块范围主要由 `Menu` 控制，配置文件可以写入菜单范围以外的数值。
- `BacktrackPrioritizeRealBody` 与 `BacktrackIgnoreWallShadows` 当前只有配置和 UI 语义，没有运行时消费者。

### `SkyDome.Cfg.ConfigManager`

文件：`Cfg/ConfigManager.cs`

- 静态初始化时反射缓存 `Config` 的全部 `public static` 字段，并将配置目录固定为 `persistentDataPath/SkyConfigs`。
- `SaveConfig()` 逐字段写入 `字段名=值`；支持 `bool`、`int`、`float`、`string` 和 `KeyCode`，其他类型退回 `ToString()`。
- `LoadConfig()` 按行 `Split('=')`，只接受恰好两段；未知字段忽略，单字段解析失败不会中止整个文件。
- 加载结束后，如果 `BacktrackMaxMs <= 0`，恢复为 `200`。这是当前唯一的回溯窗口默认恢复点。
- `DeleteConfig()` 和 `GetAllConfigNames()` 封装文件删除与枚举。所有公开文件操作都捕获异常，非 `Debug_Log` 构建静默失败。
- 浮点序列化和解析使用当前区域文化；字符串包含 `=` 时无法按原值载入。`configName` 直接参与 `Path.Combine`，本类不清理文件名。

### `Configs`

文件：`Cfg/Menu.cs`

- 这是菜单上层的静态配置集合管理器，维护 `Current` 和 `Names`。
- `Init()` 从磁盘枚举名称，确保 `default` 存在并立即加载；不存在时会先把当前静态配置保存为默认文件。
- `Save()`、`Load()` 和 `Delete()` 委托 `ConfigManager`。删除明确禁止 `default`，删除当前项后把 `Current` 改回 `default`，但不自动重新加载默认配置。
- `Names` 只做内存去重；磁盘层是否成功由 `ConfigManager` 内部处理，调用方无法从返回值判断失败。

### `SkyDome.Features.Menu`

文件：`Cfg/Menu.cs`

- `Start()` 初始化配置集合、调用 `SkinChanger.Initialize()`，并建立瞄准部位、角色、武器和背饰下拉状态。
- `Update()` 用 `Home` 切换窗口、用配置热键切换第三人称和 AirStrafe，并处理三秒弹窗和按键绑定。
- `OnGUI()` 绘制 11 个页签：玩家、视觉、自瞄、暴力、反自瞄、视角、移动、世界、杂项、换肤、配置。
- `forceThirdPerson` 是第三人称实际切换状态；`Config.ThirdPerson` 是功能总开关，相关 Hook 同时读取两者。
- 回溯页把窗口限制在 `0..5000ms`，并显示两个当前不参与运行逻辑的选项。
- 换肤页直接调用 `SkinChanger` 的静态修改方法；配置页调用 `Configs` 保存、加载和删除。
- 菜单关闭时设置 `useGUILayout=false`；按键绑定忽略 `Home`，`Escape` 会绑定为 `None`。

### `Menu.DropdownState`

- 这是每个下拉框的独立 UI 状态：是否展开、屏幕矩形、滚动位置和选中索引。
- 它没有业务逻辑，只被 `Menu.Dropdown()` 和外部点击关闭逻辑读写。

### `Menu.S`

- 这是静态 IMGUI 样式缓存，保存标题、按钮、区段、标签、盒子和白色纹理。
- `Setup()` 只初始化一次，样式从当前 `GUI.skin` 派生；后续皮肤变化不会触发重建。

## 实体、输入与公共工具类

### `SkyDome.Entity.PlayerInfo`

文件：`Entity/PlayerInfo.cs`

- 它是 `PlayerEntity` 的只读包装器，构造时拒绝 `null`。
- 属性统一暴露 ID、客户端时间、相机距离、名字、队伍、职业、HP、死亡、C4、FOV、武器、位置、视角、Punch、第三人称对象、移动和状态组件。
- `Distance` 把补偿位置从游戏坐标转成 Unity 坐标，再乘 `0.01` 转为米。
- `Position` 使用 `GetCompenstatePos`，并非原始 `GetX/Y/Z`；回溯身体记录则使用另一条原始坐标路径。
- 多数属性直接解引用底层组件，只有少量字符串和最大 HP 做保护；调用方必须保证实体组件完整。

### `SkyDome.Entity.PlayerUpdate`

文件：`Entity/PlayerUpdate.cs`

- 每帧从 Entitas `PlayerContext` 获取同时具有 `BasicInfo` 和 `ThirdPersonUnityObjects` 的实体组。
- 它反射清除 `ThirdPersonUnityObjectsComponent` 的 `_playerCached` 和 `_playerCache`，强制下一次骨骼访问重建缓存。
- `RetrievePlayerInfo()` 把 `isMyPlayer`、`isCameraOwner`、`isPrediction` 分别包装到三个静态字段；这些特殊实体不会进入普通 `EntityList`。
- `MainCamera` 每帧取 `Camera.main`。整个更新在 try/catch 中，异常时保留上一帧静态状态而不是清空。
- `ResetPlayerTransformCache()` 假定实体组非空；反射字段找不到时使用空条件跳过写入。

### `SkyDome.Engine.MouseSimulator`

文件：`Engine/MouseSimulator.cs`

- 实现游戏的 `IDeviceInput`，在 `Main.Init()` 中替换原设备输入。
- `AnyKey()` 先触发静态 `PreInputCallback`；普通 Aimbot 正是通过该回调在游戏读取输入前更新目标与注入鼠标位移。
- `ForceAxisDelta` 是一次性轴增量，读取后清零；`ForceAxisPersistent` 当前声明但没有参与 `GetAxis()`。
- `_forcedKeys` 和 `_forcedMouseButtons` 保存模拟状态。`TrueOnce/FalseOnce` 在 `GetKey` 或 `GetMouseButton` 消费后变为 `None`。
- `GetMouseButton()` 还实现 `AntiMouse1` 条件分支；深层上下文没有完整判空。
- 字典和静态轴字段无锁，多个组件可在同一帧覆盖同一个按键状态。

### `MouseSimulator.InputState`

- `None` 表示回退到真实输入；`TrueKeep/FalseKeep` 持续强制；`TrueOnce/FalseOnce` 在一次状态读取后清除。
- `GetKeyDown()` 只把 `TrueOnce` 视为按下沿，而 `GetMouseButtonDown/Up()` 当前直接返回 Unity 原始输入，不读取模拟鼠标字典。

### `SkyDome.Extension.ReflectionExtensions`

文件：`Extension/ReflectionExtensions.cs`

- 为实例对象提供私有/公开字段读取和方法调用，统一使用 `Instance | Public | NonPublic`。
- `GetFieldValue<T>`、`InvokeMethod` 和 `InvokeMethod<T>` 对空对象、空名字或成员缺失抛出明确异常。
- `TryInvokeMethod` 两个重载捕获全部异常并返回 `false`；Debug 构建打印消息。
- 方法查找只按名字调用 `GetMethod`，未指定重载参数类型，存在重载歧义风险。

### `SkyDome.GlobalEvents`

文件：`Utilities/GlobalEvents.cs`

- 当前只定义 `OnPlayerHit` 事件及其触发方法。
- `HookManager.HitPlayerHandler_Hook` 在原逻辑后触发事件；`Say` 在 `Start/OnDestroy` 订阅和退订。
- 事件是同步调用，订阅者异常会沿调用栈传播到 Hook，类本身不隔离订阅者。

### `SkyDome.Utilities.MathUtility`

文件：`Utilities/MathUtility.cs`

- 提供二维欧氏距离和水平速度计算。
- `CalculateHorizontalSpeed` 使用速度的 `x/y` 分量；FakeLag 和速度仪表沿用这一坐标语义。
- 类没有实例状态，虽然声明为普通类，方法全部静态。

### `SkyDome.Utilities.PlayerUtility`

文件：`Utilities/PlayerUtility.cs`

- 为 `PlayerInfo` 和 `PlayerEntity` 提供忽略大小写的骨骼 Transform 查找。
- `GetValidHeadNub()` 优先真实 `Bip01_HeadNub`；不存在时取 Head 第一个子节点，完全无子节点时创建 `fake_HeadNub` 并挂到 Head。
- 创建假节点会永久修改角色 Transform 层级，没有统一销毁或去重名称之外的生命周期管理。
- `Length()` 计算 Unity `Vector3` 三维长度，与 `Vector3.magnitude` 等价。

### `SkyDome.Utilities.ViewportUtility`

文件：`Utilities/ViewportUtility.cs`

- `IsScreenPointVisible()` 要求深度大于 `0.01`，并检查带少量左/下容差的屏幕边界。
- `WorldPointToScreenPoint()` 使用 `PlayerUpdate.MainCamera`，按 `Screen.height/scaledPixelHeight` 缩放并翻转 Y，返回适合 IMGUI 的坐标。
- 调用方必须保证 `MainCamera` 和 `scaledPixelHeight` 有效；本类不判空、不防除零。

## 回溯类

### `SkyDome.Feature.Backtrack.BacktrackRecord`

文件：`Feature/BacktrackManager.cs`

- 表示一条可复用历史快照，保存实体 ID、身体/头/脊柱 Unity 坐标、捕获时间、帧号和骨骼有效标志。
- 它不复制玩家对象，也不保存速度或完整碰撞体；环形缓冲覆盖时直接重写同一对象。

### `BacktrackTargetHistory`

- 每个敌人拥有一个实例和固定 128 槽 `Records`，构造时预分配全部记录对象。
- `WriteIndex` 从 `-1` 开始，`Count` 最大 128；还保存最近采样时间、失踪计数、模型实例 ID 和头/脊柱 Transform。
- `UpdateTransforms()` 只在身体模型实例 ID 变化时重新解析骨骼；脊柱回退顺序是 `Spine1 -> Spine -> Neck`。
- 有效列表和显示列表分别有帧级缓存；缓存键包含帧、窗口和死亡冻结参考时间。

### `BacktrackSelection`

- 保存跨 Aimbot、Silentbot、命令序列化和 Fire Hook 的当前选择。
- 字段包括自动攻击保持、记录索引/时间、目标 ID、年龄毫秒和包计数。
- `Reset()` 把 ID 和索引恢复为 `-1`，年龄归零，防止旧目标污染后续命令。

### `BacktrackAimState`

- 这是 `BacktrackSelection` 单例的静态访问层。
- `SelectFromAimbot()` 写入完整选择并根据 `Config.AutoAttackInBacktrack` 设置保持状态，年龄为 `(realtimeSinceStartup - CaptureTime) * 1000` 截断整数。
- `SelectFromSilentbot()` 写记录、目标和年龄，但不主动重置 `AutoAttackActive` 或 `PacketCount`，这是当前实现的状态继承行为。
- `Reset()` 被关闭、死亡、实时目标胜出和异常路径共同调用。

### `BacktrackEntityState`

- 每帧从 `Contexts` 更新实体列表、世界相机、本地实体和 HeldTarget。
- HeldTarget 在自动攻击保持生效且目标仍存活、有 hitbox/第三人称对象时继续保留。
- 否则按敌人身体位置加 `(0,150,0)` 后的屏幕曼哈顿距离选择最近目标，初始最大距离为 `10000`。
- HeldTarget 与 `BacktrackAimState.TargetEntityId` 是两条独立状态链，可能不是同一实体；该行为按当前设计保留。
- 更新异常只清 HeldTarget 和距离，不清实体列表、相机或本地实体。

### `BacktrackCoordinateUtility`

- `GetRawPosition()` 读取 `GetX/GetY/GetZ`，空实体返回零。
- `ToUnity()` 使用 `(x,y,z) -> (-y,z,x)`；Backtrack 身体位置和相关射线转换依赖该坐标约定。

### `BacktrackBoneUtility`

- 管理常规骨骼哈希、特殊角色骨骼、动态哈希和按实体缓存。
- 对武器名包含 `rpg_by_parasitism` 的角色，头部依次尝试特殊骨骼字典、递归查找、fallback 骨骼，最后回到标准 Head。
- `FindRecursive()` 是忽略大小写的深度优先 Transform 搜索。
- `ResolveHead()` 以 0.5 秒、武器名和模型实例变化为重算条件；缓存字典没有统一场景清理。
- `ShouldSkipPlayer()` 排除 `HasState(1)` 或 `InFrantic`，但职业名精确为 `bossjy6001` 时强制保留。

### `BacktrackBoneUtility.BoneCache`

- 保存实体 ID、武器名、Body 实例 ID、特殊武器标志、头部 Transform、命中骨名、来源字符串和更新时间。
- `Source` 仅用于记录解析路径，当前没有外部运行逻辑读取它。

### `BacktrackManager`

- `Initialize()` 创建常驻 `BacktrackManager_Core`，由 `Main.Awake()` 调用。
- `Update()` 在功能开启且本地存活时记录敌人。采样最短间隔 `0.05s`，仅当身体、脊柱或头部任一位置相对上一条的平方位移大于 `0.0025f` 才写入。
- 环形容量固定为 128。每 `0.25s` 检查失踪实体，连续 `MissingChecks > 20` 后移除；实体数据连续无效超过 300 帧时清空全部记录。
- 本地死亡时冻结过滤参考时间；复活后恢复实时参考。关闭功能立即清记录、死亡参考和选择状态。
- `GetValidRecords()` 按新到旧返回骨骼有效且年龄不超过 `BacktrackMaxMs` 的记录，并按帧、窗口、参考时间缓存。
- `GetDisplayRecords()` 计算了 16 条抽样步长但没有使用，最终仍返回全部有效记录。
- `PrepareCommand()` 与 `SendQueuedPackets()` 是保留但当前全仓库无调用的方法；后者不会改变现有 FakeLag 发送链。
- `PrioritizeRealBody` 和 `IgnoreWallShadows` 属性只代理配置，管理器内部不读取。

### `BacktrackTraceUtility`

文件：`Feature/BacktrackTraceUtility.cs`

- 这是 Silentbot 的统一历史坐标可瞄判定，负责在当前碰撞体不位于旧坐标时继续判断路径阻挡。
- 先用 `BulletTraceNormal`、`200000f` 距离尝试直接命中当前目标；成功立即返回。
- 未直接命中且允许世界回退时，从历史目标坐标到 BulletTrace 终点执行 `IPyWorld.Trace`，mask 为 `100663299`，并排除目标和射手物理实体。
- 根据双方 `SurfaceFlags` 使用 `1260.25f`、`64f` 或 `0f` 的终点距离阈值判断路径，解决历史坐标没有当前玩家碰撞体时必然失败的问题。
- 四个 `float[3]` 缓冲为 `[ThreadStatic]`；`IPyWorld` 和 `Trace` 是全局静态缓存。Trace 从 `TraceObjectPool` 取得但不归还，世界引用换场景时也不刷新。
- 所有物理异常都返回 `false`，不改变选择状态。

## 瞄准、命令与战斗功能类

### `SkyDome.Feature.Legit.Aimbot`

文件：`Feature/Legit/Aimbot.cs`

- `Start()` 订阅 `MouseSimulator.PreInputCallback`；当前没有对应退订，组件重复创建会导致重复回调。
- `UpdateTarget()` 保留现有角度 FOV 规则：实时点和历史点都参与 `Vector3.Angle` 竞争，必须小于 `AimbotFOV/2`，严格更小的候选才覆盖当前最佳。
- 实时和历史可见性都使用普通 `BulletTrace` 并要求命中目标当前 ID；普通 Aimbot 没有使用 `BacktrackTraceUtility`。
- `GetAimPosition()` 支持 23 个实时骨骼选项；历史记录只保存头/脊柱/身体，因此历史映射为 AimPos 0/1 用头、2/3 用脊柱、其余用身体。
- 历史记录获胜时保存记录引用、捕获时间、位置和共享选择；实时点获胜或目标丢失时立即 Reset。
- `ProcessAiming()` 要求按住 AimKey 且武器槽位不大于 3。平滑模式注入一次性鼠标轴，非平滑模式直接写命令 Pitch/Yaw。
- 非平滑角度只预测本地速度；开启 SpreadPredict 且按 Mouse0 时按 Seq+1 计算扩散偏移。
- OnPreInput 外层捕获全部异常并清目标/选择，因此深层骨骼或命令空引用不会把旧回溯状态留存。

### `SkyDome.Feature.Legit.Triggerbot`

文件：`Feature/Legit/Triggerbot.cs`

- `Update()` 管理武器切换、延迟激活、狙击排除、射线和输入触发。
- 延迟模式只用于主武器且非狙击；检测到 `ShotsFired` 增长后激活，在 `TriggerbotActiveDuration` 内保持。
- 射线使用相机 forward；SpreadPredict 开启时按当前视角、Punch、Seq 和武器扩散重新计算方向。
- 射线命中后只接受敌队、存活且不处于 State(1) 的玩家，再以 `0.01s` 最小间隔注入 Mouse0 `TrueOnce`。
- 静态 `IsActive/ActivatedTime/RemainingTime` 被 `FeatureIndicator` 读取。切换武器会清激活状态并消耗一帧。

### `Silentbot`

文件：`Feature/Rage/Silentbot.cs`

- 静态骨骼顺序为 Head、Neck、双前臂、双手、Pelvis、双小腿；`checkAllbones` 默认 `false`。
- 历史映射是索引 0 用头、1 用脊柱、其他用身体。每个骨骼按记录新到旧遍历，第一个在屏幕前方且可瞄的历史点立即胜出。
- `CanAim()` 统一调用 `BacktrackTraceUtility(..., true)`，实时点、历史点和 AutoAttack HeldTarget 都使用同一回溯可瞄判定。
- 历史点成功后先 `SelectFromSilentbot`，再以历史坐标和当前速度做一帧预测、扩散与后坐力修正，写回 yaw/pitch 并返回 `true`。
- 历史点失败后继续实时骨骼；实时点胜出会清回溯选择。
- `AutoAttackInBacktrack` 保持分支瞄的是 `BacktrackEntityState.HeldTarget` 的实时骨骼，不是历史记录坐标；这是当前设计行为。
- 外层 catch 会 Reset 后重新抛出，最终由上层 Hook 路径处理。

### `AntiAim`

文件：`Feature/Rage/AntiAim.cs`

- `ExecuteAntiAim()` 是每条待发送 `UserCmd` 的命令改写总入口。
- 它计算静态、旋转或抖动 yaw，强制配置 pitch，并在修改视角后通过 `FixMove()` 旋转 forward/right，保持移动方向。
- 只有武器当前可攻击且 `CalculateWeaponSpread() >= Accurary/100` 时才调用 Silentbot。
- Silentbot 成功后，在弹匣允许且原命令未攻击时写主攻击位 `64` 或副攻击位 `512`，同时写共享 yaw/pitch 和 silent 状态。
- `CalculateWeaponSpread()` 会在命令尚未 Predicated 且武器提供逻辑时调用 `BeforeFire`，再按 WeaponType 计算 0..1 的准确度差值。
- 上下文无效分支只把原命令复制到输出，不更新共享静态角度。

### `SkyDome.Feature.NoRecoil`

文件：`Feature/NoRecoil.cs`

- `Update()` 在 NoRecoil 开启时，把当前 Punch 与上一帧 Punch 的差值乘 2 后从命令角度扣除。
- 它还旋转 `Camera.main`，并记录上一帧 Punch；关闭期间不重置记录，重新开启首帧会使用陈旧差值。
- `LateUpdate()` 在普通 Aimbot 激活、武器槽位小于 3、SmoothControl 开启时对相机局部旋转做 Slerp。
- 深层上下文、Camera 和 GameModel 没有完整判空，类本身也没有异常保护。

### `SkyDome.Feature.Resolver`

文件：`Feature/Resolver.cs`

- 默认模式只处理 `Aimbot._currentTarget`，通过字典保存每目标原始 pitch、是否应用假角和最后真实 pitch。
- 按住 ResolverKey 时把目标 `ViewPitch` 写为原始值的相反数；释放时恢复最后实际值。
- `Resolver_Random` 模式实际不是随机数，而是每 `0.05s` 对 ViewPitch 绝对值大于 30 的敌人反号。
- 随机模式遇到第一个仍在冷却的目标会 `return` 整个遍历，而不是跳过该目标。
- 字典没有场景清理，目标切换或关闭功能时也不保证恢复所有被修改实体。

### `SkyDome.Feature.Say`

文件：`Feature/Say.cs`

- `Start()` 无条件订阅 `OnPlayerHit`，击中时向本地聊天接收链注入 system 类型“命中目标”，不受 `Config.Say` 控制。
- `Update()` 在 Say 开启时每三秒向 `battle_all` 发送 `Config.SendMsg`。
- `SendServerMessage()` 反射 GameModule 的 ChatJobSystem 并调用 `SendChatInfo`，属于真实发送路径。
- `SendLocalMessage()` 构造 `ChatHistroyData` 并调用 `OnRecvChatInfo`，只影响本地显示。
- 反射调用没有统一 try/catch；发送失败后 Update 仍会更新时间进入冷却。

### `Say.MessageType`

- 枚举覆盖 VIP、全体、观察者、队伍、个人、系统、提示、战术、登录、登出、喇叭、直播和荣誉等频道。
- `GetMsgTypeString()` 将枚举映射为协议字符串，未知值回退 `system`。

### `SkyDome.Feature.SkinChanger`

文件：`Feature/SkinChanger.cs`

- `Initialize()` 扫描当前 AppDomain 中已加载程序集，反射角色、两套武器和背饰常量字符串。
- 常量列表不排序、不去重；初始化中途异常会被吞掉，已经加入的数据保留，后续重试可能重复。
- `ChangeWeapon/Character/BackAccessory/Team/Scale/HeadEnlarge/Alpha/SelfAlpha` 直接修改本地实体 `basicInfo.Current`。
- 这些修改没有服务端同步和配置持久化保证，主要是本地实体表现副作用。

### `SkyDome.Feature.WorldSettings`

文件：`Feature/WorldSettings.cs`

- `SetLowestQuality()` 把抗锯齿和纹理限制写为 `4090`，没有保存或恢复旧值。
- `UnlockFrameRate()` 把 `Application.targetFrameRate` 设置为 `-1`。
- 两个方法都由菜单按钮静态调用，类不需要被挂载为组件。

## 自动触发类

### `MikadukiSwordDkl`

文件：`Feature/AutoTrigger/MikadukiSwordDkl.cs`

- 每帧检查本地存活、当前武器名、特殊组件、EffectLevel 位、动画状态、等级和剩余时间。
- 当时间偏移位于 `-20..75` 时注入 E 键 `TrueOnce`。
- 没有配置开关、冷却或去重；条件连续满足时每帧都可能重新注入。

### `NonStopDanceAuto`

文件：`Feature/AutoTrigger/NonStopDanceAuto.cs`

- 通过当前背包主武器名判断是否包含 `nonstopdance`，再读取 `gameRule.nonStopData` 的位置和结果。
- 每帧只处理第一个未完成且不是 `_lastPressedIndex` 的单字符 A-Z，转换为 KeyCode 并注入一次。
- 所有结果完成时才把索引恢复为 `-1`；面板消失、死亡或武器不符时不会重置旧索引。
- 武器包检查整体 try/catch，失败返回 `false`。

### `WindSpiritRecall`

文件：`Feature/AutoTrigger/WindSpiritRecall.cs`

- `Update()` 不受显示开关控制；持有指定武器时每帧扫描 WIND_SPIRIT_TAG，并重建静态 `EnemiesOnPaths`。
- 每个风铃到相机形成一条路径，`BulletTraceToPlayersOnly()` 使用对象池和玩家 hitbox，只检测玩家，不检测世界墙体。
- 玩家遍历排除自己、预测实体、同队和死亡，并在需要时刷新 HitBoxBrush。
- `OnGUI()` 才受 `Config.WindSpiritPath` 控制，有敌人的路径画红色，否则青色。
- 结果字典使用精确 `Vector3` 作为键；相机空值和零距离方向没有完整保护。

### `WindSpiritRecall.EnemyOnPath`

- 保存路径上命中的 `PlayerInfo`、命中 Unity 坐标和换算后的距离。
- 当前检测每条路径最终只从单个 `TraceResult` 构造命中项，虽然容器类型是列表。

## 视觉功能类

### `WallHack`

文件：`Feature/Visuals/WallHack.cs`

- 这是玩家 ESP 聚合入口，受 `Config.WallHack` 控制；遍历敌队存活玩家并绘制残影、2D 框、血条、骨骼、射线和堆叠文字。
- 包围盒以玩家根节点和 HeadNub 的屏幕高度计算，宽度固定为高度除以 `2.3`。
- 颜色规则为当前 Aimbot 目标黄色、当前相机 forward 的 BulletTrace 命中目标时红色、其他绿色；这里的“可见”不是逐目标遮挡射线。
- 回溯残影要求 WallHack、Backtrack 和 ShowBacktrack 同时开启；记录少于两条不画，脊柱优先、身体位置 `+1` 回退，尺寸为 `8*fade+4`。
- 16x16 径向纹理和 GUI color 是静态/全局资源，纹理没有显式销毁；残影绘制异常会被吞掉。
- 风铃路径文字读取 `WindSpiritRecall.EnemiesOnPaths`，不额外检查 WindSpiritPath 显示开关。

### `WallHack.TextRule`

- 只读结构封装文字是否显示、文字内容和可选颜色函数。
- TopRules 管名字、武器、yaw、pitch；BottomRules 管 HP、距离、C4 和风铃路径。

### `BoundingBox3D`

文件：`Feature/Visuals/BoundingBox3D.cs`

- 同时要求 Show3DBox 和 WallHack，使用约 40 个关键骨骼构造世界轴对齐 `Bounds`。
- 不读取 Renderer 体积，也不加 padding；姿势和旋转只通过骨点最小/最大值反映。
- 把 Bounds 八个角投影后画 12 条青色边；任一角在相机后方就放弃整盒。
- 屏幕缩放只按高度比例处理，未考虑 viewport 偏移。

### `Chams`

文件：`Feature/Visuals/Chams.cs`

- 初始化或获取主相机上的 `cakeslice.OutlineEffect`，每帧配置颜色、线宽、强度和深度参数。
- 对敌队存活角色的 SkinnedMeshRenderer 添加 Outline；关闭 Chams 或角色死亡时删除。
- 删除逻辑会销毁 Renderer 上任何现有 Outline，不区分是否由本类创建。
- 与 ItemOutline 共用同一个 OutlineEffect；两个组件每帧写颜色槽，结果依赖 Unity Update 顺序。

### `Crosshair`

文件：`Feature/Visuals/Crosshair.cs`

- 只在 ShowCrosshair 开启、本地存活、当前武器为 WeaponType 5 且未开镜时绘制。
- 使用 `ImmediateRenderer.DrawCrosshair` 在屏幕中心画粉色十字，不替换游戏自身准星。

### `C4Timer`

文件：`Feature/Visuals/C4Timer.cs`

- 读取 `gameRule.c4State`；普通模式最大时间 35 秒，RaceType 8 使用 45 秒。
- 显示剩余一位小数秒和顶部 200x8 进度条，14 秒和 7 秒作为颜色阈值。
- 时间和进度都钳制到非负/0..1，但不验证服务端时间单位是否变化。

### `AntiAimIndicator`

文件：`Feature/Visuals/AntiAimIndicator.cs`

- 只在本地存活且 AntiAim 开启时显示。
- 仅识别 yaw 为 `90`、`-90` 或 `-180`；左右使用亮/暗三角，`-180` 两侧都暗。
- 其他自定义 yaw 值不会显示方向提示。

### `DamageDisplay`

文件：`Feature/Visuals/DamageDisplay.cs`

- 每帧缓存敌人 HP，HP 相比上一帧下降超过 `0.5` 时在头顶创建数字。
- 数字持续 1.5 秒，按每秒 50 Unity 单位上升并线性淡出。
- 关闭显示时只清活动数字，不清 `_lastHpCache`；重新开启会拿旧 HP 比较。
- 缓存没有移除离场实体 ID，ID 复用可能产生错误差值。

### `DamageDisplay.DamageNumber`

- 保存伤害、世界起点、创建时间、持续时间和上升速度。
- `GetAlpha/GetCurrentPosition/IsExpired` 都直接以 `Time.time` 计算。

### `FeatureIndicator`

文件：`Feature/Visuals/FeatureIndicator.cs`

- 通过静态规则数组收集当前激活指示器，目前只有延迟 Triggerbot 倒计时和 FakeLag choke 数。
- 第一人称绘制在屏幕左侧中部；第三人称同时要求配置开关和 `Menu.forceThirdPerson`，并跟随本地 Spine。
- 每次 OnGUI 都新建活动列表；规则 getter 直接读取其他模块静态状态。

### `FeatureIndicator.IndicatorRule`

- 只读结构保存 `IsEnabled`、`GetText` 和 `GetColor` 三个委托。
- 未提供颜色函数时回退白色。

### `ItemESP`

文件：`Feature/Visuals/ItemESP.cs`

- 遍历 `SceneWeapon`，把游戏坐标转 Unity 坐标，在屏幕前方绘制白点、武器名和距离。
- 尝试通过 `LanguageUtils.GetWeaponCnName` 翻译名称，翻译异常静默回退原名。
- `showText` 内再次判断 ShowItemESP，但方法只有开关开启时才调用，因此关闭分支不可达。

### `ItemOutline`

文件：`Feature/Visuals/ItemOutline.cs`

- 每帧扫描 SceneWeapon 的 Unity 模型，为 MeshRenderer/SkinnedMeshRenderer 添加颜色槽 1 的 Outline。
- `_outlinedItems` 按场景物品 ID 去重；即使模型尚无 Renderer，也会标记完成，后续模型加载不会重试。
- 删除和清理会销毁物品层级上的全部 Outline，不区分所有权。
- OutlineEffect 与 Chams 共用；关闭开关时不会恢复共享颜色槽。

### `MoveEntityESP`

文件：`Feature/Visuals/MoveEntityESP.cs`

- 遍历 `SceneObjectMatcher.MoveObject`，对有名称的移动实体绘制青色点、名称和距离。
- 只检查深度大于零，不检查完整屏幕边界。

### `Radar`

文件：`Feature/Visuals/Radar.cs`

- 以屏幕中心、半径 `167.25` 绘制圆形雷达。
- 敌人相对相机位置按本地 yaw 旋转，使用固定比例 `Screen.height * 2.4E-07 * 167.25`，再钳制到圆内。
- 标记包含圆点和敌方 yaw 箭头；敌人根 Transform 缺失会产生空引用。

### `SceneBuffESP`

文件：`Feature/Visuals/SceneBuffESP.cs`

- 遍历 SceneBuff，对非空 BufName 绘制品红点、原始名称和距离。
- 它不翻译 Buff 名称，也不按类型过滤。

### `SpectatorList`

文件：`Feature/Visuals/SpectatorList.cs`

- 读取 `battleRoom.playerInfo.ObserverList`，在屏幕左侧垂直居中绘制“观战”和名字列表。
- 只受 ShowWatcher 控制，访问 `Contexts.sharedInstance.battleRoom` 前没有空条件保护。

### `SpeedDashboard`

文件：`Feature/Visuals/SpeedDashboard.cs`

- 从本地 Move.Velocity 计算水平速度，并从 `BasePyPlayerAdapter.GetMaxSpeed()` 得到逻辑上限。
- 物理上限定义为逻辑上限的 `1.25` 倍；超过逻辑上限变青，接近物理上限变粉。
- 记录峰值三秒，之后向当前速度插值衰减；圆弧按 20 段用 IMGUI 纹理绘制。
- Start 创建的 1x1 Texture2D 没有 OnDestroy 清理。

### `SkyDome.Feature.Visuals.Trace`

文件：`Feature/Visuals/Trace.cs`

- 监控当前武器 `ShotsFired` 增长，每次增长以主相机 forward 做 Unity Physics.Raycast，创建一秒黑色轨迹。
- 轨迹是客户端当前相机方向，不读取 Silentbot、Backtrack 或服务器命中点。
- `Update()` 无论 ShowTracers 是否开启都检测新射击和维护列表；OnGUI 才门控显示。
- `CreateTracer()` 的相机空判断使用 `&&`，某些相机为空/武器条件下仍会继续解引用相机。

### `Trace.TracerData`

- 保存起点、终点、颜色、创建时间、持续时间和射击索引。
- `IsExpired()` 只按 `Time.time` 判断；ShotIndex 当前不用于去重，去重由组件的 `_lastShotIndex` 完成。

## 渲染封装类

### `SkyDome.Render.ImmediateRenderer`

文件：`Render/ImmediateRenderer.cs`

- 静态缓存 `Hidden/Internal-Colored` Material，配置透明混合、关闭剔除和 ZWrite。
- 提供框、线、文字、圆、三角、Polygon、箭头、角框、扇区、准星、命中点和世界空间线段。
- 线宽大于 1 时用四边形模拟；填充 Polygon 使用从第一个点展开的三角扇，只适用于凸多边形。
- `DrawString()` 只按 fontSize 重建单个 GUIStyle，颜色在绘制时临时修改。
- 大多数 2D 方法使用 GL PixelMatrix；圆轮廓使用 Ortho；调用者必须理解 IMGUI Y 轴与 GL 坐标的差异。
- Material 和 GUIStyle 没有显式销毁；`DrawLinearTracer()` 在 EnsureMaterial 后没有再次判空。

### `SkyDome.Render.OverLay`

文件：`Render/OverLay.cs`

- `DrawVerticalHealthBar()` 根据目标 Rect 绘制背景、红绿插值填充和黑色边框。
- `DrawSkeleton()` 先缓存全部骨骼屏幕坐标，再按固定人体骨链连线；特殊职业 `rpg_by_parasitism` 用 Bone05 画头点。
- 普通头部用 Head 与 Head 第一个子节点计算圆心和半径，没有子节点时不会画头圆。
- 类声明为普通类，但公开功能全部静态。

## Hook 业务类

### `HookManager`

文件：`MonoMod_Hook/HookManager.cs`

- 这是具体 Hook 注册和替换中心，维护已安装 `MethodHook` 列表、FakeLag 队列和共享命令 writer。
- `StartHook()` 注册截图、上传、本地化、速度/命令、相机、朝向、GetUserCmdBytes、Fire、FPS、UDP、命中和闪光 Hook。
- 聊天、QuickRuntimeConsole、CameraOwnerYaw 和 ControlEntityYaw 的注册当前被注释或没有调用。
- 截图 Hook 直接阻止原抓取；上传 Hook 构造空白图数据。相机 Hook 处理第三人称和共享 silent 角度。
- `GetUserCmdBytes_Hook()` 对第一条命令先执行 Bhop 和 AntiAim；有回溯选择时只序列化 `RenderTime - AgeMilliseconds` 并设 `PredicatedOnce=false`，不修改命令对象的 RenderTime。
- 后续命令没有独立 RenderTime 字段，只写 interval、move、buttons、装备和角度。
- `Fire_Hook()` 按原 yaw/pitch/punch/spread 计算 BulletTrace，有选择时只覆盖结果 EntityId。
- `SendUdpData_Hook()` 独立实现 FakeLag；它不会调用 BacktrackManager 保留的 `SendQueuedPackets()`。
- `HitPlayerHandler_Hook()` 在原逻辑后触发 GlobalEvents；NoFlash 通过反射隐藏 ViewModel，失败时回退原逻辑。

### `HookManager.OriginalProxies`

- 包含与目标方法同签名的 NoInlining/NoOptimization 空壳。
- `CodePatcher` 安装时把目标原始指令复制到代理方法并补跳回，因此 Hook 调用代理才会执行原逻辑。
- 如果 Hook 未成功安装，代理只会执行源码中的空实现或返回默认值。

## Hook 底层类

### `MonoHook.MethodHook`

文件：`MonoMod_Hook/MethodHook.cs`

- 保存目标、替换、代理 MethodBase 和解析后的原生地址。
- `Install()` 先通过 HookPool 去重，再解析 Mono JIT 或 IL2CPP 方法指针，选择 CodePatcher 并应用跳转。
- Mono 使用 `MethodHandle.GetFunctionPointer()`；IL2CPP 通过反射对象内存布局取得 `methodPointer`。
- 架构选择依据 ARM、指针宽度和距离，x64 小于 2GB 使用 near，否则 far。
- 地址解析失败、抽象方法、Thumb 指令和空指针在 Debug/Release 下处理并不完全一致。
- 类没有锁；卸载后保留 `_codePatcher`，再次安装的状态路径需要谨慎。

### `MethodHook.__ForCopy`

- 私有 Pack=1 结构，用于把 IL2CPP 反射对象内存中的 MethodBase 位置映射出来。
- 它依赖特定运行时对象布局，不是跨 Unity/IL2CPP 版本的稳定 ABI。

### `MonoHook.HookPool`

文件：`MonoMod_Hook/HookPool.cs`

- 以目标 MethodBase 为键保存当前 Hook，同目标新增时先卸载旧实例。
- 支持按方法获取/移除、卸载全部、按 tag 卸载和返回快照列表。
- `UninstallAll/UninstallByTag` 先复制 Values，避免遍历时由 Uninstall 修改字典。
- 字典无锁，不适合多个线程同时安装或卸载。

### `MonoHook.HookUtils`

文件：`MonoMod_Hook/HookUtils.cs`

- 提供裸内存复制、JIT byte[] 写入、页面 RWX、指令缓存刷新、页对齐和十六进制输出。
- 当前源码强制 Windows 条件编译，页面权限通过 VirtualProtect 改为 ExecuteReadWrite，返回值和旧权限未恢复。
- Intel/AMD 路径不建立自定义 FlushICache delegate；ARM 路径使用内置机器码。
- `MemCpy_Jit()` 固定 `src[0]`，空数组会越界。

### `HookUtils.Protection`

- 对应 Win32 页面保护标志，主要使用 `PageExecuteReadWrite`。

### `HookUtils.MmapProts`

- 对应 Unix `mprotect` 标志；由于当前 Windows 条件编译路径，它不会参与主要构建。

### `MonoHook.CodePatcher`

文件：`MonoMod_Hook/CodePatcher.cs`

- 抽象补丁器负责计算完整指令覆盖长度、备份目标头、修改内存权限、写目标跳转和构建代理 trampoline。
- `ApplyPatch()` 先备份，再把目标跳到 replacement；代理存在时复制原始头并跳回目标剩余地址。
- `RemovePatch()` 只恢复目标头，不恢复代理方法内容。
- 类不会重定位被复制指令中的 RIP-relative/relative 操作数，代理正确性依赖目标头指令形式。

### `CodePatcher_x86`

- 使用 5 字节 `E9 rel32` 跳转。
- 目标与替换必须处于有符号 32 位相对位移范围。

### `CodePatcher_x64_near`

- 直接复用 x86 的 5 字节相对跳转，用于 2GB 范围内的 x64 地址。

### `CodePatcher_x64_far`

- 使用 12 字节 `mov rax, imm64; push rax; ret` 绝对跳转。
- 该序列会使用 RAX，正确性依赖补丁点的调用约定和指令上下文。

### `CodePatcher_arm32_near`

- 生成 4 字节 ARM B 指令，并校验 near 距离。
- MethodHook 明确拒绝 Thumb 地址，因此只覆盖 ARM 模式。

### `CodePatcher_arm32_far`

- 使用 8 字节 LDR PC literal 绝对跳转，适用于超出 near 范围的地址。

### `CodePatcher_arm64_near`

- 生成 4 字节 ARM64 B 指令，要求约 ±128MB 范围。

### `CodePatcher_arm64_far`

- 类保留 20 字节 far 模板接口，但 `GenJmpCode()` 当前抛 `NotImplementedException`。
- MethodHook 当前不会选择它，不能视为可用路径。

### `DotNetDetour.LDasm`

文件：`MonoMod_Hook/LDasm.cs`

- 这是 x86/x64 长度反汇编器，并提供 ARM 架构和 IL2CPP 探测。
- `SizeofMinNumByte()` 连续解析完整指令，直到覆盖跳转所需字节；ARM 路径按 4 字节向上对齐。
- opcode 表描述前缀、ModRM、SIB、位移、立即数和相对寻址，单条 x86 指令长度超过 15 会标无效。
- `IsIL2CPP()` 通过本方法是否有 MethodBody/IL 字节推断并缓存。
- `CalcARMThumbMinLen()` 和 `IsiOS()` 当前全仓库无调用；MethodHook 也拒绝 Thumb。
- 代码只计算长度，不重写 trampoline 中的相对地址。

### `LDasm.ldasm_data`

- 保存 flags、REX、ModRM、SIB、opcode、位移和立即数的偏移/长度。
- 它是单条指令解析的中间状态，不跨调用持久化。

## 控制台与资源类

### `SkyDome.ConsoleManager`

文件：`Console/ConsoleManager.cs`

- 是常驻单例组件，重复实例会销毁自身。
- 仅在 `Debug_Log` 条件下调用 Win32 `AllocConsole`，设置输入/输出代码页为 UTF-8，并把 Console.Out/Error 重定向到标准句柄。
- `ShowConsole/HideConsole/ClearConsole` 在非 Debug_Log 构建为空操作。
- `OnDestroy()` 释放 writer 和控制台；`OnApplicationQuit()` 直接再次调用该清理逻辑，状态检查避免重复 FreeConsole。

### `SkyDome.RuntimeConsole.ExternalConsoleWindow`

文件：`Console/ExternalConsoleWindow.cs`

- WinForms 窗口包含输出、输入、命令历史、自动补全和代码编辑器。
- 支持 `cd/clear/cmp/code/find/invoke/show/var`，实际执行委托游戏的 `CommandFactory` 或当前 LockCommand。
- 输出超过 500 行时保留末尾约 400 行；历史命令去重后追加。
- 自动补全可列出当前对象的公开属性/字段，最多显示 10 项。
- 执行异常写入窗口，不向外抛出；它运行在独立 STA UI 线程。

### `SkyDome.RuntimeConsole.QuickRuntimeConsoleGUI`

文件：`Console/QuickRuntimeConsoleGUI.cs`

- 如果被手动挂载，`Start()` 创建后台 STA 线程并运行 `ExternalConsoleWindow`。
- `Update()` 观察游戏 `ConsoleStatus.ConsoleOpened`，通过 WinForms `Invoke` 显示窗口。
- 应用退出时关闭窗口；没有 Join UI 线程。
- 当前 `Main` 注册和 QuickRuntimeConsole Hook 均被注释，默认不会实例化。

### `SkyDome.Properties.Resources`

文件：`Properties/Resources.Designer.cs`

- Visual Studio 自动生成的强类型资源包装器。
- 懒加载 `ResourceManager`，支持覆盖 `Culture`。
- 当前公开两个 byte[] 资源：`menu_font` 和 `ProggyTiny`。
- 应修改 `.resx` 而不是直接维护生成文件。

## 非运行类型源码

### `Feature/AIChatBot.cs`

- 整个 `AIChatBot` 及拟定的请求/响应嵌套类型都被注释，程序集没有这些类型。
- 草稿描述了 HTTP Chat Completions、20 条历史、3 秒冷却、15 秒超时和通过 Say 回写聊天，但当前无任何运行副作用。

### `Feature/Visuals/BoneDebug.cs`

- 整个 `BoneDebug` 类被注释，`Main` 中注册也被注释。
- 草稿意图是遍历敌人全部骨骼，绘制黄色点和骨名；当前程序集没有该类型。

### `Properties/AssemblyInfo.cs`

- 文件只包含程序集元数据属性，没有类或运行逻辑。

## 关键数据流

### 回溯记录到射击

```text
BacktrackManager.Update
  -> BacktrackTargetHistory.Records
  -> GetValidRecords（新到旧、窗口过滤）
  -> Aimbot 或 Silentbot 选择历史点
  -> BacktrackAimState
  -> AntiAim.ExecuteAntiAim
       -> Silentbot true 时写攻击位
  -> HookManager.GetUserCmdBytes_Hook
       -> 首节点 RenderTime - AgeMilliseconds
       -> PredicatedOnce = false
  -> HookManager.Fire_Hook
       -> 原方向/扩散 BulletTrace
       -> 覆盖 TraceResult.EntityId
```

Unity 客户端物理世界只包含当前玩家碰撞体。`BacktrackTraceUtility` 的世界 Trace 用于判断历史坐标路径是否被墙体阻挡，不会创建历史碰撞体。服务器是否接受具体历史时间，仍取决于实际协议和对局环境。

### Hook 安装与原方法代理

```text
HookManager.StartHook
  -> CreateMonoHook
  -> MethodHook.Install
  -> HookPool.AddHook
  -> 解析 target/replacement/proxy 地址
  -> LDasm 计算完整覆盖长度
  -> CodePatcher 备份目标头
  -> target 跳 replacement
  -> proxy = 原始头 + 跳回 target 尾部
```

### 输入注入

```text
游戏 InputCollector
  -> MouseSimulator.AnyKey
       -> PreInputCallback
            -> Aimbot.OnPreInput
  -> GetAxis/GetKey/GetMouseButton
       -> 消费一次性或持续模拟状态

Triggerbot / AutoTrigger / Aimbot
  -> ForceMouseButton / ForceKey / ForceAxisDelta
```

## 构建

环境要求：

- Windows
- Visual Studio 2019+ 或兼容的 MSBuild
- .NET Framework 4.8 Developer Pack
- 完整的 `引用/` 本地 DLL

推荐验证命令：

```powershell
dotnet msbuild .\SkyDome.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=AnyCPU /v:minimal
dotnet msbuild .\SkyDome.csproj /t:Rebuild /p:Configuration=Release /p:Platform=x64 /v:minimal
```

输出位置：

- `Debug|AnyCPU`：`bin/Debug/SkyDome.dll`
- `Debug|x64`：`bin/x64/Debug/SkyDome.dll`
- `Release|AnyCPU`：`bin/Release/SkyDome.dll`，项目文件仍指定 `PlatformTarget=x64`
- `Release|x64`：`bin/x64/Release/SkyDome.dll`

## 已知实现边界

- 多个组件直接访问深层游戏组件，空值保护并不统一。
- `Main.Init()` 的 Unity API 调用来自后台线程。
- 多个静态缓存没有场景切换清理，例如 Backtrack 骨骼缓存、Resolver 字典和渲染资源。
- Chams 与 ItemOutline 共享并竞争同一个 OutlineEffect。
- Hook 安装、HookPool、输入字典和 Backtrack 的非线程局部对象没有统一锁。
- Hook trampoline 复制相对寻址指令时不做重定位。
- 普通 Aimbot 历史可见性仍要求命中当前实体 ID；只有 Silentbot 使用世界 Trace 历史路径判定。
- FakeLag 与 Backtrack 的保留发包辅助没有连接。
- Release 构建中的 `Debug_Log` 条件不同，错误路径通常静默。

## 免责声明

本项目仅供学习、研究和授权测试使用。请勿在未经授权的环境中运行、传播或部署构建产物。
