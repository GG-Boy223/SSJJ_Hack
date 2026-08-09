using Assets.Sources.Info.Weapon;
using Assets.Sources.Info.Weapon.WeaponInfo;
using physics;
using physics.trace;
using SkyDome.Entity;
using SSJJMath;
using SSJJPhysics;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// 该模块把游戏真实 BulletTrace 结果转换为 UI / 自瞄都能复用的穿墙观测数据。
namespace SkyDome.Utilities
{
    // 预览轨迹分类。这里不直接表示“能不能打”，而是描述枪线在当前帧的几何关系。
    internal enum TelemetryTraceKind
    {
        // 当前帧没有得到可用的目标/枪线关系，外层会把它当作无效预览。
        Unknown,
        // 枪线直接命中目标，中间没有场景墙阻挡。
        Direct,
        // 游戏枪线告诉我们目标被阻挡，需要继续做厚度采样。
        Wall
    }

    // 单个障碍厚度的解析状态。区分精确厚度、只能拿到下界，以及直接撞到实体的情况。
    internal enum TraceThicknessStatus
    {
        // 没法确认这段障碍的厚度。
        Unknown,
        // 已经探测到这段障碍的精确厚度。
        Exact,
        // 一直到目标前都没走出阻挡，因此只有厚度下界。
        BlockedUntilTarget,
        // 这次阻挡本质上是实体命中，不应该算作场景墙。
        EntityObstacle
    }

    // 单次“穿过一个障碍”的探测结果。单位仍是游戏内部 Trace 单位，外层再统一换算为米。
    internal struct TraceThicknessProbeResult
    {
        // 当前障碍最终被识别成哪一种厚度状态。
        public TraceThicknessStatus Status;
        // 已经精确探测到的厚度值；只有 Status=Exact 时才可信。
        public float ThicknessUnits;
        // 探针没能在到达目标前离开阻挡时，这里保存“至少有这么厚”的下界。
        public float ThicknessLowerBoundUnits;
        // 为 true 时说明后续已经没有继续追踪下一段障碍的意义。
        public bool ShouldStopTracing;
        // 记录 Trace 起点是否已经落在阻挡体内部，便于日志解释异常厚度。
        public bool StartedInSolid;
        // 记录整段 Trace 是否都处在 solid 内部，这通常意味着目标整段被实体包住。
        public bool AllSolid;

        // 只有拿到精确厚度且厚度大于极小值时，外层才会把这段墙计入“精确可判定”。
        public bool HasExactThickness
        {
            get { return Status == TraceThicknessStatus.Exact && ThicknessUnits > 0.01f; }
        }

        // 只要能得到一个有效下界，就可以在日志中展示“至少这么厚”。
        public bool HasLowerBound
        {
            get { return ThicknessLowerBoundUnits > 0.01f; }
        }
    }

    // 整条枪线障碍汇总结果。除了总厚度，还记录是否中途被实体截断。
    internal struct TraceObstacleCollectionInfo
    {
        // 所有已精确解析障碍厚度的米制总和。
        public float TotalThicknessMeters;
        // 包含“至少厚度”的保守总和，用于展示 >= 公式。
        public float TotalThicknessLowerBoundMeters;
        // 只要其中任意一段障碍没能拿到精确出口，这个标记就会被置为 true。
        public bool HasUnresolvedThickness;
        // 为 true 说明追踪不是被场景墙结束，而是被玩家/实体阻挡结束。
        public bool EndedByEntityHit;
        // 若最后被实体截断，记录对应实体 id 方便日志定位。
        public int EndedByEntityId;
    }

    // 单个障碍的可视化样本，用于 UI 文本和日志串联。
    internal struct TraceObstacleSample
    {
        // 从开枪起点到命中该障碍入口位置的距离。
        public float DistanceMeters;
        // 该障碍的精确厚度；没有精确值时保持 0。
        public float ThicknessMeters;
        // 该障碍的厚度下界；用于展示 >=X.XXm 的情况。
        public float ThicknessLowerBoundMeters;
        // 当前障碍样本对应的厚度状态。
        public TraceThicknessStatus ThicknessStatus;
        // 记录 Trace 起点是否已经落在阻挡体内部，便于日志解释异常厚度。
        public bool StartedInSolid;
        // 记录整段 Trace 是否都处在 solid 内部，这通常意味着目标整段被实体包住。
        public bool AllSolid;
        // 游戏 Trace 返回的 surface 标志，主要用于日志侧排查材质/碰撞类型。
        public int SurfaceFlags;
        // 游戏 Trace 返回的 contents 标志，辅助区分不同阻挡内容。
        public int Contents;
        // 非 0 说明这次阻挡命中了实体而不是纯场景几何。
        public int EntityId;
        // 障碍入口点对应的 Unity 世界坐标，便于后续调试可视化。
        public Vector3 HitPoint;
    }

    // 统一维护“开枪前环境观测”。DrawMain 用它展示厚度，自瞄也用同一套结果做目标放行判定。
    public static class PenetrationDamageTelemetry
    {
        // 当前预览目标的刷新节流，避免每帧都做完整穿墙采样。
        private const float PreviewObservationIntervalSeconds = 0.25f;
        // 单条枪线最多保留 8 段障碍，既够展示又能控制字符串和 Trace 成本。
        private const int MaxObstacleSummaryEntries = 8;
        // 只有总厚度精确且不超过该阈值时，穿墙目标才允许进入自瞄更新。
        private const float ThinWallAutoAimMaxThicknessMeters = 1.5f;
        // 直接复用游戏子弹 Trace 的内容掩码，确保采样规则和真实弹道一致。
        private const int BulletTraceMask = 100663299;
        // 命中障碍后先向内推进一点，避免反复停在同一个接触面上。
        private const float WallProbeEnterOffsetUnits = 1f;
        // 在障碍内部找出口时的步进长度；步进越小越精确但越耗性能。
        private const float WallProbeStepUnits = 2f;
        // 每次探针自身的 Trace 长度，只用于判断当前位置是否已离开阻挡体。
        private const float WallProbeLengthUnits = 0.5f;
        // 对任意世界点做墙厚探测时，目标点需要沿枪线再向后延长一小段，避免“刚好打在墙皮上”导致厚度为 0。
        private const float WorldPointProbeExtendUnits = 256f;

        // 缓存、临时数组和日志去重状态都挂在静态字段上，统一用这把锁保护。
        private static readonly object s_sync = new object();
        // 零尺寸 point trace 的 mins；和 maxs 一起让游戏走“点射线”而不是体积扫掠。
        private static readonly float[] PointTraceMins = new float[3];
        // 零尺寸 point trace 的 maxs。
        private static readonly float[] PointTraceMaxs = new float[3];
        // 复用的障碍采样缓冲区，避免每次观测都 new 新数组。
        private static readonly TraceObstacleSample[] s_traceObstacleBuffer = new TraceObstacleSample[MaxObstacleSummaryEntries];
        // 按目标实体 id 缓存最近一次墙体观测结果，供绘制和选人共用。
        private static readonly Dictionary<int, TargetWallObservation> s_targetWallObservations = new Dictionary<int, TargetWallObservation>();

