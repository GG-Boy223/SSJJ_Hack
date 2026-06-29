using Assets.Sources.Modules.Player.HitBox;
using physics.data;
using physics.trace;
using share;
using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Render;
using SkyDome.Utilities;
using SSJJPhysics;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Feature.AutoTrigger
{
    public class WindSpiritRecall : MonoBehaviour
    {
        // 缓存所有找到的风铃标记位置
        private List<Vector3> _windSpiritTagPositions = new List<Vector3>();

        // 缓存每个风铃路径上检测到的敌人信息（静态，供WallHack访问）
        public static Dictionary<Vector3, List<EnemyOnPath>> EnemiesOnPaths = new Dictionary<Vector3, List<EnemyOnPath>>();

        // 敌人信息结构（公开给WallHack使用）
        public struct EnemyOnPath
        {
            public PlayerInfo Player;
            public Vector3 Position;
            public float Distance;
        }

        private void Update()
        {
            _windSpiritTagPositions.Clear();
            EnemiesOnPaths.Clear();

            if (PlayerUpdate.LocalEntity == null ||
                PlayerUpdate.LocalEntity._entity == null ||
                PlayerUpdate.LocalEntity.IsDead)
            {
                return;
            }

            if (PlayerUpdate.LocalEntity.CurrentWeaponName != "wind_spirit")
            {
                return;
            }

            FindAllWindSpiritTags();
            DetectEnemiesOnAllPaths();
        }

        private void OnGUI()
        {
            // 检查开关
            if (!Config.WindSpiritPath)
                return;

            if (_windSpiritTagPositions.Count == 0)
                return;

            if (PlayerUpdate.LocalEntity == null ||
                PlayerUpdate.LocalEntity.IsDead ||
                PlayerUpdate.MainCamera == null)
                return;

            if (PlayerUpdate.LocalEntity.CurrentWeaponName != "wind_spirit")
                return;

            DrawAllRecallPaths();
        }

        private void FindAllWindSpiritTags()
        {
            var sceneObjectContext = Contexts.sharedInstance?.sceneObject;
            if (sceneObjectContext == null) return;

            foreach (SceneObjectEntity sceneObjectEntity in sceneObjectContext.GetGroup(SceneObjectMatcher.SceneBuff))
            {
                if (sceneObjectEntity == null || !sceneObjectEntity.hasSceneBuff)
                    continue;

                var buffData = sceneObjectEntity.sceneBuff.Data;
                if (buffData == null) continue;

                if (buffData.BufName == "WIND_SPIRIT_TAG")
                {
                    Vector3 gamePos = new Vector3(buffData.X, buffData.Y, buffData.Z);
                    Vector3 unityPos = SSJJMath.VectorCoordConverter.SsjjToUnity(gamePos);
                    _windSpiritTagPositions.Add(unityPos);
                }
            }
        }

        /// <summary>
        /// 检测所有回收路径上的敌人
        /// 射线从风铃发射到摄像机位置，只检测玩家碰撞，无视墙壁
        /// </summary>
        private void DetectEnemiesOnAllPaths()
        {
            if (PlayerUpdate.EntityList == null || PlayerUpdate.EntityList.Count == 0)
                return;

            var pyEngine = Contexts.sharedInstance?.battleRoom?.pyEngine?.PyEngine;
            if (pyEngine == null) return;

            var playerContext = Contexts.sharedInstance?.player;
            if (playerContext == null) return;

            int localTeam = PlayerUpdate.LocalEntity.Team;

            // 获取摄像机位置（射线终点）
            Vector3 cameraUnityPos = PlayerUpdate.MainCamera.transform.position;
            Vector3 cameraGamePos = SSJJMath.VectorCoordConverter.UnityToSsjj(cameraUnityPos);

            foreach (Vector3 tagUnityPos in _windSpiritTagPositions)
            {
                List<EnemyOnPath> enemiesOnThisPath = new List<EnemyOnPath>();

                // 获取风铃位置（射线起点）
                Vector3 tagGamePos = SSJJMath.VectorCoordConverter.UnityToSsjj(tagUnityPos);

                // 计算射线方向（从风铃到摄像机）
                Vector3 direction = (cameraGamePos - tagGamePos);
                float distance = direction.magnitude;
                direction.Normalize();

                // 转换为Vector3D用于射线检测
                Vector3D shotDirection = new Vector3D(direction.x, direction.y, direction.z);

                // 从风铃位置发射射线到摄像机位置，只检测玩家（无视墙壁）
                TraceResult result = BulletTraceToPlayersOnly(
                    pyEngine,
                    tagGamePos,
                    playerContext,
                    distance,
                    shotDirection,
                    localTeam
                );

                // 检查是否击中了敌人玩家
                if (result.EntityId > 0)
                {
                    // 查找对应的玩家实体
                    foreach (var enemy in PlayerUpdate.EntityList)
                    {
                        if (enemy.Id == result.EntityId && enemy.Team != localTeam && !enemy.IsDead)
                        {
                            enemiesOnThisPath.Add(new EnemyOnPath
                            {
                                Player = enemy,
                                Position = SSJJMath.VectorCoordConverter.SsjjToUnity(
                                    new Vector3(
                                        result.EndPos[0],
                                        result.EndPos[1],
                                        result.EndPos[2]
                                    )
                                ),
                                Distance = result.Fraction * distance * 0.01f // 转换为米
                            });
                            break;
                        }
                    }
                }

                EnemiesOnPaths[tagUnityPos] = enemiesOnThisPath;
            }
        }

        /// <summary>
        /// 从指定点发射射线，只检测玩家碰撞（无视墙壁）
        /// </summary>
        private TraceResult BulletTraceToPlayersOnly(
            physics.IPyEngine pyEngine,
            Vector3 startPos,
            PlayerContext otherPlayers,
            float attackDistance,
            Vector3D forward,
            int localTeam)
        {
            Trace trace = TraceObjectPool.AllocateTrace();
            Vector3D start = TraceObjectPool.AllocateVector3D();
            Vector3D end = TraceObjectPool.AllocateVector3D();
            float[] startF3 = TraceObjectPool.AllocateFloat3();
            float[] endF3 = TraceObjectPool.AllocateFloat3();
            float[] mins = TraceObjectPool.AllocateFloat3();
            float[] maxs = TraceObjectPool.AllocateFloat3();
            float[] playerPosF3 = TraceObjectPool.AllocateFloat3();
            TraceResult traceResult = default(TraceResult);

            try
            {
                start.x = startPos.x;
                start.y = startPos.y;
                start.z = startPos.z;

                mins[0] = 0f; mins[1] = 0f; mins[2] = 0f;
                maxs[0] = 0f; maxs[1] = 0f; maxs[2] = 0f;

                VectorUtils.VectorMA(start, (double)attackDistance, forward, end);
                VectorUtils.Vector3DToFloat3(start, startF3);
                VectorUtils.Vector3DToFloat3(end, endF3);

                trace.fraction = 1f;
                trace.entityId = -1;

                foreach (PlayerEntity playerEntity in otherPlayers)
                {
                    if (!playerEntity.hasHitBox || playerEntity.isMyPlayer || playerEntity.isPrediction)
                        continue;

                    if (playerEntity.basicInfo.Current.Team == localTeam)
                        continue;

                    if (playerEntity.basicInfo.Current.IsDead)
                        continue;

                    Vector3 enemyPos = playerEntity.GetCompenstatePos(playerEntity.fpos.Change.GetPosIndex());
                    playerPosF3[0] = enemyPos.x;
                    playerPosF3[1] = enemyPos.y;
                    playerPosF3[2] = enemyPos.z;

                    double scale = (double)playerEntity.headScale.Scale;
                    HitPreliminaryGeo hitPreliminaryGeo = playerEntity.hitBox.HitPreliminaryGeo;

                    if (hitPreliminaryGeo == null || !pyEngine.GetWorld().BulletPreliminaryTrace(startF3, endF3, mins, maxs, playerPosF3, scale, hitPreliminaryGeo))
                    {
                        if (playerEntity.hitBox.HitBoxBrushDirty)
                        {
                            PlayerHitBoxBrushUtility.UpdatePlayerAllHitBoxBrush(playerEntity);
                        }

                        pyEngine.GetEntityManager().UpdateEntityHitBrush(2, playerEntity.move.PyPlayerMove.GetEntity(), playerEntity.hitBox.HitBoxBrush);
                        pyEngine.GetTrace().ClipMoveToPlayerHitBoxes(trace, startF3, endF3, mins, maxs, playerEntity.move.PyPlayerMove.GetEntity(), playerEntity.hitBox.HitBoxBrush);
                    }
                }

                traceResult.CopyFrom(trace);
            }
            finally
            {
                TraceObjectPool.ReturnVector3D(start);
                TraceObjectPool.ReturnVector3D(end);
                TraceObjectPool.ReturnFloat3(startF3);
                TraceObjectPool.ReturnFloat3(endF3);
                TraceObjectPool.ReturnFloat3(mins);
                TraceObjectPool.ReturnFloat3(maxs);
                TraceObjectPool.ReturnFloat3(playerPosF3);
                TraceObjectPool.ReturnTrace(trace);
            }

            return traceResult;
        }

        private void DrawAllRecallPaths()
        {
            Camera cam = PlayerUpdate.MainCamera;

            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
            Ray centerRay = cam.ScreenPointToRay(screenCenter);
            Vector3 lineStartPos = centerRay.GetPoint(cam.nearClipPlane);

            foreach (Vector3 tagPos in _windSpiritTagPositions)
            {
                bool hasEnemyOnPath = EnemiesOnPaths.ContainsKey(tagPos) && EnemiesOnPaths[tagPos].Count > 0;
                DrawRecallPath(tagPos, lineStartPos, hasEnemyOnPath);
            }
        }

        private void DrawRecallPath(Vector3 tagWorldPos, Vector3 lineStartPos, bool hasEnemy)
        {
            // 根据是否有敌人改变颜色
            Color lineColor = hasEnemy
                ? new Color(1f, 0.3f, 0.3f, 0.8f)  // 红色（有敌人）
                : new Color(0f, 1f, 1f, 0.6f);     // 青色（安全）

            // 只绘制3D线条，不显示文字（图标透视已包含风铃信息）
            ImmediateRenderer.DrawLinearTracer(lineStartPos, tagWorldPos, lineColor);
        }
    }
}
