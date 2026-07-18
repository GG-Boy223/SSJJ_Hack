using System;
using Assets.Sources.Utils.Weapon;
using physics.trace;
using SSJJMath;
using SSJJPhysics;
using UnityEngine;
using PhysicsTrace = physics.trace.Trace;

namespace SkyDome.Feature.Backtrack
{
    internal static class BacktrackTraceUtility
    {
        [ThreadStatic]
        private static float[] _mins;

        [ThreadStatic]
        private static float[] _maxs;

        [ThreadStatic]
        private static float[] _traceStart;

        [ThreadStatic]
        private static float[] _traceEnd;

        private static IPyWorld _world;
        private static PhysicsTrace _trace;

        private static void EnsureBuffers()
        {
            if (_mins == null)
            {
                _mins = new float[3];
            }
            if (_maxs == null)
            {
                _maxs = new float[3];
            }
            if (_traceStart == null)
            {
                _traceStart = new float[3];
            }
            if (_traceEnd == null)
            {
                _traceEnd = new float[3];
            }
        }

        private static PhysicsTrace GetTrace()
        {
            if (_trace == null)
            {
                _trace = TraceObjectPool.AllocateTrace();
            }
            _trace.fraction = 1f;
            _trace.surfaceFlags = 0;
            return _trace;
        }

        private static IPyWorld GetWorld()
        {
            if (_world == null)
            {
                BattleRoomContext battleRoom = Contexts.sharedInstance.battleRoom;
                if (battleRoom != null &&
                    battleRoom.pyEngine != null &&
                    battleRoom.pyEngine.PyEngine != null)
                {
                    _world = battleRoom.pyEngine.PyEngine.GetWorld();
                }
            }
            return _world;
        }

        public static bool CanAim(
            PlayerEntity shooter,
            PlayerEntity target,
            Vector3 startPosition,
            Vector3 targetPosition,
            bool allowWorldTrace)
        {
            if (shooter == null ||
                target == null ||
                shooter.IsDead() ||
                target.IsDead())
            {
                return false;
            }

            Contexts contexts = Contexts.sharedInstance;
            if (contexts == null ||
                contexts.battleRoom == null ||
                contexts.battleRoom.pyEngine == null ||
                contexts.battleRoom.pyEngine.PyEngine == null)
            {
                return false;
            }

            if (shooter.move == null ||
                shooter.move.PyPlayerMove == null ||
                shooter.move.PyPlayerMove.GetEntity() == null)
            {
                return false;
            }
            if (target.move == null ||
                target.move.PyPlayerMove == null ||
                target.move.PyPlayerMove.GetEntity() == null)
            {
                return false;
            }

            Vector3 delta = targetPosition - startPosition;
            if (delta.sqrMagnitude < 0.001f)
            {
                return false;
            }

            EnsureBuffers();
            int targetId = target.GetId();
            Vector3 forward = VectorCoordConverter.UnityToSsjj(delta.normalized);
            TraceResult bulletTrace;
            try
            {
                bulletTrace = FireUtility.BulletTraceNormal(
                    contexts.battleRoom.pyEngine.PyEngine,
                    shooter,
                    200000f,
                    forward,
                    _mins,
                    _maxs);
            }
            catch
            {
                return false;
            }

            if (bulletTrace.EntityId == targetId)
            {
                return true;
            }
            if (!allowWorldTrace)
            {
                return false;
            }

            try
            {
                IPyWorld world = GetWorld();
                if (world == null)
                {
                    return false;
                }
                if (bulletTrace.EndPos == null || bulletTrace.EndPos.Length < 3)
                {
                    return false;
                }

                PhysicsTrace trace = GetTrace();
                Vector3 traceStart = VectorCoordConverter.UnityToSsjj(targetPosition);
                Vector3 bulletEnd = new Vector3(
                    bulletTrace.EndPos[0],
                    bulletTrace.EndPos[1],
                    bulletTrace.EndPos[2]);

                _traceStart[0] = traceStart.x;
                _traceStart[1] = traceStart.y;
                _traceStart[2] = traceStart.z;
                _traceEnd[0] = bulletEnd.x;
                _traceEnd[1] = bulletEnd.y;
                _traceEnd[2] = bulletEnd.z;

                world.Trace(
                    trace,
                    _traceStart,
                    _mins,
                    _maxs,
                    _traceEnd,
                    100663299,
                    target.move.PyPlayerMove.GetEntity(),
                    shooter.move.PyPlayerMove.GetEntity(),
                    0);

                if (trace.endPos == null || trace.endPos.Length < 3)
                {
                    return false;
                }

                Vector3 worldEnd = new Vector3(
                    trace.endPos[0],
                    trace.endPos[1],
                    trace.endPos[2]);
                float distance = (bulletEnd - worldEnd).sqrMagnitude / 100f;
                bool bulletNoHit = (bulletTrace.SurfaceFlags & 0x1000) != 0;
                bool worldNoHit = (trace.surfaceFlags & 0x1000) != 0;
                bool bulletNoSurface = bulletTrace.SurfaceFlags == 0;
                bool worldNoSurface = trace.surfaceFlags == 0;

                if (!bulletNoHit && !worldNoHit)
                {
                    if (!bulletNoSurface && !worldNoSurface)
                    {
                        return distance < 1260.25f;
                    }
                    return distance < 64f;
                }
                return distance <= 0f;
            }
            catch
            {
                return false;
            }
        }
    }
}