        // 上一次预览日志对应的武器键，用于判断是否需要刷新预览。
        private static string s_previewWeaponKey;
        // 当前预览绑定的目标 id。
        private static int s_previewTargetId = -1;
        // 当前预览绑定的目标血量；血量变化也会触发日志刷新。
        private static float s_previewTargetHp = -1f;
        // 记录当前预览 Trace 最终命中的实体 id，用来区分“同目标但 Trace 结果变了”的情况。
        private static int s_previewTraceEntityId = -1;
        // 当前预览轨迹类型。
        private static TelemetryTraceKind s_previewTraceKind = TelemetryTraceKind.Unknown;
        // 下一次允许重采当前预览目标的时间点。
        private static float s_nextPreviewObservationTime = -1f;
        // 上一条已输出日志的完整文本，用于去重，避免刷屏。
        private static string s_lastPreviewLogKey;
        // 当前预览目标最终展示在界面上的墙体厚度字符串。
        private static string s_previewWallThicknessText;

        // 由 DrawMain 每帧驱动。它只负责维护“当前预览目标”的日志/UI状态，不负责全敌人缓存。
        public static void Tick(PlayerInfo currentTarget, ShotTraceInfo crosshairTraceInfo, ShotTraceInfo targetTraceInfo)
        {
            try
            {
                PlayerInfo localPlayer = PlayerUpdate.LocalEntity;
                WeaponEntity currentWeapon = Contexts.sharedInstance?.weapon?.currentWeaponEntity;
                // 本地玩家无效、已死亡、或者当前武器不是需要观测的枪械时，直接清空预览状态。
                if (localPlayer == null || localPlayer.IsDead || !TryCreateWeaponSnapshot(currentWeapon, out WeaponSnapshot weaponSnapshot))
                {
                    lock (s_sync)
                    {
                        ResetPreviewObservationStateCore();
                    }
                    return;
                }

                lock (s_sync)
                {
                    UpdatePreviewObservation(weaponSnapshot, currentWeapon, currentTarget, crosshairTraceInfo, targetTraceInfo, Time.realtimeSinceStartup);
                }
            }
            catch (Exception)
            {
            }
        }

        // 预热所有敌人的墙体缓存。选人逻辑会先调用它，避免“未缓存就选不中”的死循环。
        public static void RefreshEnemyObservations()
        {
            try
            {
                lock (s_sync)
                {
                    RefreshEnemyObservationsCore(Time.realtimeSinceStartup);
                }
            }
            catch (Exception)
            {
            }
        }

        // 根据当前自瞄目标和准星 Trace 刷新预览结果。只有目标/武器/血量/Trace 变化或超时才会重采。
        private static void UpdatePreviewObservation(
            WeaponSnapshot weaponSnapshot,
            WeaponEntity currentWeapon,
            PlayerInfo currentTarget,
            ShotTraceInfo crosshairTraceInfo,
            ShotTraceInfo targetTraceInfo,
            float now)
        {
            TelemetryTraceKind traceKind = ResolveTraceKind(currentTarget, crosshairTraceInfo, targetTraceInfo, out int targetId, out float targetHp);
            if (traceKind == TelemetryTraceKind.Unknown)
            {
                ResetPreviewObservationStateCore();
                return;
            }

            int traceEntityId = targetTraceInfo.EntityId;
            // 以下条件任一变化，都需要重新采样并重新拼装日志。
            bool shouldRefresh =
                !string.Equals(s_previewWeaponKey, weaponSnapshot.WeaponKey, StringComparison.Ordinal) ||
                s_previewTargetId != targetId ||
                Mathf.Abs(s_previewTargetHp - targetHp) > 0.01f ||
                s_previewTraceEntityId != traceEntityId ||
                s_previewTraceKind != traceKind ||
                now >= s_nextPreviewObservationTime;

            if (!shouldRefresh)
            {
                return;
            }

            s_previewWeaponKey = weaponSnapshot.WeaponKey;
            s_previewTargetId = targetId;
            s_previewTargetHp = targetHp;
            s_previewTraceEntityId = traceEntityId;
            s_previewTraceKind = traceKind;
            s_nextPreviewObservationTime = now + PreviewObservationIntervalSeconds;

            // 预览对象只描述“当前目标这一帧看到的结果”，不会写回全敌人缓存。
            var previewObservation = new PreviewObservation
            {
                WeaponKey = weaponSnapshot.WeaponKey,
                TargetId = targetId,
                TargetName = FindPlayerName(targetId),
                TargetHp = targetHp,
                TraceKind = traceKind
            };

            PopulateShotEnvironment(previewObservation, currentWeapon, currentTarget);
            if (previewObservation.TraceKind == TelemetryTraceKind.Wall)
            {
                s_previewWallThicknessText = previewObservation.WallThicknessText;
            }
            else
            {
                s_previewWallThicknessText = null;
            }

            LogPreviewObservation(previewObservation);
        }

        // 在锁内刷新所有敌人缓存，并把已经离开列表的目标清理掉。
        private static void RefreshEnemyObservationsCore(float now)
        {
            PlayerInfo localPlayer = PlayerUpdate.LocalEntity;
            if (localPlayer == null || localPlayer.IsDead || PlayerUpdate.EntityList == null)
            {
                s_targetWallObservations.Clear();
                return;
            }

            var activeEnemyIds = new HashSet<int>();
            // 逐个敌人刷新缓存。这里故意不依赖 currentTarget，保证每个敌人都有独立厚度结果。
            foreach (PlayerInfo player in PlayerUpdate.EntityList)
            {
                if (!IsEnemyPlayer(player) || player.IsDead)
                {
                    continue;
                }

                activeEnemyIds.Add(player.Id);
                RefreshTargetWallObservationCore(player, now);
            }

            // 不能在遍历字典 Keys 时直接删除，所以先收集失效 id 再统一移除。
            var staleTargetIds = new List<int>();
            foreach (int targetId in s_targetWallObservations.Keys)
            {
                if (!activeEnemyIds.Contains(targetId))
                {
                    staleTargetIds.Add(targetId);
                }
            }

            for (int i = 0; i < staleTargetIds.Count; i++)
            {
                s_targetWallObservations.Remove(staleTargetIds[i]);
            }
        }

        // 单目标刷新带节流；同一个目标在刷新窗口内重复查询时直接复用缓存。
        private static void RefreshTargetWallObservationCore(PlayerInfo target, float now)
        {
            if (target == null || target.Id <= 0)
            {
                return;
            }

            // 缓存仍在刷新窗口内时直接跳过，避免多处调用把同一目标重复 Trace 多次。
            if (s_targetWallObservations.TryGetValue(target.Id, out TargetWallObservation existingObservation) &&
                now < existingObservation.NextRefreshTime)
            {
                return;
            }

            if (!s_targetWallObservations.TryGetValue(target.Id, out TargetWallObservation targetObservation))
            {
                targetObservation = new TargetWallObservation();
                s_targetWallObservations[target.Id] = targetObservation;
            }

            PopulateTargetWallObservationCore(targetObservation, target);
            targetObservation.NextRefreshTime = now + PreviewObservationIntervalSeconds;
        }

        // 对外统一的缓存入口：需要时先刷新，再返回最新观测。
        private static bool TryGetTargetWallObservationCore(PlayerInfo target, float now, out TargetWallObservation targetObservation)
        {
            targetObservation = null;
            if (target == null || target.Id <= 0 || !IsEnemyPlayer(target))
            {
                return false;
            }

            RefreshTargetWallObservationCore(target, now);
            return s_targetWallObservations.TryGetValue(target.Id, out targetObservation);
        }

