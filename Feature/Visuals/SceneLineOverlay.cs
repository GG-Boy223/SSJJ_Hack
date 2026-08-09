using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Feature.Legit;
using SkyDome.Render;
using SkyDome.Utilities;
using SSJJSceneLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace SkyDome.Feature.Visuals
{
    public sealed class SceneLineOverlay : MonoBehaviour
    {
        private static readonly bool EnableSceneStatusOverlay = false;
        private const float RefreshIntervalSeconds = 1.5f;
        private const float CandidateRefreshIntervalSeconds = 0.2f;
        private const int MaxLineVertices = 240000;
        private const int HighlightPassMaxVertices = 140000;
        private const int CandidateSegmentSamplesPerRefresh = 48;
        private const int MaxWallProbeCandidates = 8;
        private const int CircleSegments = 16;
        private const float CandidateMinSegmentLengthUnits = 18f;
        private const float CandidateMaxSegmentLengthUnits = 640f;
        private const float CandidateMinDistanceUnits = 120f;
        private const float CandidateMaxDistanceUnits = 4200f;
        private const float CandidateMaxHeightDeltaUnits = 180f;
        private const float CandidateMinViewDot = 0.55f;
        private const float CandidateScreenMargin = 48f;
        private const float CandidateHitToleranceScale = 0.65f;
        private const float CandidateMinHitToleranceUnits = 22f;
        private const float CandidateMaxHitToleranceUnits = 90f;
        private const float CandidateDedupWorldDistanceUnits = 100f;
        private const float CandidateMaxDisplayThicknessMeters = 3f;
        private static readonly FieldInfo SurfaceToDrawField = typeof(BaseSceneRender).GetField("_surfaceToDraw", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SceneMeshDictField = typeof(BaseSceneRender).GetField("_sceneMeshDict", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DrawMatrixField = typeof(BaseSceneRender).GetField("_drawMatrix", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Color OverlayCoreColor = new Color(0.58f, 1f, 0.96f, 0.92f);
        private static readonly Color OverlayHighlightColor = new Color(0.88f, 1f, 1f, 0.34f);
        private static readonly Color ThinWallCandidateColor = new Color(1f, 0.82f, 0.24f, 0.96f);
        private static readonly Color ThinWallCandidateGlowColor = new Color(1f, 0.96f, 0.68f, 0.38f);
        private static readonly Color MeasuredWallCandidateColor = new Color(0.95f, 0.98f, 1f, 0.85f);
        private static readonly string[] ExcludedNameKeywords =
        {
            "player",
            "weapon",
            "bullet",
            "effect",
            "particle",
            "muzzle",
            "fire",
            "bip",
            "bone",
            "sky",
            "water",
            "uv2_huxi_player",
            "object001"
        };

        private readonly List<Vector3> _lineVertices = new List<Vector3>(65536);
        private readonly List<WallProbeCandidate> _wallProbeCandidates = new List<WallProbeCandidate>(MaxWallProbeCandidates);
        private readonly List<Vector3> _thinWallCandidateLineVertices = new List<Vector3>(MaxWallProbeCandidates * 2);
        private readonly List<Vector3> _measuredWallCandidateLineVertices = new List<Vector3>(MaxWallProbeCandidates * 2);

        private int _crosshairTraceFrame = -1;
        private ShotTraceInfo _crosshairTraceInfo;
        private int _targetTraceFrame = -1;
        private int _targetTracePlayerId = -1;
        private ShotTraceInfo _targetTraceInfo;
        private GameObject _cachedSceneRoot;
        private float _nextRefreshTime;
        private float _nextCandidateRefreshTime;
        private int _candidateSampleCursor;
        private int _lineGeometryRevision;
        private int _candidateGeometryRevision = -1;
        private Vector3 _lastCandidateShotOrigin;
        private Vector3 _lastCandidateCameraForward;
        private bool _candidateViewSnapshotValid;
        private int _colliderCount;
        private int _meshColliderCount;
        private int _boxColliderCount;
        private int _sphereColliderCount;
        private int _capsuleColliderCount;
        private int _terrainColliderCount;
        private int _meshFilterCount;
        private int _sceneRenderCount;
        private int _visibleSurfaceCount;
        private int _sceneClipSurfaceCount;
        private int _sceneSurfaceFaceCount;
        private int _sceneMeshDictCount;
        private string _rootName = "None";
        private string _dataSource = "None";
        private string _lastLoggedSnapshot;
        private float _nextDebugLogTime;

        public int SegmentCount => _lineVertices.Count / 2;

        private void OnGUI()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            bool needsAimTelemetry = Config.AllowThinWallAim && (Config.Aimbot || Config.Silentbot);
            bool needsTelemetry = Config.ShowSceneLines || Config.ShowWallThickness || needsAimTelemetry;
            if (needsTelemetry)
            {
                PlayerInfo currentTarget = Aimbot._currentTarget;
                PenetrationDamageTelemetry.RefreshEnemyObservations();
                PenetrationDamageTelemetry.Tick(
                    currentTarget,
                    GetCrosshairTraceInfo(),
                    GetCurrentTargetTraceInfo(currentTarget));
            }

            Draw();
        }

        private ShotTraceInfo GetCrosshairTraceInfo()
        {
            int currentFrame = Time.frameCount;
            if (_crosshairTraceFrame == currentFrame)
            {
                return _crosshairTraceInfo;
            }

            _crosshairTraceInfo = PlayerUtility.GetCurrentShotTraceInfo(Config.SpreadPredict);
            _crosshairTraceFrame = currentFrame;
            return _crosshairTraceInfo;
        }

        private ShotTraceInfo GetCurrentTargetTraceInfo(PlayerInfo currentTarget)
        {
            int currentFrame = Time.frameCount;
            int currentTargetId = currentTarget?.Id ?? -1;
            if (_targetTraceFrame == currentFrame && _targetTracePlayerId == currentTargetId)
            {
                return _targetTraceInfo;
            }

            _targetTraceFrame = currentFrame;
            _targetTracePlayerId = currentTargetId;
            _targetTraceInfo = default;

            PlayerInfo localPlayer = PlayerUpdate.LocalEntity;
            if (currentTarget == null || localPlayer == null)
            {
                return _targetTraceInfo;
            }

            if (!PenetrationDamageTelemetry.TryGetTargetAimPoint(currentTarget, out Vector3 targetPosition, out _) ||
                !IsFiniteVector(targetPosition))
            {
                return _targetTraceInfo;
            }

            _targetTraceInfo = PlayerUtility.GetShotTraceInfoToTarget(
                localPlayer,
                currentTarget,
                PenetrationDamageTelemetry.GetLocalShotOrigin(),
                targetPosition);
            return _targetTraceInfo;
        }

        public void Draw()
        {
            if (!Config.ShowSceneLines || PlayerUpdate.MainCamera == null)
            {
                return;
            }

            TryEnsureCache();
            TryRefreshWallProbeCandidates();

            if (Event.current != null && Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (_lineVertices.Count > 0)
            {
                DrawOverlayLines();
            }

            DrawWallProbeCandidates();
            DrawStatus();
        }

        private void DrawOverlayLines()
        {
            // 直接使用批量世界线绘制，避免每帧把大量静态顶点重新投影到屏幕坐标。
            ImmediateRenderer.DrawWorldLines(_lineVertices, OverlayCoreColor, true);

            // 双层叠加能让线更亮，但在线段极多时自动退化成单层，避免高负载地图继续放大开销。
            if (_lineVertices.Count <= HighlightPassMaxVertices)
            {
                ImmediateRenderer.DrawWorldLines(_lineVertices, OverlayHighlightColor, true);
            }
        }

        private void DrawWallProbeCandidates()
        {
            if (_measuredWallCandidateLineVertices.Count > 0)
            {
                ImmediateRenderer.DrawWorldLines(_measuredWallCandidateLineVertices, MeasuredWallCandidateColor, true);
            }

            if (_thinWallCandidateLineVertices.Count > 0)
            {
                ImmediateRenderer.DrawWorldLines(_thinWallCandidateLineVertices, ThinWallCandidateColor, true);
                ImmediateRenderer.DrawWorldLines(_thinWallCandidateLineVertices, ThinWallCandidateGlowColor, true);
            }

            for (int i = 0; i < _wallProbeCandidates.Count; i++)
            {
                WallProbeCandidate candidate = _wallProbeCandidates[i];
                Vector3 screenPoint = ViewportUtility.WorldPointToScreenPoint(candidate.LabelWorldPosition);
                if (!IsCandidateScreenPointVisible(screenPoint))
                {
                    continue;
                }

                ImmediateRenderer.DrawString(
                    new Vector2(screenPoint.x, screenPoint.y - 14f),
                    candidate.DisplayText,
                    candidate.IsThinWallCandidate ? ThinWallCandidateColor : MeasuredWallCandidateColor,
                    true,
                    12);
            }
        }

        private void DrawStatus()
        {
            if (!EnableSceneStatusOverlay)
            {
                return;
            }

            string info =
                $"SceneLines: {_rootName}\n" +
                $"Source: {_dataSource}\n" +
                $"Segments: {SegmentCount}\n" +
                $"Renders/V/C/F: {_sceneRenderCount}/{_visibleSurfaceCount}/{_sceneClipSurfaceCount}/{_sceneSurfaceFaceCount}\n" +
                $"SceneMeshes: {_sceneMeshDictCount}  MeshFilters: {_meshFilterCount}\n" +
                $"Colliders M/B/S/C/T: {_meshColliderCount}/{_boxColliderCount}/{_sphereColliderCount}/{_capsuleColliderCount}/{_terrainColliderCount}\n" +
                $"AllColliders: {_colliderCount}";

            //ImmediateRenderer.DrawString(
            //    new Vector2(Screen.width - 330f, 55f),
            //    info,
            //    Color.cyan,
            //    false,
            //    12);
        }

        private void TryRefreshWallProbeCandidates()
        {
            if (_lineVertices.Count < 2 || PlayerUpdate.MainCamera == null || PlayerUpdate.LocalEntity == null)
            {
                ClearWallProbeCandidates();
                return;
            }

            Vector3 shotOrigin = PenetrationDamageTelemetry.GetLocalShotOrigin();
            Transform cameraTransform = PlayerUpdate.MainCamera.transform;
            Vector3 cameraForward = cameraTransform.forward;
            bool geometryChanged = _candidateGeometryRevision != _lineGeometryRevision;
            bool viewChanged = !_candidateViewSnapshotValid ||
                               Vector3.Distance(_lastCandidateShotOrigin, shotOrigin) > 40f ||
                               Vector3.Dot(_lastCandidateCameraForward, cameraForward) < 0.985f;
            if (geometryChanged || viewChanged)
            {
                _candidateSampleCursor = 0;
            }

            if (!geometryChanged &&
                !viewChanged &&
                Time.unscaledTime < _nextCandidateRefreshTime)
            {
                return;
            }

            RefreshWallProbeCandidates(shotOrigin, cameraTransform.position, cameraForward);
            _candidateGeometryRevision = _lineGeometryRevision;
            _lastCandidateShotOrigin = shotOrigin;
            _lastCandidateCameraForward = cameraForward;
            _candidateViewSnapshotValid = true;
            _nextCandidateRefreshTime = Time.unscaledTime + CandidateRefreshIntervalSeconds;
        }

        private void RefreshWallProbeCandidates(Vector3 shotOrigin, Vector3 cameraPosition, Vector3 cameraForward)
        {
            _wallProbeCandidates.Clear();
            _thinWallCandidateLineVertices.Clear();
            _measuredWallCandidateLineVertices.Clear();

            int segmentCount = _lineVertices.Count / 2;
            if (segmentCount <= 0)
            {
                return;
            }

            int sampleCount = Math.Min(CandidateSegmentSamplesPerRefresh, segmentCount);
            int sampleStep = Math.Max(1, segmentCount / sampleCount);
            int startSegment = segmentCount > 0 ? _candidateSampleCursor % segmentCount : 0;

            for (int i = 0; i < sampleCount; i++)
            {
                int segmentIndex = (startSegment + i * sampleStep) % segmentCount;
                if (!TryBuildWallProbeCandidate(segmentIndex, shotOrigin, cameraPosition, cameraForward, out WallProbeCandidate candidate))
                {
                    continue;
                }

                InsertWallProbeCandidate(candidate);
            }

            _candidateSampleCursor = segmentCount > 0
                ? (startSegment + 1) % segmentCount
                : 0;
            RebuildWallProbeLineBuffers();
        }

        private bool TryBuildWallProbeCandidate(
            int segmentIndex,
            Vector3 shotOrigin,
            Vector3 cameraPosition,
            Vector3 cameraForward,
            out WallProbeCandidate candidate)
        {
            candidate = default;

            int vertexIndex = segmentIndex * 2;
            if (vertexIndex < 0 || vertexIndex + 1 >= _lineVertices.Count)
            {
                return false;
            }

            Vector3 start = _lineVertices[vertexIndex];
            Vector3 end = _lineVertices[vertexIndex + 1];
            Vector3 segment = end - start;
            float segmentLength = segment.magnitude;
            if (segmentLength < CandidateMinSegmentLengthUnits || segmentLength > CandidateMaxSegmentLengthUnits)
            {
                return false;
            }

            Vector3 midpoint = (start + end) * 0.5f;
            if (!IsFiniteVector(midpoint) || Mathf.Abs(midpoint.y - shotOrigin.y) > CandidateMaxHeightDeltaUnits)
            {
                return false;
            }

            Vector3 toMidpoint = midpoint - cameraPosition;
            float cameraDistance = toMidpoint.magnitude;
            if (cameraDistance < CandidateMinDistanceUnits || cameraDistance > CandidateMaxDistanceUnits)
            {
                return false;
            }

            Vector3 toMidpointDir = toMidpoint / cameraDistance;
            if (Vector3.Dot(cameraForward, toMidpointDir) < CandidateMinViewDot)
            {
                return false;
            }

            Vector3 screenPoint = ViewportUtility.WorldPointToScreenPoint(midpoint);
            if (!IsCandidateScreenPointVisible(screenPoint))
            {
                return false;
            }

            float centerDx = Mathf.Abs(screenPoint.x - Screen.width * 0.5f);
            float centerDy = Mathf.Abs(screenPoint.y - Screen.height * 0.5f);
            if (centerDx > Screen.width * 0.43f || centerDy > Screen.height * 0.38f)
            {
                return false;
            }

            float hitToleranceUnits = Mathf.Clamp(
                segmentLength * CandidateHitToleranceScale,
                CandidateMinHitToleranceUnits,
                CandidateMaxHitToleranceUnits);
            if (!PenetrationDamageTelemetry.TrySampleWallThicknessToWorldPoint(midpoint, hitToleranceUnits, out PenetrationDamageTelemetry.WorldWallThicknessSample sample))
            {
                return false;
            }

            if (sample.WallThicknessMeters > CandidateMaxDisplayThicknessMeters &&
                !sample.IsThinWallCandidate)
            {
                return false;
            }

            string displayText = sample.IsThinWallCandidate
                ? $"薄墙 {sample.WallThicknessText}"
                : sample.WallThicknessText;
            float score = BuildWallProbeScore(sample, centerDx, centerDy);
            Vector3 labelWorldPosition = IsFiniteVector(sample.HitPoint)
                ? sample.HitPoint
                : midpoint;
            candidate = new WallProbeCandidate
            {
                Start = start,
                End = end,
                LabelWorldPosition = labelWorldPosition,
                DisplayText = displayText,
                IsThinWallCandidate = sample.IsThinWallCandidate,
                Score = score
            };
            return true;
        }

        private void InsertWallProbeCandidate(WallProbeCandidate candidate)
        {
            for (int i = 0; i < _wallProbeCandidates.Count; i++)
            {
                if (Vector3.Distance(_wallProbeCandidates[i].LabelWorldPosition, candidate.LabelWorldPosition) > CandidateDedupWorldDistanceUnits)
                {
                    continue;
                }

                if (candidate.Score < _wallProbeCandidates[i].Score)
                {
                    _wallProbeCandidates[i] = candidate;
                }

                SortWallProbeCandidatesByScore();
                TrimWallProbeCandidates();
                return;
            }

            _wallProbeCandidates.Add(candidate);
            SortWallProbeCandidatesByScore();
            TrimWallProbeCandidates();
        }

        private void SortWallProbeCandidatesByScore()
        {
            _wallProbeCandidates.Sort((left, right) => left.Score.CompareTo(right.Score));
        }

        private void TrimWallProbeCandidates()
        {
            if (_wallProbeCandidates.Count <= MaxWallProbeCandidates)
            {
                return;
            }

            _wallProbeCandidates.RemoveRange(MaxWallProbeCandidates, _wallProbeCandidates.Count - MaxWallProbeCandidates);
        }

        private void RebuildWallProbeLineBuffers()
        {
            _thinWallCandidateLineVertices.Clear();
            _measuredWallCandidateLineVertices.Clear();

            for (int i = 0; i < _wallProbeCandidates.Count; i++)
            {
                WallProbeCandidate candidate = _wallProbeCandidates[i];
                List<Vector3> targetBuffer = candidate.IsThinWallCandidate
                    ? _thinWallCandidateLineVertices
                    : _measuredWallCandidateLineVertices;
                targetBuffer.Add(candidate.Start);
                targetBuffer.Add(candidate.End);
            }
        }

        private void ClearWallProbeCandidates()
        {
            _wallProbeCandidates.Clear();
            _thinWallCandidateLineVertices.Clear();
            _measuredWallCandidateLineVertices.Clear();
            _candidateViewSnapshotValid = false;
        }

        private static float BuildWallProbeScore(
            PenetrationDamageTelemetry.WorldWallThicknessSample sample,
            float centerDx,
            float centerDy)
        {
            float score = sample.IsThinWallCandidate ? 0f : 1000f;
            if (!sample.HasExactThickness)
            {
                score += 260f;
            }

            score += sample.WallThicknessMeters * 120f;
            score += sample.DistanceMeters * 2.4f;
            score += centerDx * 0.03f;
            score += centerDy * 0.04f;
            return score;
        }

        private static bool IsCandidateScreenPointVisible(Vector3 screenPoint)
        {
            return screenPoint.z > 0.01f &&
                   screenPoint.x >= -CandidateScreenMargin &&
                   screenPoint.y >= -CandidateScreenMargin &&
                   screenPoint.x <= Screen.width + CandidateScreenMargin &&
                   screenPoint.y <= Screen.height + CandidateScreenMargin;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsInfinity(value.z);
        }

        private void TryEnsureCache()
        {
            GameObject sceneRoot = ResolveSceneRoot();
            if (sceneRoot != _cachedSceneRoot || Time.unscaledTime >= _nextRefreshTime)
            {
                RebuildCache(sceneRoot);
            }
        }

        private void RebuildCache(GameObject sceneRoot)
        {
            _cachedSceneRoot = sceneRoot;
            _nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
            _lineVertices.Clear();
            _lineGeometryRevision++;

            _colliderCount = 0;
            _meshColliderCount = 0;
            _boxColliderCount = 0;
            _sphereColliderCount = 0;
            _capsuleColliderCount = 0;
            _terrainColliderCount = 0;
            _meshFilterCount = 0;
            _sceneRenderCount = 0;
            _visibleSurfaceCount = 0;
            _sceneClipSurfaceCount = 0;
            _sceneSurfaceFaceCount = 0;
            _sceneMeshDictCount = 0;
            _rootName = sceneRoot != null ? sceneRoot.name : "Global";
            _dataSource = "None";

            BaseSceneRender[] sceneRenders = CollectSceneRenderers(sceneRoot);
            _sceneRenderCount = sceneRenders.Length;

            AppendVisibleSceneSurfaces(sceneRenders);
            if (_lineVertices.Count > 0)
            {
                _dataSource = "SceneSurfaceVisible";
                WriteDebugLog(sceneRoot);
                return;
            }

            AppendSceneRenderMeshes(sceneRenders);
            if (_lineVertices.Count > 0)
            {
                _dataSource = "SceneMeshDict";
                WriteDebugLog(sceneRoot);
                return;
            }

            AppendSceneClipSurfaces(sceneRenders);
            if (_lineVertices.Count > 0)
            {
                _dataSource = "SceneClipMap";
                WriteDebugLog(sceneRoot);
                return;
            }

            AppendMeshFilters(sceneRoot);
            if (_lineVertices.Count > 0)
            {
                _dataSource = "MeshFilter";
                WriteDebugLog(sceneRoot);
                return;
            }

            AppendColliders(sceneRoot);
            if (_lineVertices.Count > 0)
            {
                _dataSource = "Collider";
            }

            WriteDebugLog(sceneRoot);
        }

        private void WriteDebugLog(GameObject sceneRoot)
        {
            string snapshot = BuildDebugSnapshot(sceneRoot);
            bool hasChanged = !string.Equals(snapshot, _lastLoggedSnapshot, StringComparison.Ordinal);
            if (!hasChanged && Time.unscaledTime < _nextDebugLogTime)
            {
                return;
            }

            _lastLoggedSnapshot = snapshot;
            _nextDebugLogTime = Time.unscaledTime + 10f;
            Logger.LogDebug("SceneLines", snapshot);
        }

        private string BuildDebugSnapshot(GameObject sceneRoot)
        {
            var sb = new StringBuilder(512);
            sb.Append("Root=").Append(_rootName);
            sb.Append(", Source=").Append(_dataSource);
            sb.Append(", Segments=").Append(SegmentCount);
            sb.Append(", Colliders=").Append(_colliderCount);
            sb.Append(", MeshCollider=").Append(_meshColliderCount);
            sb.Append(", BoxCollider=").Append(_boxColliderCount);
            sb.Append(", SphereCollider=").Append(_sphereColliderCount);
            sb.Append(", CapsuleCollider=").Append(_capsuleColliderCount);
            sb.Append(", TerrainCollider=").Append(_terrainColliderCount);
            sb.Append(", MeshFilters=").Append(_meshFilterCount);
            sb.Append(", SceneRenders=").Append(_sceneRenderCount);
            sb.Append(", VisibleSurfaces=").Append(_visibleSurfaceCount);
            sb.Append(", ClipSurfaces=").Append(_sceneClipSurfaceCount);
            sb.Append(", SurfaceFaces=").Append(_sceneSurfaceFaceCount);
            sb.Append(", SceneMeshes=").Append(_sceneMeshDictCount);

            if (sceneRoot != null)
            {
                sb.Append(", RootChildren=").Append(sceneRoot.transform.childCount);
                sb.Append(", RootComponents=");
                AppendComponentNames(sb, sceneRoot.GetComponents<Component>(), 8);
            }
            else
            {
                sb.Append(", RootChildren=-1");
            }

            return sb.ToString();
        }

        private static void AppendComponentNames(StringBuilder sb, Component[] components, int maxCount)
        {
            bool hasWritten = false;
            int writtenCount = 0;

            for (int i = 0; i < components.Length && writtenCount < maxCount; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                if (hasWritten)
                {
                    sb.Append('|');
                }

                sb.Append(component.GetType().Name);
                hasWritten = true;
                writtenCount++;
            }

            if (!hasWritten)
            {
                sb.Append("None");
            }
        }

        private void AppendColliders(GameObject sceneRoot)
        {
            Collider[] colliders = CollectSceneColliders(sceneRoot);
            for (int i = 0; i < colliders.Length; i++)
            {
                AppendCollider(colliders[i]);

                if (_lineVertices.Count >= MaxLineVertices)
                {
                    return;
                }
            }
        }

        private void AppendMeshFilters(GameObject sceneRoot)
        {
            MeshFilter[] meshFilters = sceneRoot != null
                ? sceneRoot.GetComponentsInChildren<MeshFilter>(true)
                : UnityEngine.Object.FindObjectsOfType<MeshFilter>();

            for (int i = 0; i < meshFilters.Length; i++)
            {
                AppendMeshFilter(meshFilters[i]);

                if (_lineVertices.Count >= MaxLineVertices)
                {
                    return;
                }
            }
        }

        private static BaseSceneRender[] CollectSceneRenderers(GameObject sceneRoot)
        {
            BaseSceneRender[] sceneRenders = sceneRoot != null
                ? sceneRoot.GetComponentsInChildren<BaseSceneRender>(true)
                : UnityEngine.Object.FindObjectsOfType<BaseSceneRender>();

            if (sceneRenders.Length > 0)
            {
                return sceneRenders;
            }

            return UnityEngine.Object.FindObjectsOfType<BaseSceneRender>();
        }

        private void AppendVisibleSceneSurfaces(BaseSceneRender[] sceneRenders)
        {
            if (SurfaceToDrawField == null || sceneRenders == null)
            {
                return;
            }

            for (int i = 0; i < sceneRenders.Length; i++)
            {
                BaseSceneRender sceneRender = sceneRenders[i];
                if (sceneRender == null)
                {
                    continue;
                }

                if (!(SurfaceToDrawField.GetValue(sceneRender) is List<CSurface> visibleSurfaces) || visibleSurfaces.Count == 0)
                {
                    continue;
                }

                _visibleSurfaceCount += visibleSurfaces.Count;
                AppendSceneSurfaceList(visibleSurfaces, ResolveSceneDrawMatrix(sceneRender));

                if (_lineVertices.Count >= MaxLineVertices)
                {
                    return;
                }
            }
        }

        private void AppendSceneClipSurfaces(BaseSceneRender[] sceneRenders)
        {
            if (sceneRenders == null)
            {
                return;
            }

            for (int i = 0; i < sceneRenders.Length; i++)
            {
                BaseSceneRender sceneRender = sceneRenders[i];
                if (sceneRender == null)
                {
                    continue;
                }

                SceneClipMap sceneClipMap = sceneRender.GetSceneClipMap();
                if (sceneClipMap == null || sceneClipMap.surfaces == null || sceneClipMap.surfaces.Count == 0)
                {
                    continue;
                }

                _sceneClipSurfaceCount += sceneClipMap.surfaces.Count;
                AppendSceneSurfaceList(sceneClipMap.surfaces, ResolveSceneDrawMatrix(sceneRender));

                if (_lineVertices.Count >= MaxLineVertices)
                {
                    return;
                }
            }
        }

        private void AppendSceneRenderMeshes(BaseSceneRender[] sceneRenders)
        {
            if (SceneMeshDictField == null || sceneRenders == null)
            {
                return;
            }

            for (int i = 0; i < sceneRenders.Length; i++)
            {
                BaseSceneRender sceneRender = sceneRenders[i];
                if (sceneRender == null)
                {
                    continue;
                }

                if (!(SceneMeshDictField.GetValue(sceneRender) is IDictionary sceneMeshDict) || sceneMeshDict.Count == 0)
                {
                    continue;
                }

                Matrix4x4 localToWorld = ResolveSceneDrawMatrix(sceneRender);
                foreach (DictionaryEntry entry in sceneMeshDict)
                {
                    if (!(entry.Value is Mesh mesh))
                    {
                        continue;
                    }

                    _sceneMeshDictCount++;
                    AppendMesh(mesh, localToWorld);

                    if (_lineVertices.Count >= MaxLineVertices)
                    {
                        return;
                    }
                }
            }
        }

        private static Matrix4x4 ResolveSceneDrawMatrix(BaseSceneRender sceneRender)
        {
            if (sceneRender != null && DrawMatrixField != null)
            {
                object value = DrawMatrixField.GetValue(sceneRender);
                if (value is Matrix4x4 drawMatrix)
                {
                    return drawMatrix;
                }
            }

            return sceneRender != null
                ? sceneRender.transform.localToWorldMatrix
                : Matrix4x4.identity;
        }

        private void AppendSceneSurfaceList(IEnumerable<CSurface> surfaces, Matrix4x4 localToWorld)
        {
            if (surfaces == null)
            {
                return;
            }

            foreach (CSurface surface in surfaces)
            {
                AppendSceneSurface(surface, localToWorld);
                if (_lineVertices.Count >= MaxLineVertices)
                {
                    return;
                }
            }
        }

        private void AppendSceneSurface(CSurface surface, Matrix4x4 localToWorld)
        {
            if (surface == null || surface.surfaceFace == null)
            {
                return;
            }

            _sceneSurfaceFaceCount++;
            AppendSceneSurfaceFace(surface.surfaceFace, localToWorld);
        }

        private void AppendSceneSurfaceFace(CSurfaceFace surfaceFace, Matrix4x4 localToWorld)
        {
            if (surfaceFace == null || surfaceFace.points == null || surfaceFace.numPoints < 2)
            {
                return;
            }

            int pointStride = ResolveSurfacePointStride(surfaceFace);
            if (pointStride < 3)
            {
                return;
            }

            if (surfaceFace.indexes != null && surfaceFace.indexes.Count >= 3)
            {
                HashSet<EdgeKey> uniqueEdges = new HashSet<EdgeKey>();
                int triangleIndexCount = surfaceFace.indexes.Count - (surfaceFace.indexes.Count % 3);
                for (int i = 0; i < triangleIndexCount; i += 3)
                {
                    AddSurfaceEdge(uniqueEdges, surfaceFace.points, pointStride, localToWorld, surfaceFace.indexes[i], surfaceFace.indexes[i + 1]);
                    AddSurfaceEdge(uniqueEdges, surfaceFace.points, pointStride, localToWorld, surfaceFace.indexes[i + 1], surfaceFace.indexes[i + 2]);
                    AddSurfaceEdge(uniqueEdges, surfaceFace.points, pointStride, localToWorld, surfaceFace.indexes[i + 2], surfaceFace.indexes[i]);

                    if (_lineVertices.Count >= MaxLineVertices)
                    {
                        return;
                    }
                }

                return;
            }

            AppendSequentialSurfaceEdges(surfaceFace.points, pointStride, surfaceFace.numPoints, localToWorld);
        }

        private static int ResolveSurfacePointStride(CSurfaceFace surfaceFace)
        {
            if (surfaceFace == null || surfaceFace.points == null || surfaceFace.numPoints <= 0)
            {
                return 0;
            }

            int stride = surfaceFace.points.Count / surfaceFace.numPoints;
            return stride >= 3 ? stride : 0;
        }

        private void AddSurfaceEdge(HashSet<EdgeKey> uniqueEdges, IList<float> points, int pointStride, Matrix4x4 localToWorld, int indexA, int indexB)
        {
            if (indexA < 0 || indexB < 0 || indexA == indexB)
            {
                return;
            }

            EdgeKey edge = new EdgeKey(indexA, indexB);
            if (!uniqueEdges.Add(edge))
            {
                return;
            }

            if (!TryGetSurfacePoint(points, pointStride, indexA, out Vector3 start) ||
                !TryGetSurfacePoint(points, pointStride, indexB, out Vector3 end))
            {
                return;
            }

            AddLine(localToWorld.MultiplyPoint3x4(start), localToWorld.MultiplyPoint3x4(end));
        }

        private void AppendSequentialSurfaceEdges(IList<float> points, int pointStride, int pointCount, Matrix4x4 localToWorld)
        {
            if (pointCount < 2)
            {
                return;
            }

            for (int i = 0; i < pointCount; i++)
            {
                int nextIndex = (i + 1) % pointCount;
                if (!TryGetSurfacePoint(points, pointStride, i, out Vector3 start) ||
                    !TryGetSurfacePoint(points, pointStride, nextIndex, out Vector3 end))
                {
                    continue;
                }

                AddLine(localToWorld.MultiplyPoint3x4(start), localToWorld.MultiplyPoint3x4(end));
                if (_lineVertices.Count >= MaxLineVertices)
                {
                    return;
                }
            }
        }

        private static bool TryGetSurfacePoint(IList<float> points, int pointStride, int pointIndex, out Vector3 point)
        {
            point = default;

            if (points == null || pointStride < 3 || pointIndex < 0)
            {
                return false;
            }

            int offset = pointIndex * pointStride;
            if (offset < 0 || offset + 2 >= points.Count)
            {
                return false;
            }

            point = new Vector3(points[offset], points[offset + 1], points[offset + 2]);
            return true;
        }

        private Collider[] CollectSceneColliders(GameObject sceneRoot)
        {
            if (sceneRoot != null)
            {
                Collider[] rootColliders = sceneRoot.GetComponentsInChildren<Collider>(true);
                if (rootColliders.Length > 0)
                {
                    return rootColliders;
                }
            }

            return UnityEngine.Object.FindObjectsOfType<Collider>();
        }

        private void AppendCollider(Collider collider)
        {
            if (!ShouldUseCollider(collider))
            {
                return;
            }

            _colliderCount++;

            if (collider is MeshCollider meshCollider)
            {
                _meshColliderCount++;
                AppendMesh(meshCollider.sharedMesh, meshCollider.transform.localToWorldMatrix);
                return;
            }

            if (collider is BoxCollider boxCollider)
            {
                _boxColliderCount++;
                AppendBoxCollider(boxCollider);
                return;
            }

            if (collider is SphereCollider sphereCollider)
            {
                _sphereColliderCount++;
                AppendSphereCollider(sphereCollider);
                return;
            }

            if (collider is CapsuleCollider capsuleCollider)
            {
                _capsuleColliderCount++;
                AppendCapsuleCollider(capsuleCollider);
                return;
            }

            if (collider is TerrainCollider terrainCollider)
            {
                _terrainColliderCount++;
                AppendBounds(terrainCollider.bounds);
            }
        }

        private void AppendMeshFilter(MeshFilter meshFilter)
        {
            if (!ShouldUseMeshFilter(meshFilter))
            {
                return;
            }

            _meshFilterCount++;
            AppendMesh(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix);
        }

        private static bool ShouldUseCollider(Collider collider)
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                return false;
            }

            if (collider.attachedRigidbody != null || !collider.gameObject.activeInHierarchy)
            {
                return false;
            }

            return !ShouldExcludeName(collider.gameObject.name);
        }

        private static bool ShouldUseMeshFilter(MeshFilter meshFilter)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            GameObject gameObject = meshFilter.gameObject;
            if (!gameObject.activeInHierarchy || ShouldExcludeName(gameObject.name))
            {
                return false;
            }

            Renderer renderer = meshFilter.GetComponent<Renderer>();
            return renderer != null && renderer.enabled;
        }

        private static bool ShouldExcludeName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            string lowerName = objectName.ToLowerInvariant();
            for (int i = 0; i < ExcludedNameKeywords.Length; i++)
            {
                if (lowerName.Contains(ExcludedNameKeywords[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void AppendMesh(Mesh mesh, Matrix4x4 localToWorld)
        {
            if (mesh == null)
            {
                return;
            }

            Vector3[] vertices;
            int[] triangles;

            try
            {
                vertices = mesh.vertices;
                triangles = mesh.triangles;
            }
            catch (Exception)
            {
                return;
            }

            if (vertices == null || triangles == null || triangles.Length < 3)
            {
                return;
            }

            HashSet<EdgeKey> uniqueEdges = new HashSet<EdgeKey>();
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                AddMeshEdge(uniqueEdges, localToWorld, vertices, triangles[i], triangles[i + 1]);
                AddMeshEdge(uniqueEdges, localToWorld, vertices, triangles[i + 1], triangles[i + 2]);
                AddMeshEdge(uniqueEdges, localToWorld, vertices, triangles[i + 2], triangles[i]);

                if (_lineVertices.Count >= MaxLineVertices)
                {
                    return;
                }
            }
        }

        private void AddMeshEdge(HashSet<EdgeKey> uniqueEdges, Matrix4x4 localToWorld, Vector3[] vertices, int indexA, int indexB)
        {
            if (indexA < 0 || indexB < 0 || indexA >= vertices.Length || indexB >= vertices.Length || indexA == indexB)
            {
                return;
            }

            EdgeKey edge = new EdgeKey(indexA, indexB);
            if (!uniqueEdges.Add(edge))
            {
                return;
            }

            AddLine(localToWorld.MultiplyPoint3x4(vertices[indexA]), localToWorld.MultiplyPoint3x4(vertices[indexB]));
        }

        private void AppendBoxCollider(BoxCollider boxCollider)
        {
            Vector3 center = boxCollider.center;
            Vector3 extents = boxCollider.size * 0.5f;
            Transform transform = boxCollider.transform;

            Vector3[] corners =
            {
                transform.TransformPoint(center + new Vector3(-extents.x, -extents.y, -extents.z)),
                transform.TransformPoint(center + new Vector3(extents.x, -extents.y, -extents.z)),
                transform.TransformPoint(center + new Vector3(extents.x, -extents.y, extents.z)),
                transform.TransformPoint(center + new Vector3(-extents.x, -extents.y, extents.z)),
                transform.TransformPoint(center + new Vector3(-extents.x, extents.y, -extents.z)),
                transform.TransformPoint(center + new Vector3(extents.x, extents.y, -extents.z)),
                transform.TransformPoint(center + new Vector3(extents.x, extents.y, extents.z)),
                transform.TransformPoint(center + new Vector3(-extents.x, extents.y, extents.z))
            };

            AddLine(corners[0], corners[1]);
            AddLine(corners[1], corners[2]);
            AddLine(corners[2], corners[3]);
            AddLine(corners[3], corners[0]);
            AddLine(corners[4], corners[5]);
            AddLine(corners[5], corners[6]);
            AddLine(corners[6], corners[7]);
            AddLine(corners[7], corners[4]);
            AddLine(corners[0], corners[4]);
            AddLine(corners[1], corners[5]);
            AddLine(corners[2], corners[6]);
            AddLine(corners[3], corners[7]);
        }

        private void AppendSphereCollider(SphereCollider sphereCollider)
        {
            Transform transform = sphereCollider.transform;
            Vector3 center = transform.TransformPoint(sphereCollider.center);
            float radius = sphereCollider.radius * MaxAbs(transform.lossyScale);

            Vector3 right = transform.right.normalized;
            Vector3 up = transform.up.normalized;
            Vector3 forward = transform.forward.normalized;

            AddCircle(center, right, up, radius, CircleSegments);
            AddCircle(center, right, forward, radius, CircleSegments);
            AddCircle(center, up, forward, radius, CircleSegments);
        }

        private void AppendCapsuleCollider(CapsuleCollider capsuleCollider)
        {
            Transform transform = capsuleCollider.transform;
            Vector3 center = transform.TransformPoint(capsuleCollider.center);

            Vector3 axis;
            Vector3 basisA;
            Vector3 basisB;
            float axisScale;
            float radialScaleA;
            float radialScaleB;

            switch (capsuleCollider.direction)
            {
                case 0:
                    axis = transform.right.normalized;
                    basisA = transform.up.normalized;
                    basisB = transform.forward.normalized;
                    axisScale = Mathf.Abs(transform.lossyScale.x);
                    radialScaleA = Mathf.Abs(transform.lossyScale.y);
                    radialScaleB = Mathf.Abs(transform.lossyScale.z);
                    break;
                case 2:
                    axis = transform.forward.normalized;
                    basisA = transform.right.normalized;
                    basisB = transform.up.normalized;
                    axisScale = Mathf.Abs(transform.lossyScale.z);
                    radialScaleA = Mathf.Abs(transform.lossyScale.x);
                    radialScaleB = Mathf.Abs(transform.lossyScale.y);
                    break;
                default:
                    axis = transform.up.normalized;
                    basisA = transform.right.normalized;
                    basisB = transform.forward.normalized;
                    axisScale = Mathf.Abs(transform.lossyScale.y);
                    radialScaleA = Mathf.Abs(transform.lossyScale.x);
                    radialScaleB = Mathf.Abs(transform.lossyScale.z);
                    break;
            }

            float radius = capsuleCollider.radius * Mathf.Max(radialScaleA, radialScaleB);
            float totalHeight = capsuleCollider.height * axisScale;
            float halfCylinder = Mathf.Max(0f, totalHeight * 0.5f - radius);

            Vector3 topCenter = center + axis * halfCylinder;
            Vector3 bottomCenter = center - axis * halfCylinder;

            AddCircle(topCenter, basisA, basisB, radius, CircleSegments);
            AddCircle(bottomCenter, basisA, basisB, radius, CircleSegments);
            AddCircle(center, basisA, basisB, radius, CircleSegments);

            AddLine(topCenter + basisA * radius, bottomCenter + basisA * radius);
            AddLine(topCenter - basisA * radius, bottomCenter - basisA * radius);
            AddLine(topCenter + basisB * radius, bottomCenter + basisB * radius);
            AddLine(topCenter - basisB * radius, bottomCenter - basisB * radius);
        }

        private void AppendBounds(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            Vector3[] corners =
            {
                center + new Vector3(-extents.x, -extents.y, -extents.z),
                center + new Vector3(extents.x, -extents.y, -extents.z),
                center + new Vector3(extents.x, -extents.y, extents.z),
                center + new Vector3(-extents.x, -extents.y, extents.z),
                center + new Vector3(-extents.x, extents.y, -extents.z),
                center + new Vector3(extents.x, extents.y, -extents.z),
                center + new Vector3(extents.x, extents.y, extents.z),
                center + new Vector3(-extents.x, extents.y, extents.z)
            };

            AddLine(corners[0], corners[1]);
            AddLine(corners[1], corners[2]);
            AddLine(corners[2], corners[3]);
            AddLine(corners[3], corners[0]);
            AddLine(corners[4], corners[5]);
            AddLine(corners[5], corners[6]);
            AddLine(corners[6], corners[7]);
            AddLine(corners[7], corners[4]);
            AddLine(corners[0], corners[4]);
            AddLine(corners[1], corners[5]);
            AddLine(corners[2], corners[6]);
            AddLine(corners[3], corners[7]);
        }

        private void AddCircle(Vector3 center, Vector3 axisA, Vector3 axisB, float radius, int segments)
        {
            if (radius <= 0f || segments < 3)
            {
                return;
            }

            Vector3 previousPoint = center + axisA * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                Vector3 currentPoint = center + (axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)) * radius;
                AddLine(previousPoint, currentPoint);
                previousPoint = currentPoint;

                if (_lineVertices.Count >= MaxLineVertices)
                {
                    return;
                }
            }
        }

        private void AddLine(Vector3 start, Vector3 end)
        {
            if (_lineVertices.Count + 2 > MaxLineVertices)
            {
                return;
            }

            _lineVertices.Add(start);
            _lineVertices.Add(end);
        }

        private static GameObject ResolveSceneRoot()
        {
            try
            {
                if (Contexts.sharedInstance != null &&
                    Contexts.sharedInstance.sceneObject != null &&
                    Contexts.sharedInstance.sceneObject.sceneMapEntity != null)
                {
                    var sceneMapEntity = Contexts.sharedInstance.sceneObject.sceneMapEntity;
                    if (sceneMapEntity.hasMapUnityObjects && sceneMapEntity.mapUnityObjects.MapObject != null)
                    {
                        return sceneMapEntity.mapUnityObjects.MapObject;
                    }
                }
            }
            catch (Exception)
            {
            }

            try
            {
                return GameObject.FindGameObjectWithTag("SceneMap") ?? GameObject.Find("UnitySceneRoot");
            }
            catch (Exception)
            {
                return GameObject.Find("UnitySceneRoot");
            }
        }

        private static float MaxAbs(Vector3 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private struct WallProbeCandidate
        {
            public Vector3 Start;
            public Vector3 End;
            public Vector3 LabelWorldPosition;
            public string DisplayText;
            public bool IsThinWallCandidate;
            public float Score;
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public EdgeKey(int a, int b)
            {
                if (a < b)
                {
                    A = a;
                    B = b;
                }
                else
                {
                    A = b;
                    B = a;
                }
            }

            private int A { get; }
            private int B { get; }

            public bool Equals(EdgeKey other)
            {
                return A == other.A && B == other.B;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A * 397) ^ B;
                }
            }
        }
    }
}
