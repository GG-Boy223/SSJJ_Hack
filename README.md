# SSJJ_Hack 项目说明

> 清理修复版：Hook 底层已切换到 `MonoMod_Hook` 方案，当前工程结构与 README 已重新同步。

## 项目概述

SSJJ_Hack 是一个基于 C# / .NET Framework 4.8 的 Unity 运行时研究工程。项目以组件化方式组织配置、实体状态、渲染、输入模拟、运行时控制台以及 Hook 相关逻辑，适合在授权环境中进行 Unity/Mono/IL2CPP 运行时行为研究与调试。

本仓库只用于学习、研究和授权测试。请勿在未授权环境中使用或分发构建产物。

## 当前状态

| 项目项 | 当前说明 |
| --- | --- |
| 工程类型 | C# Class Library |
| 目标框架 | .NET Framework 4.8 |
| 主要平台 | Windows x64 |
| 语言版本 | C# 8.0 |
| Unsafe | 已启用 |
| Hook 底层 | `MonoMod_Hook/MethodHook.cs` + `CodePatcher` |
| 本地依赖 | `引用/` 目录中的 Unity 与游戏相关 DLL |
| 入口组件 | `Loader.Load()` 创建并挂载 `Main` |

## 架构总览

```text
SSJJ_Hack
├── Loader.cs                    # 初始化/卸载入口，负责创建 SkyDome_HookObject
├── Main.cs                      # 运行时组件注册，启动输入替换与 HookManager
├── RuntimeState.cs              # 运行时状态共享
├── Cfg/                         # 配置定义、配置读写、菜单界面
├── Console/                     # 外部控制台与运行时控制台
├── Engine/                      # 输入模拟封装
├── Entity/                      # 本地玩家与实体数据追踪
├── Feature/                     # 功能组件，按 Legit/Rage/Visuals/AutoTrigger 等目录拆分
├── MonoMod_Hook/                # 新 Hook 底层：方法地址解析、跳板、代码补丁与 Hook 池
├── Render/                      # GL/IMGUI 绘制封装
├── Resources/                   # 字体等嵌入资源
├── Utilities/                   # 数学、视口、玩家、事件等通用工具
├── Properties/                  # 程序集属性与资源描述
└── 引用/                        # 本地 DLL 依赖
```

## 初始化流程

```text
Loader.Load()
  ├─ 创建或复用 SkyDome_HookObject
  ├─ 确保 Main 组件存在
  └─ DontDestroyOnLoad 保持跨场景生命周期

Main.Awake()
  └─ 启动高优先级 Init 线程

Main.Init()
  ├─ 创建 HookObject 防止重复初始化
  ├─ 注册 PlayerUpdate、Visuals、Legit、AutoTrigger、菜单、控制台等组件
  ├─ InputCollector 替换为 MouseSimulator
  └─ HookManager.StartHook() 安装运行时 Hook
```

## Hook 底层说明

当前 Hook 底层集中在 `MonoMod_Hook/`：

| 文件 | 作用 |
| --- | --- |
| `MethodHook.cs` | 管理目标方法、替换方法、代理方法，解析 Mono/IL2CPP 下的方法原生地址 |
| `CodePatcher.cs` | 备份目标方法头部，写入跳转指令，生成代理方法跳板 |
| `HookManager.cs` | 集中注册具体 Hook，并保存已安装的 `MethodHook` 实例 |
| `HookPool.cs` | 防止同一目标方法重复 Hook，支持按方法或标签卸载 |
| `HookUtils.cs` | 内存拷贝、内存权限切换、指令缓存刷新、调试输出辅助 |
| `LDasm.cs` | 计算可覆盖指令长度，避免截断原始指令 |

核心流程：

1. 通过反射定位目标方法、替换方法和代理方法。
2. 获取方法 JIT/IL2CPP 后的原生地址。
3. 根据架构选择 x86/x64/ARM 对应的跳转补丁。
4. 备份目标方法头部指令。
5. 将目标方法跳转到替换方法。
6. 如提供代理方法，则把原始指令复制到代理方法并补跳回原函数后续地址。
7. 卸载时恢复目标方法头部备份。

Hook 注意事项：

- 代理方法必须保持与目标方法兼容的签名。
- 代理方法应使用 `MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining`，避免 JIT 优化影响地址稳定性。
- 新增 Hook 时优先通过 `CreateMonoHook` 注册，避免绕过统一异常处理和 Hook 列表管理。
- ARM64 远距离跳转类当前保留接口，未作为 Windows x64 主路径使用。

## 功能模块

| 模块 | 目录/文件 | 说明 |
| --- | --- | --- |
| 配置系统 | `Cfg/Config.cs`, `Cfg/ConfigManager.cs` | 静态字段配置、Key-Value 文件读写、多配置管理 |
| 菜单系统 | `Cfg/Menu.cs` | 运行时 IMGUI 菜单与功能开关 |
| 实体追踪 | `Entity/PlayerUpdate.cs`, `Entity/PlayerInfo.cs` | 本地玩家与实体列表状态更新 |
| 输入模拟 | `Engine/MouseSimulator.cs` | 实现游戏输入接口替换 |
| 合法类功能 | `Feature/Legit/` | 自瞄、自动扳机等组件 |
| Rage 类功能 | `Feature/Rage/` | 角度与命令相关组件 |
| 视觉类功能 | `Feature/Visuals/` | ESP、雷达、Chams、准星、伤害显示等绘制组件 |
| 自动触发 | `Feature/AutoTrigger/` | 特定机制的自动触发逻辑 |
| 渲染封装 | `Render/` | 即时绘制、覆盖层绘制辅助 |
| 公共工具 | `Utilities/` | 数学计算、视口转换、玩家辅助、全局事件 |

## 构建环境

- Windows
- Visual Studio 2019+ 或兼容 MSBuild
- .NET Framework 4.8 Developer Pack
- C# 8.0 支持
- `引用/` 目录中的本地 DLL 依赖保持完整

## 构建步骤

1. 使用 Visual Studio 打开 `SkyDome.sln`。
2. 确认 `SkyDome.csproj` 中的 `<HintPath>` 仍指向仓库内的 `引用/` 目录。
3. 优先使用 `Debug|x64` 或 `Release|x64` 配置构建。
4. 构建输出位于对应配置的 `bin/` 目录。

命令行构建示例：

```powershell
msbuild .\SkyDome.csproj /p:Configuration=Debug /p:Platform=x64
msbuild .\SkyDome.csproj /p:Configuration=Release /p:Platform=x64
```

## 配置文件

运行时配置由 `ConfigManager` 管理：

- 配置目录：`Application.persistentDataPath/SkyConfigs`
- 存储格式：每行一个 `字段名=值`
- 支持类型：`bool`、`int`、`float`、`string`、`KeyCode`
- 配置字段来源：`Cfg/Config.cs` 中的 public static 字段

## 免责声明

本项目仅供学习、研究和授权测试使用。使用者应遵守相关法律法规以及目标软件的服务条款，不得用于未经授权的环境。