        // 把“该敌人当前是否直达 / 是否穿墙 / 穿墙多厚”填到缓存对象里。
        private static void PopulateTargetWallObservationCore(TargetWallObservation targetObservation, PlayerInfo target)
        {
            if (targetObservation == null)
            {
                return;
            }

            targetObservation.TargetId = target?.Id ?? -1;
            targetObservation.TraceKind = TelemetryTraceKind.Unknown;
            targetObservation.WallThicknessMeters = 0f;
            targetObservation.HasExactWallThickness = false;
            targetObservation.WallThicknessText = null;

            PlayerInfo localPlayer = PlayerUpdate.LocalEntity;
            if (target == null || localPlayer == null || localPlayer.IsDead)
            {
                return;
            }

            if (!TryGetTargetAimPoint(target, out Vector3 aimPoint, out _))
            {
                return;
            }

            Vector3 shotOrigin = GetLocalShotOrigin();
            if (!IsFiniteVector(shotOrigin) || !IsFiniteVector(aimPoint))
            {
                return;
            }

            ShotTraceInfo targetTraceInfo = PlayerUtility.GetShotTraceInfoToTarget(
                localPlayer,
                target,
                shotOrigin,
                aimPoint);
            // 先用游戏自己的枪线判定确定这是直达还是穿墙，再决定是否继续做厚度采样。
            targetObservation.TraceKind = ResolveTargetTraceKind(target, targetTraceInfo);
            // 直达目标不需要额外做墙厚探测；缓存里只保留轨迹类型即可。
            if (targetObservation.TraceKind != TelemetryTraceKind.Wall)
            {
                return;
            }

            // 预览对象只描述“当前目标这一帧看到的结果”，不会写回全敌人缓存。
            var previewObservation = new PreviewObservation
            {
                TargetId = target.Id,
                TargetName = target.PlayerName,
                TargetHp = Mathf.Max(target.Hp, 0f),
                TraceKind = targetObservation.TraceKind
            };

            PopulateShotEnvironment(previewObservation, Contexts.sharedInstance?.weapon?.currentWeaponEntity, target);
            targetObservation.WallThicknessMeters = previewObservation.WallThicknessMeters;
            targetObservation.HasExactWallThickness = previewObservation.HasExactWallThickness;
            targetObservation.WallThicknessText = previewObservation.WallThicknessText;
        }

        // 这里只回答“有没有被世界/其他阻挡挡住”，不在这里做厚度门槛判断。
        private static TelemetryTraceKind ResolveTargetTraceKind(PlayerInfo target, ShotTraceInfo targetTraceInfo)
        {
            if (target == null)
            {
                return TelemetryTraceKind.Unknown;
            }

            if (targetTraceInfo.EntityId == target.Id)
            {
                return TelemetryTraceKind.Direct;
            }

            return targetTraceInfo.State == ShotTraceState.HitWorld || targetTraceInfo.State == ShotTraceState.HitOtherEntity
                ? TelemetryTraceKind.Wall
                : TelemetryTraceKind.Unknown;
        }

        // 绘制层优先读取按目标缓存的厚度文本；如果缓存没命中，再退回当前预览结果。
        public static bool TryGetPreviewWallThicknessText(PlayerInfo target, out string wallThicknessText)
        {
            wallThicknessText = null;

            if (target == null)
            {
                return false;
            }

            lock (s_sync)
            {
                // 同一帧里多个 UI 位置可能请求同一目标厚度，这里直接复用该目标缓存。
                if (TryGetTargetWallObservationCore(target, Time.realtimeSinceStartup, out TargetWallObservation targetObservation) &&
                    targetObservation.TraceKind == TelemetryTraceKind.Wall &&
                    !string.IsNullOrWhiteSpace(targetObservation.WallThicknessText))
                {
                    wallThicknessText = targetObservation.WallThicknessText;
                    return true;
                }

                if (s_previewTraceKind != TelemetryTraceKind.Wall ||
                    s_previewTargetId != target.Id ||
                    string.IsNullOrWhiteSpace(s_previewWallThicknessText))
                {
                    return false;
                }

                wallThicknessText = s_previewWallThicknessText;
                return true;
            }
        }

        // 给地图几何线段之类的外层模块使用：对任意世界点采样“当前枪线遇到的第一层场景墙”厚度。
        public static bool TrySampleWallThicknessToWorldPoint(
            Vector3 targetPoint,
            float hitToleranceUnits,
            out WorldWallThicknessSample sample)
        {
            sample = default;
            if (!IsFiniteVector(targetPoint))
            {
                return false;
            }

            lock (s_sync)
            {
                PlayerEntity localPlayer = PlayerUpdate.LocalEntity?._entity;
                IPyEngine pyEngine = Contexts.sharedInstance?.battleRoom?.pyEngine?.PyEngine;
                if (localPlayer == null || localPlayer.move?.PyPlayerMove == null || pyEngine == null)
                {
                    return false;
                }

                Vector3 shotOrigin = GetLocalShotOrigin();
                if (!IsFiniteVector(shotOrigin))
                {
                    return false;
                }

                Vector3 ssjjStart = VectorCoordConverter.UnityToSsjj(shotOrigin);
                Vector3 ssjjTarget = VectorCoordConverter.UnityToSsjj(targetPoint);
                Vector3 delta = ssjjTarget - ssjjStart;
                float targetDistanceUnits = delta.magnitude;
                if (targetDistanceUnits <= 0.01f)
                {
                    return false;
                }

                Vector3 direction = delta / targetDistanceUnits;
                float toleranceUnits = Mathf.Max(8f, hitToleranceUnits);
                Vector3 probeTarget = ssjjTarget + direction * Mathf.Max(WorldPointProbeExtendUnits, toleranceUnits * 2f);
                float totalProbeUnits = (probeTarget - ssjjStart).magnitude;
                if (totalProbeUnits <= 0.01f)
                {
                    return false;
                }

                TraceResult trace = TracePath(pyEngine, localPlayer, ssjjStart, probeTarget);
                if (!IsBlockingTrace(trace) || trace.EntityId > 0)
                {
                    return false;
                }

                Vector3 hitPointSsjj = GetTraceEndPos(trace, ssjjStart);
                if (Vector3.Distance(hitPointSsjj, ssjjTarget) > toleranceUnits)
                {
                    return false;
                }

                float hitDistanceUnits = totalProbeUnits * Mathf.Clamp01(trace.Fraction);
                TraceThicknessProbeResult thicknessProbe = ResolveObstacleThicknessUnits(
                    pyEngine,
                    localPlayer,
                    trace,
                    ssjjStart,
                    direction,
                    hitDistanceUnits,
                    totalProbeUnits);
                if (thicknessProbe.Status == TraceThicknessStatus.EntityObstacle ||
                    thicknessProbe.Status == TraceThicknessStatus.Unknown)
                {
                    return false;
                }

                float thicknessUnits = thicknessProbe.HasExactThickness
                    ? thicknessProbe.ThicknessUnits
                    : thicknessProbe.ThicknessLowerBoundUnits;
                if (thicknessUnits <= 0.01f)
                {
                    return false;
                }

                float wallThicknessMeters = thicknessUnits * 0.01f;
                bool hasExactThickness = thicknessProbe.HasExactThickness;
                sample = new WorldWallThicknessSample
                {
                    HitPoint = VectorCoordConverter.SsjjToUnity(hitPointSsjj),
                    DistanceMeters = hitDistanceUnits * 0.01f,
                    WallThicknessMeters = wallThicknessMeters,
                    HasExactThickness = hasExactThickness,
                    IsThinWallCandidate = hasExactThickness && wallThicknessMeters <= ThinWallAutoAimMaxThicknessMeters,
                    WallThicknessText = FormatWallThicknessValue(wallThicknessMeters, !hasExactThickness)
                };
                return true;
            }
        }

        // 敌人框旁边只需要“总厚度”这个短文本；直达显示 0.00m，多段墙也只保留总值。
        public static bool TryGetTargetWallThicknessDisplayText(PlayerInfo target, out string wallThicknessText)
        {
            wallThicknessText = null;

            if (target == null)
            {
                return false;
            }

            lock (s_sync)
            {
                if (!TryGetTargetWallObservationCore(target, Time.realtimeSinceStartup, out TargetWallObservation targetObservation))
                {
                    return false;
                }

                if (targetObservation.TraceKind == TelemetryTraceKind.Direct)
                {
                    wallThicknessText = "0.00m";
                    return true;
                }

                if (targetObservation.TraceKind != TelemetryTraceKind.Wall)
                {
                    return false;
                }

                if (targetObservation.WallThicknessMeters > 0.01f)
                {
                    wallThicknessText = FormatWallThicknessValue(
                        targetObservation.WallThicknessMeters,
                        !targetObservation.HasExactWallThickness);
                    return true;
                }

                if (string.IsNullOrWhiteSpace(targetObservation.WallThicknessText))
                {
                    return false;
                }

                wallThicknessText = targetObservation.WallThicknessText;
                return true;
            }
        }

        // 自瞄更新门槛：直达目标直接放行；穿墙目标必须拿到精确总厚度且不超过阈值。
        public static bool CanUpdateTarget(PlayerInfo target)
        {
            if (target == null || target.IsDead || !IsEnemyPlayer(target))
            {
                return false;
            }

            lock (s_sync)
            {
                if (!TryGetTargetWallObservationCore(target, Time.realtimeSinceStartup, out TargetWallObservation targetObservation))
                {
                    return false;
                }

                // 直达代表游戏枪线没有场景阻挡，不需要走薄墙阈值判断。
                if (targetObservation.TraceKind == TelemetryTraceKind.Direct)
                {
                    return true;
                }

                return targetObservation.TraceKind == TelemetryTraceKind.Wall &&
                       targetObservation.HasExactWallThickness &&
                       targetObservation.WallThicknessMeters <= ThinWallAutoAimMaxThicknessMeters;
            }
        }

        // 采集一次完整的“开枪前环境”：基础伤害、枪线距离、障碍列表、厚度公式、是否精确。
        private static void PopulateShotEnvironment(PreviewObservation observation, WeaponEntity currentWeapon, PlayerInfo currentTarget)
        {
            if (observation == null)
            {
                return;
            }

            float configuredBaseDamage = GetBaseDamage(currentWeapon);
            if (configuredBaseDamage > 0.01f)
            {
                observation.ConfiguredBaseDamage = configuredBaseDamage;
                observation.HasConfiguredBaseDamage = true;
            }

            PlayerInfo targetPlayer = currentTarget;
            // 调用方可能只带了 observation.TargetId，没有把完整目标对象传下来，这里兜底回查一次。
            if (targetPlayer == null || (observation.TargetId > 0 && targetPlayer.Id != observation.TargetId))
            {
                targetPlayer = FindPlayerById(observation.TargetId);
            }

            if (targetPlayer == null)
            {
                observation.ObstacleSummary = "未解析到目标，无法采集障碍";
                return;
            }

            if (!TryGetTargetAimPoint(targetPlayer, out Vector3 aimPoint, out _))
            {
                observation.ObstacleSummary = "未获取到目标骨骼，无法采集障碍";
                return;
            }

            Vector3 shotOrigin = GetLocalShotOrigin();
            if (!IsFiniteVector(shotOrigin) || !IsFiniteVector(aimPoint))
            {
                observation.ObstacleSummary = "起点或目标点无效，无法采集障碍";
                return;
            }

            float shotDistanceUnits = Vector3.Distance(shotOrigin, aimPoint);
            if (shotDistanceUnits > 0.01f)
            {
                observation.ShotDistanceMeters = shotDistanceUnits * 0.01f;
                observation.HasShotDistance = true;
            }

            // 障碍采集完全基于游戏 BulletTrace，不走 Unity Physics，所以结果更贴近真实服务器/客户端判定。
            int obstacleCount = CollectTraceObstacles(
                shotOrigin,
                aimPoint,
                s_traceObstacleBuffer,
                out TraceObstacleCollectionInfo collectionInfo);

            observation.ObstacleCount = obstacleCount;
            observation.ObstacleSummary = BuildObstacleSummary(s_traceObstacleBuffer, obstacleCount);

            if (obstacleCount > 0)
            {
                observation.WallThicknessText = BuildWallThicknessFormula(
                    s_traceObstacleBuffer,
                    obstacleCount,
                    collectionInfo,
                    out float wallThicknessMeters,
                    out bool hasWallThickness);

                observation.WallThicknessMeters = wallThicknessMeters;
                observation.HasWallThickness = hasWallThickness;
            }

            // 游戏枪线判定为“有阻挡”，但我们没定位到可量化的场景障碍时，要明确标记为未解析而不是误报 0 墙。
            if (observation.TraceKind == TelemetryTraceKind.Wall && obstacleCount <= 0)
            {
                if (collectionInfo.EndedByEntityHit)
                {
                    observation.ObstacleSummary = BuildEntityHitSummary(collectionInfo.EndedByEntityId);
                    observation.WallThicknessText = "无场景墙体";
                }
                else
                {
                    observation.ObstacleSummary = "游戏枪线判定有阻挡，但当前未解析到具体场景障碍";
                    observation.WallThicknessText = "未解析(未定位到具体场景障碍)";
                }
            }

            // 只要任意一段墙没能探到出口，整条厚度公式就只能作为下界，不能当作精确值给自瞄放行。
            if (collectionInfo.HasUnresolvedThickness)
            {
                if (string.IsNullOrEmpty(observation.WallThicknessText))
                {
                    observation.WallThicknessText = "未解析";
                }
            }
            else if (collectionInfo.TotalThicknessMeters > 0.01f && !observation.HasWallThickness)
            {
                observation.WallThicknessMeters = collectionInfo.TotalThicknessMeters;
                observation.HasWallThickness = true;
                observation.WallThicknessText = $"{collectionInfo.TotalThicknessMeters:F2}m";
            }

            observation.HasExactWallThickness = observation.HasWallThickness && !collectionInfo.HasUnresolvedThickness;
        }

        // 穿墙采样和实际自瞄都应共用同一套瞄点解析，避免一边在追假头一边在量真实 hitbox。
        internal static bool TryGetTargetAimPoint(PlayerInfo target, out Vector3 aimPoint, out int aimHitGroup)
        {
            aimPoint = Vector3.zero;
            aimHitGroup = 8;

            if (target == null || target._entity == null)
            {
                return false;
            }

            if (string.Equals(target.Career, "rpg_by_parasitism", StringComparison.OrdinalIgnoreCase))
            {
                Transform specialBone = target._entity.GetBone("Bone05");
                if (specialBone != null)
                {
                    aimPoint = specialBone.position;
                    aimHitGroup = 2;
                    return true;
                }
            }

            Transform head = target.GetPlayerTransform("Bip01_Head");
            Transform headNub = target.GetPlayerTransform("Bip01_HeadNub");
            if (head != null && headNub != null)
            {
                aimPoint = (head.position + headNub.position) * 0.5f;
                aimHitGroup = 1;
                return true;
            }

            if (headNub != null)
            {
                aimPoint = headNub.position;
                aimHitGroup = 1;
                return true;
            }

            if (head != null)
            {
                aimPoint = head.position;
                aimHitGroup = 1;
                return true;
            }

            Transform neck = target.GetPlayerTransform("Bip01_Neck");
            if (neck != null)
            {
                aimPoint = neck.position;
                aimHitGroup = 2;
                return true;
            }

            return false;
        }

        // 尽量取游戏补偿后的真实视角开火点；取不到时才退回头骨或 Camera。
        internal static Vector3 GetLocalShotOrigin()
        {
            PlayerEntity localPlayer = PlayerUpdate.LocalEntity?._entity;
            if (localPlayer != null && localPlayer.move?.PyPlayerMove != null)
            {
                try
                {
                    Vector3 ssjjEyePos = localPlayer.GetCompenstatePos(localPlayer.fpos.Change.GetPosIndex());
                    ssjjEyePos.z += (float)localPlayer.move.PyPlayerMove.GetViewHeight();
                    return VectorCoordConverter.SsjjToUnity(ssjjEyePos);
                }
                catch
                {
                }
            }

            Transform localHead = PlayerUpdate.LocalEntity?._entity?.GetBone("Bip01_Head");
            if (localHead != null)
            {
                return localHead.position;
            }

            return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        }

        // 优先读武器真实配置伤害；特殊模式武器会切到副伤害。
        private static float GetBaseDamage(WeaponEntity weapon)
        {
            if (weapon == null || !weapon.hasBasicInfo || weapon.basicInfo?.Info == null)
            {
                return 0f;
            }

            object weaponInfo = weapon.basicInfo.Info;
            int secondaryDamage = 0;
            int primaryDamage = 0;

            if (weaponInfo is DefaultWeaponInfo defaultWeaponInfo)
            {
                secondaryDamage = defaultWeaponInfo.SecondaryDamage;
                primaryDamage = defaultWeaponInfo.Damage;
            }
            else
            {
                secondaryDamage = GetWeaponInfoIntProperty(weaponInfo, "SecondaryDamage");
                primaryDamage = GetWeaponInfoIntProperty(weaponInfo, "Damage");
            }

            if (weapon.hasSpecial && weapon.special.SpecialMode && secondaryDamage > 0)
            {
                return secondaryDamage;
            }

            if (primaryDamage > 0)
            {
                return primaryDamage;
            }

            if (secondaryDamage > 0)
            {
                return secondaryDamage;
            }

            return GetFallbackBaseDamageGuess(weapon);
        }

        // 从开火点朝目标点反复 Trace。每解析完一段墙，就推进到墙后继续找下一段障碍。
        private static int CollectTraceObstacles(
            Vector3 startPos,
            Vector3 targetPos,
            TraceObstacleSample[] buffer,
            out TraceObstacleCollectionInfo collectionInfo)
        {
            collectionInfo = default;

            if (buffer == null || buffer.Length == 0)
            {
                return 0;
            }

            PlayerEntity localPlayer = PlayerUpdate.LocalEntity?._entity;
            IPyEngine pyEngine = Contexts.sharedInstance?.battleRoom?.pyEngine?.PyEngine;
            if (localPlayer == null || localPlayer.move?.PyPlayerMove == null || pyEngine == null)
            {
                return 0;
            }

            Vector3 ssjjStart = VectorCoordConverter.UnityToSsjj(startPos);
            Vector3 ssjjTarget = VectorCoordConverter.UnityToSsjj(targetPos);
            Vector3 delta = ssjjTarget - ssjjStart;
            float totalUnits = delta.magnitude;
            if (totalUnits <= 0.01f)
            {
                return 0;
            }

            Vector3 direction = delta / totalUnits;
            Vector3 currentStart = ssjjStart;
            float traveledUnits = 0f;
            int obstacleCount = 0;

            // 循环终止条件有三个：缓冲区满、已经到目标附近、或者后续枪线已经不再被阻挡。
            while (obstacleCount < buffer.Length && traveledUnits < totalUnits - 0.01f)
            {
                float remainingUnits = (ssjjTarget - currentStart).magnitude;
                if (remainingUnits <= 0.01f)
                {
                    break;
                }

                TraceResult trace = TracePath(pyEngine, localPlayer, currentStart, ssjjTarget);
                if (!IsBlockingTrace(trace))
                {
                    break;
                }

                float hitDistanceUnits = remainingUnits * Mathf.Clamp01(trace.Fraction);
                float cumulativeDistanceUnits = Mathf.Min(totalUnits, traveledUnits + hitDistanceUnits);
                TraceThicknessProbeResult thicknessProbe = ResolveObstacleThicknessUnits(
                    pyEngine,
                    localPlayer,
                    trace,
                    currentStart,
                    direction,
                    hitDistanceUnits,
                    remainingUnits);

                // 命中实体时停止场景墙采样；实体本身不计入“场景墙厚度”。
                if (thicknessProbe.Status == TraceThicknessStatus.EntityObstacle)
                {
                    collectionInfo.EndedByEntityHit = true;
                    collectionInfo.EndedByEntityId = trace.EntityId;
                    break;
                }

                float exactThicknessMeters = thicknessProbe.HasExactThickness
                    ? thicknessProbe.ThicknessUnits * 0.01f
                    : 0f;
                float lowerBoundThicknessMeters = thicknessProbe.HasLowerBound
                    ? thicknessProbe.ThicknessLowerBoundUnits * 0.01f
                    : exactThicknessMeters;

                collectionInfo.TotalThicknessMeters += exactThicknessMeters;
                collectionInfo.TotalThicknessLowerBoundMeters += lowerBoundThicknessMeters;
                if (!thicknessProbe.HasExactThickness && thicknessProbe.Status != TraceThicknessStatus.Unknown)
                {
                    collectionInfo.HasUnresolvedThickness = true;
                }

                buffer[obstacleCount] = new TraceObstacleSample
                {
                    DistanceMeters = cumulativeDistanceUnits * 0.01f,
                    ThicknessMeters = exactThicknessMeters,
                    ThicknessLowerBoundMeters = lowerBoundThicknessMeters,
                    ThicknessStatus = thicknessProbe.Status,
                    StartedInSolid = thicknessProbe.StartedInSolid,
                    AllSolid = thicknessProbe.AllSolid,
                    SurfaceFlags = trace.SurfaceFlags,
                    Contents = trace.Contents,
                    EntityId = trace.EntityId,
                    HitPoint = VectorCoordConverter.SsjjToUnity(GetTraceEndPos(trace, currentStart))
                };
                obstacleCount++;

                if (thicknessProbe.ShouldStopTracing)
                {
                    break;
                }

                // 推进到当前墙体之后，下一轮 Trace 才能继续寻找下一堵墙，而不会卡在同一面墙上。
                float advanceUnits = hitDistanceUnits + Mathf.Max(thicknessProbe.ThicknessUnits, WallProbeEnterOffsetUnits);
                if (advanceUnits <= 0.01f || float.IsNaN(advanceUnits) || float.IsInfinity(advanceUnits))
                {
                    advanceUnits = WallProbeEnterOffsetUnits;
                }

                currentStart += direction * advanceUnits;
                traveledUnits += advanceUnits;
            }

            return obstacleCount;
        }

        // 只有武器配置读取失败时才会走到这里；它只是日志兜底，不参与真实厚度判定。
        private static float GetFallbackBaseDamageGuess(WeaponEntity weapon)
        {
            if (weapon == null)
            {
                return 0f;
            }

            string weaponName = weapon.basicInfo?.Info?.Name;
            if (!string.IsNullOrEmpty(weaponName))
            {
                string lowerName = weaponName.ToLowerInvariant();
                if (lowerName.Contains("awp") || lowerName.Contains("awm") || lowerName.Contains("barrett") || lowerName.Contains("sniper"))
                {
                    return 90f;
                }

                if (lowerName.Contains("deagle") || lowerName.Contains("revolver"))
                {
                    return 45f;
                }

                if (lowerName.Contains("shotgun") || lowerName.Contains("spas") || lowerName.Contains("m3"))
                {
                    return 16f;
                }

                if (lowerName.Contains("smg") || lowerName.Contains("mp5") || lowerName.Contains("uzi") || lowerName.Contains("p90"))
                {
                    return 24f;
                }
            }

            if (weapon.hasSlot)
            {
                switch (weapon.slot.Slot)
                {
                    case 1:
                        return 34f;

                    case 2:
                        return 24f;

                    case 3:
                        return 42f;
                }
            }

            return 30f;
        }

        // 部分武器信息类型不是 DefaultWeaponInfo，只能通过反射兜底读取字段。
        private static int GetWeaponInfoIntProperty(object weaponInfo, string propertyName)
        {
            if (weaponInfo == null || string.IsNullOrEmpty(propertyName))
            {
                return 0;
            }

            try
            {
                PropertyInfo property = weaponInfo.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public);
                if (property == null)
                {
                    return 0;
                }

                object value = property.GetValue(weaponInfo, null);
                if (value == null)
                {
                    return 0;
                }

                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        // 在障碍内部用小步长探针向前扫，直到离开 solid 为止；离不开就只返回厚度下界。
        private static TraceThicknessProbeResult ResolveObstacleThicknessUnits(
            IPyEngine pyEngine,
            PlayerEntity localPlayer,
            TraceResult hitTrace,
            Vector3 traceStart,
            Vector3 direction,
            float hitDistanceUnits,
            float remainingUnits)
        {
            var result = new TraceThicknessProbeResult
            {
                Status = TraceThicknessStatus.Unknown,
                StartedInSolid = hitTrace.Startsolid,
                AllSolid = hitTrace.Allsolid
            };

            // 入口 Trace 直接打到实体时，这段阻挡不再当作“场景墙”处理。
            if (hitTrace.EntityId > 0)
            {
                result.Status = TraceThicknessStatus.EntityObstacle;
                result.ShouldStopTracing = true;
                return result;
            }

            // 从墙体内部一点开始向前探，避免出口判断再次落回同一接触面。
            for (float probeDistanceUnits = hitDistanceUnits + WallProbeEnterOffsetUnits;
                 probeDistanceUnits < remainingUnits;
                 probeDistanceUnits += WallProbeStepUnits)
            {
                Vector3 probeStart = traceStart + direction * probeDistanceUnits;
                Vector3 probeEnd = probeStart + direction * WallProbeLengthUnits;
                TraceResult probeTrace = TracePath(pyEngine, localPlayer, probeStart, probeEnd);

                if (!IsBlockingTrace(probeTrace))
                {
                    result.Status = TraceThicknessStatus.Exact;
                    result.ThicknessUnits = Mathf.Max(0f, probeDistanceUnits - hitDistanceUnits);
                    result.ThicknessLowerBoundUnits = result.ThicknessUnits;
                    return result;
                }
            }

            // 直到目标都没探出墙体时，只能确认“至少厚这么多”。
            float reachableThicknessUnits = Mathf.Max(0f, remainingUnits - hitDistanceUnits);
            result.Status = TraceThicknessStatus.BlockedUntilTarget;
            result.ThicknessLowerBoundUnits = reachableThicknessUnits;
            result.ShouldStopTracing = true;
            return result;
        }

        // 只要 StartSolid / AllSolid / Fraction 未走满，都视为枪线仍被阻挡。
        private static bool IsBlockingTrace(TraceResult trace)
        {
            return trace.Startsolid || trace.Allsolid || trace.Fraction < 0.999f;
        }

        // 这是所有厚度计算的底层入口，直接调用游戏世界的 Trace 接口。
        private static TraceResult TracePath(IPyEngine pyEngine, PlayerEntity localPlayer, Vector3 start, Vector3 end)
        {
            var trace = new Trace();
            var result = default(TraceResult);

            float[] startF3 = { start.x, start.y, start.z };
            float[] endF3 = { end.x, end.y, end.z };

            pyEngine.GetWorld().Trace(
                trace,
                startF3,
                PointTraceMins,
                PointTraceMaxs,
                endF3,
                BulletTraceMask,
                localPlayer.move.PyPlayerMove.GetEntity(),
                localPlayer.move.PyPlayerMove.GetEntity(),
                0);
            result.CopyFrom(trace);
            return result;
        }

        // TraceResult.EndPos 在某些异常路径下可能为空，这里统一做安全兜底。
        private static Vector3 GetTraceEndPos(TraceResult trace, Vector3 fallback)
        {
            return new Vector3(
                trace.EndPos != null && trace.EndPos.Length > 0 ? trace.EndPos[0] : fallback.x,
                trace.EndPos != null && trace.EndPos.Length > 1 ? trace.EndPos[1] : fallback.y,
                trace.EndPos != null && trace.EndPos.Length > 2 ? trace.EndPos[2] : fallback.z);
        }

        // 把障碍样本按先后顺序串成日志字符串。
        private static string BuildObstacleSummary(TraceObstacleSample[] samples, int obstacleCount)
        {
            if (samples == null || obstacleCount <= 0)
            {
                return "未采集";
            }

            int visibleCount = Math.Min(obstacleCount, samples.Length);
            var entries = new List<string>(visibleCount);
            for (int i = 0; i < visibleCount; i++)
            {
                entries.Add(BuildObstacleEntry(samples[i], i + 1));
            }

            return entries.Count > 0 ? string.Join(" -> ", entries.ToArray()) : "未采集";
        }

        // 当障碍追踪被实体截断时，把实体信息格式化成可读文本。
        private static string BuildEntityHitSummary(int entityId)
        {
            if (entityId <= 0)
            {
                return "命中实体障碍";
            }

            PlayerInfo player = FindPlayerById(entityId);
            return player != null
                ? $"命中玩家 {player.PlayerName}({player.Id})"
                : $"命中实体(EntityId={entityId})";
        }

        // 把障碍厚度渲染成 1.23m + 0.77m = 2.00m 这种公式；下界用 >= 标记。
        private static string BuildWallThicknessFormula(
            TraceObstacleSample[] samples,
            int obstacleCount,
            TraceObstacleCollectionInfo collectionInfo,
            out float wallThicknessMeters,
            out bool hasWallThickness)
        {
            wallThicknessMeters = 0f;
            hasWallThickness = false;

            if (samples == null || obstacleCount <= 0)
            {
                return string.Empty;
            }

            int visibleCount = Math.Min(obstacleCount, samples.Length);
            var terms = new List<string>(visibleCount);

            for (int i = 0; i < visibleCount; i++)
            {
                if (!TryBuildWallThicknessTerm(samples[i], out string term))
                {
                    continue;
                }

                terms.Add(term);
            }

            if (terms.Count <= 0)
            {
                return string.Empty;
            }

            // 存在未解析墙段时，总厚度也只能作为下界展示，不能标成精确值。
            bool totalIsLowerBound = collectionInfo.HasUnresolvedThickness;
            wallThicknessMeters = totalIsLowerBound
                ? collectionInfo.TotalThicknessLowerBoundMeters
                : collectionInfo.TotalThicknessMeters;
            hasWallThickness = wallThicknessMeters > 0.01f;

            string totalText = FormatWallThicknessValue(wallThicknessMeters, totalIsLowerBound);
            if (terms.Count == 1)
            {
                return totalText;
            }

            return $"{string.Join(" + ", terms.ToArray())} = {totalText}";
        }

        // 只有精确厚度或下界厚度才会进入公式；纯未知状态不会污染公式文本。
        private static bool TryBuildWallThicknessTerm(TraceObstacleSample sample, out string term)
        {
            term = null;
            bool isLowerBound = false;
            float thicknessValue = 0f;

            switch (sample.ThicknessStatus)
            {
                case TraceThicknessStatus.Exact:
                    thicknessValue = sample.ThicknessMeters;
                    break;

                case TraceThicknessStatus.BlockedUntilTarget:
                    thicknessValue = sample.ThicknessLowerBoundMeters;
                    isLowerBound = true;
                    break;

                default:
                    return false;
            }

            if (thicknessValue <= 0.001f)
            {
                return false;
            }

            term = FormatWallThicknessValue(thicknessValue, isLowerBound);
            return true;
        }

        // 统一厚度文本格式，保证 UI 和日志对精确值 / 下界值的表现一致。
        private static string FormatWallThicknessValue(float thicknessMeters, bool isLowerBound)
        {
            if (thicknessMeters <= 0.001f)
            {
                return isLowerBound ? ">=0.00m" : "0.00m";
            }

            return isLowerBound
                ? $">={thicknessMeters:F2}m"
                : $"{thicknessMeters:F2}m";
        }

        // 单条障碍日志既保留距离，也保留厚度状态和原始 Trace 标志。
        private static string BuildObstacleEntry(TraceObstacleSample sample, int index)
        {
            string obstacleKind = sample.EntityId > 0
                ? $"实体障碍#{index}(EntityId={sample.EntityId})"
                : $"场景障碍#{index}";
            string thicknessText = FormatObstacleThicknessText(sample);

            return $"{obstacleKind}[距离={sample.DistanceMeters:F2}m, 厚度={thicknessText}, SurfaceFlags={sample.SurfaceFlags}, Contents={sample.Contents}]";
        }

        // 针对不同厚度状态生成用户能看懂的障碍描述文本。
        private static string FormatObstacleThicknessText(TraceObstacleSample sample)
        {
            string solidHint = BuildObstacleSolidHint(sample);

            switch (sample.ThicknessStatus)
            {
                case TraceThicknessStatus.Exact:
                    return sample.ThicknessMeters > 0.001f
                        ? $"{sample.ThicknessMeters:F2}m"
                        : "0.00m";

                case TraceThicknessStatus.BlockedUntilTarget:
                    if (sample.ThicknessLowerBoundMeters > 0.001f)
                    {
                        return $">={sample.ThicknessLowerBoundMeters:F2}m(阻挡延伸到目标{solidHint})";
                    }

                    return $"未解析(阻挡延伸到目标{solidHint})";

                case TraceThicknessStatus.EntityObstacle:
                    return $"实体阻挡{solidHint}";

                default:
                    return $"未解析{solidHint}";
            }
        }

        // 补充“整段都在 solid 内”或“起点就在 solid 内”的上下文，便于排查异常厚度。
        private static string BuildObstacleSolidHint(TraceObstacleSample sample)
        {
            if (sample.AllSolid)
            {
                return "，整段Trace都在阻挡内";
            }

            if (sample.StartedInSolid)
            {
                return "，探针起点在阻挡内";
            }

            return string.Empty;
        }

        // 过滤 NaN / Infinity，避免坏坐标把距离和 Trace 结果全部带偏。
        private static bool IsFiniteVector(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsInfinity(value.z);
        }

        // 当前预览目标存在时，优先看“指向目标的枪线”；否则退回准星直接命中玩家的情况。
        private static TelemetryTraceKind ResolveTraceKind(
            PlayerInfo currentTarget,
            ShotTraceInfo crosshairTraceInfo,
            ShotTraceInfo targetTraceInfo,
            out int targetId,
            out float targetHp)
        {
            targetId = -1;
            targetHp = -1f;

            PlayerInfo validCurrentTarget = IsEnemyPlayer(currentTarget)
                ? currentTarget
                : null;

            // 一旦已有自瞄目标，预览日志就围绕该目标展开，不被准星偶发扫过的其他玩家干扰。
            if (validCurrentTarget != null)
            {
                if (targetTraceInfo.EntityId == validCurrentTarget.Id)
                {
                    targetId = validCurrentTarget.Id;
                    targetHp = Mathf.Max(validCurrentTarget.Hp, 0f);
                    return TelemetryTraceKind.Direct;
                }

                if (targetTraceInfo.State == ShotTraceState.HitWorld || targetTraceInfo.State == ShotTraceState.HitOtherEntity)
                {
                    targetId = validCurrentTarget.Id;
                    targetHp = Mathf.Max(validCurrentTarget.Hp, 0f);
                    return TelemetryTraceKind.Wall;
                }

                return TelemetryTraceKind.Unknown;
            }

            // 没有 currentTarget 时，只把准星直达命中的敌人当作预览目标。
            if (crosshairTraceInfo.State == ShotTraceState.HitPlayer && crosshairTraceInfo.EntityId > 0)
            {
                PlayerInfo crosshairTarget = FindPlayerById(crosshairTraceInfo.EntityId);
                if (!IsEnemyPlayer(crosshairTarget))
                {
                    return TelemetryTraceKind.Unknown;
                }

                targetId = crosshairTarget.Id;
                targetHp = Mathf.Max(crosshairTarget.Hp, 0f);
                return TelemetryTraceKind.Direct;
            }

            return TelemetryTraceKind.Unknown;
        }

        // 统一过滤自己、队友、无效 id；所有缓存和日志都只关心敌方目标。
        private static bool IsEnemyPlayer(PlayerInfo player)
        {
            PlayerInfo localPlayer = PlayerUpdate.LocalEntity;
            return player != null &&
                   localPlayer != null &&
                   player.Id > 0 &&
                   player.Id != localPlayer.Id &&
                   player.Team != localPlayer.Team;
        }

        // 按当前 EntityList 做线性查找，足够应付房间内玩家数量。
        private static PlayerInfo FindPlayerById(int targetId)
        {
            if (targetId <= 0 || PlayerUpdate.EntityList == null)
            {
                return null;
            }

            // 逐个敌人刷新缓存。这里故意不依赖 currentTarget，保证每个敌人都有独立厚度结果。
            foreach (PlayerInfo player in PlayerUpdate.EntityList)
            {
                if (player != null && player.Id == targetId)
                {
                    return player;
                }
            }

            return null;
        }

        // 日志输出时优先补上玩家名称，没有则返回空串。
        private static string FindPlayerName(int targetId)
        {
            PlayerInfo player = FindPlayerById(targetId);
            return player?.PlayerName ?? string.Empty;
        }

        // 把内部轨迹枚举映射成界面/日志里的中文词。
        private static string FormatTraceKind(TelemetryTraceKind traceKind)
        {
            switch (traceKind)
            {
                case TelemetryTraceKind.Direct:
                    return "直达";

                case TelemetryTraceKind.Wall:
                    return "穿墙";

                default:
                    return "未知";
            }
        }

        // 统一目标展示格式：有 id 就显示 名称(id)，否则只显示名称。
        private static string FormatTargetLabel(PreviewObservation observation)
        {
            if (observation == null)
            {
                return "未知目标";
            }

            string targetName = string.IsNullOrEmpty(observation.TargetName) ? "未命名目标" : observation.TargetName;
            return observation.TargetId > 0 ? $"{targetName}({observation.TargetId})" : targetName;
        }

        // 把本次观测中和开枪环境有关的字段拼成最终日志正文。
        private static string BuildShotEnvironmentText(PreviewObservation observation)
        {
            if (observation == null)
            {
                return "武器原始伤害(配置)=未知，枪线距离=未知，墙体厚度=未知，障碍数量=0，障碍=未采集";
            }

            string baseDamageText = observation.HasConfiguredBaseDamage
                ? observation.ConfiguredBaseDamage.ToString("F1")
                : "未知";
            string distanceText = observation.HasShotDistance
                ? $"{observation.ShotDistanceMeters:F2}m"
                : "未知";
            string wallThicknessText = observation.TraceKind == TelemetryTraceKind.Wall
                ? (!string.IsNullOrEmpty(observation.WallThicknessText)
                    ? observation.WallThicknessText
                    : (observation.HasWallThickness ? $"{observation.WallThicknessMeters:F2}m" : "未知"))
                : "无";
            string obstacleText = string.IsNullOrEmpty(observation.ObstacleSummary)
                ? "未采集"
                : observation.ObstacleSummary;

            return $"武器原始伤害(配置)={baseDamageText}，枪线距离={distanceText}，墙体厚度={wallThicknessText}，障碍来源=游戏BulletTrace，障碍数量={observation.ObstacleCount}，障碍={obstacleText}";
        }

        // 只输出“发生变化”的开枪前观测，避免同一目标静止不动时刷重复日志。
        private static void LogPreviewObservation(PreviewObservation observation)
        {
            if (observation == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(observation.TargetName))
            {
                observation.TargetName = FindPlayerName(observation.TargetId);
            }

            string previewText =
                $"开枪前观测：武器={observation.WeaponKey}，轨迹={FormatTraceKind(observation.TraceKind)}，目标={FormatTargetLabel(observation)}，目标血量={observation.TargetHp:F1}，{BuildShotEnvironmentText(observation)}";
            // 整条日志文本完全相同时直接跳过，这比逐字段比较更稳定。
            if (string.Equals(previewText, s_lastPreviewLogKey, StringComparison.Ordinal))
            {
                return;
            }

            s_lastPreviewLogKey = previewText;
            Logger.LogDebug("每枪数据", previewText);
        }

        // 清空当前预览状态，但不影响全敌人厚度缓存。
        private static void ResetPreviewObservationStateCore()
        {
            s_previewWeaponKey = null;
            s_previewTargetId = -1;
            s_previewTargetHp = -1f;
            s_previewTraceEntityId = -1;
            s_previewTraceKind = TelemetryTraceKind.Unknown;
            s_previewWallThicknessText = null;
            s_nextPreviewObservationTime = -1f;
            s_lastPreviewLogKey = null;
        }

        // 只给主武器/副武器建立观测快照；手雷、刀、特殊槽位不参与穿墙日志。
        private static bool TryCreateWeaponSnapshot(WeaponEntity currentWeapon, out WeaponSnapshot snapshot)
        {
            snapshot = default;

            if (currentWeapon == null ||
                !currentWeapon.hasBasicInfo ||
                currentWeapon.basicInfo == null ||
                currentWeapon.basicInfo.Data == null ||
                !currentWeapon.hasSlot ||
                (currentWeapon.slot.Slot != 1 && currentWeapon.slot.Slot != 2))
            {
                return false;
            }

            int weaponId = currentWeapon.hasWeaponId ? currentWeapon.weaponId.Value : 0;
            bool specialMode = currentWeapon.hasSpecial && currentWeapon.special.SpecialMode;
            string weaponName = currentWeapon.basicInfo.Data.WeaponName;
            if (string.IsNullOrEmpty(weaponName))
            {
                weaponName = currentWeapon.basicInfo.Info?.Name ?? "unknown";
            }

            snapshot = new WeaponSnapshot
            {
                WeaponKey = $"{weaponId}:{(specialMode ? 1 : 0)}:{weaponName}"
            };

            return true;
        }

        // 单次预览日志使用的临时聚合对象。它可以包含墙厚、距离、基础伤害和障碍摘要。
        private sealed class PreviewObservation
        {
            public string WeaponKey;
            public int TargetId;
            public string TargetName;
            public float TargetHp;
            public TelemetryTraceKind TraceKind;
            public float ConfiguredBaseDamage;
            public bool HasConfiguredBaseDamage;
            public float ShotDistanceMeters;
            public bool HasShotDistance;
            public float WallThicknessMeters;
            public bool HasWallThickness;
            public bool HasExactWallThickness;
            public string WallThicknessText;
            public int ObstacleCount;
            public string ObstacleSummary;
        }

        // 按敌人缓存的精简观测对象。这里只保留选人和绘制真正需要的字段。
        private sealed class TargetWallObservation
        {
            public int TargetId;
            public TelemetryTraceKind TraceKind;
            public float WallThicknessMeters;
            public bool HasExactWallThickness;
            public string WallThicknessText;
            public float NextRefreshTime;
        }

        // 用于识别“当前是不是同一把枪”的最小快照。武器 id / 特殊模式 / 名称变化都会触发刷新。
        private struct WeaponSnapshot
        {
            public string WeaponKey;
        }

        public struct WorldWallThicknessSample
        {
            public Vector3 HitPoint;
            public float DistanceMeters;
            public float WallThicknessMeters;
            public bool HasExactThickness;
            public bool IsThinWallCandidate;
            public string WallThicknessText;
        }
    }
}
