using Assets.Sources.Components.Interface.Info.Weapon;
using Assets.Sources.Networking.Server;
using Entitas;
using NetData;
using SSJJBase.String;
using SSJJUserCmd;
using SkyDome.Cfg;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Feature.Backtrack
{
    internal sealed class BacktrackRecord
    {
        public int EntityId;
        public Vector3 BodyPosition;
        public float CaptureTime;
        public int FrameNumber;
        public bool HasBonePosition;
        public Vector3 HeadPosition;
        public Vector3 SpinePosition;
    }

    internal sealed class BacktrackTargetHistory
    {
        public PlayerEntity Player;
        public float LastRecordTime;
        public int MissingChecks;
        public int ModelInstanceId;
        public Transform HeadTransform;
        public Transform SpineTransform;
        public readonly BacktrackRecord[] Records = new BacktrackRecord[128];
        public int WriteIndex = -1;
        public int Count;
        public int ValidCacheFrame = -1;
        public int ValidCacheWindow = -1;
        public float ValidCacheReferenceTime = -1f;
        public readonly List<BacktrackRecord> ValidRecords = new List<BacktrackRecord>(128);
        public int DisplayCacheFrame = -1;
        public readonly List<BacktrackRecord> DisplayRecords = new List<BacktrackRecord>(16);

        public BacktrackTargetHistory(PlayerEntity player)
        {
            Player = player;
            for (int i = 0; i < Records.Length; i++)
            {
                Records[i] = new BacktrackRecord
                {
                    EntityId = player.GetId()
                };
            }
        }

        public void UpdateTransforms()
        {
            if (Player != null && Player.hasThirdPersonUnityObjects)
            {
                Transform bodyTransform = Player.thirdPersonUnityObjects.ThirdTran?.BodyTransform;
                if (bodyTransform != null)
                {
                    int instanceId = bodyTransform.GetInstanceID();
                    if (instanceId != ModelInstanceId)
                    {
                        ModelInstanceId = instanceId;
                        HeadTransform = BacktrackBoneUtility.GetTransform(Player, "Bip01_Head");
                        SpineTransform = BacktrackBoneUtility.GetTransform(Player, "Bip01_Spine1");
                        if (SpineTransform == null)
                        {
                            SpineTransform = BacktrackBoneUtility.GetTransform(Player, "Bip01_Spine");
                        }
                        if (SpineTransform == null)
                        {
                            SpineTransform = BacktrackBoneUtility.GetTransform(Player, "Bip01_Neck");
                        }
                    }
                }
                else
                {
                    ModelInstanceId = 0;
                }
            }
            else
            {
                ModelInstanceId = 0;
            }
        }
    }

    internal sealed class BacktrackSelection
    {
        public bool AutoAttackActive;
        public int RecordIndex = -1;
        public float RecordTime;
        public int TargetEntityId = -1;
        public int AgeMilliseconds;
        public int PacketCount = -1;

        public void Reset()
        {
            AutoAttackActive = false;
            RecordIndex = -1;
            TargetEntityId = -1;
            AgeMilliseconds = 0;
            PacketCount = -1;
            RecordTime = 0f;
        }
    }

    internal static class BacktrackAimState
    {
        public static readonly BacktrackSelection Selection = new BacktrackSelection();

        public static bool AutoAttackActive
        {
            get => Selection.AutoAttackActive;
            set => Selection.AutoAttackActive = value;
        }

        public static int RecordIndex
        {
            get => Selection.RecordIndex;
            set => Selection.RecordIndex = value;
        }

        public static float RecordTime
        {
            get => Selection.RecordTime;
            set => Selection.RecordTime = value;
        }

        public static int TargetEntityId
        {
            get => Selection.TargetEntityId;
            set => Selection.TargetEntityId = value;
        }

        public static int AgeMilliseconds
        {
            get => Selection.AgeMilliseconds;
            set => Selection.AgeMilliseconds = value;
        }

        public static int PacketCount
        {
            get => Selection.PacketCount;
            set => Selection.PacketCount = value;
        }

        public static void SelectFromAimbot(
            int recordIndex,
            BacktrackRecord record,
            int entityId,
            bool autoAttack)
        {
            Selection.AutoAttackActive = autoAttack;
            Selection.RecordIndex = recordIndex;
            Selection.RecordTime = record.CaptureTime;
            Selection.TargetEntityId = entityId;
            Selection.AgeMilliseconds = (int)(
                (Time.realtimeSinceStartup - record.CaptureTime) * 1000f);
            Selection.PacketCount = recordIndex + 1;
        }

        public static void SelectFromSilentbot(
            int recordIndex,
            BacktrackRecord record,
            int entityId)
        {
            Selection.RecordIndex = recordIndex;
            Selection.RecordTime = record.CaptureTime;
            Selection.TargetEntityId = entityId;
            Selection.AgeMilliseconds = (int)(
                (Time.realtimeSinceStartup - record.CaptureTime) * 1000f);
        }

        public static void Reset()
        {
            Selection.Reset();
        }
    }

    internal static class BacktrackEntityState
    {
        private static readonly Vector3 TargetOffset = new Vector3(0f, 150f, 0f);

        public static List<IEntity> Entities;
        public static int TargetScreenDistance = 10000;
        public static PlayerEntity HeldTarget;
        public static Camera Camera;
        public static PlayerEntity LocalPlayer;

        public static void Update()
        {
            try
            {
                UpdateCamera();
                UpdateEntities();
                UpdateLocalPlayer();
                UpdateHeldTarget();
            }
            catch (Exception)
            {
                HeldTarget = null;
                TargetScreenDistance = 10000;
            }
        }

        private static void UpdateEntities()
        {
            if (Contexts.sharedInstance != null && Contexts.sharedInstance.player != null)
            {
                Entities = Contexts.sharedInstance.player.GetEntities();
            }
            else
            {
                Entities = null;
            }
        }

        private static void UpdateCamera()
        {
            if (Contexts.sharedInstance != null &&
                Contexts.sharedInstance.worldCamera != null &&
                Contexts.sharedInstance.worldCamera.unityObjects != null)
            {
                Camera = Contexts.sharedInstance.worldCamera.unityObjects.mainCamera;
            }
            else
            {
                Camera = null;
            }
        }

        private static void UpdateLocalPlayer()
        {
            LocalPlayer = null;
            if (Entities == null)
            {
                return;
            }

            int count = Entities.Count;
            for (int i = 0; i < count; i++)
            {
                PlayerEntity player = Entities[i] as PlayerEntity;
                if (player != null && player.IsMySelf())
                {
                    LocalPlayer = player;
                    return;
                }
            }
        }

        private static void UpdateHeldTarget()
        {
            if (LocalPlayer != null && Camera != null && Entities != null && !LocalPlayer.IsDead())
            {
                if (BacktrackAimState.AutoAttackActive && HeldTarget != null)
                {
                    if (!HeldTarget.IsDead() && HeldTarget.hasHitBox && HeldTarget.hasThirdPersonUnityObjects)
                    {
                        return;
                    }

                    HeldTarget = null;
                    TargetScreenDistance = 10000;
                }

                PlayerEntity selected = null;
                int selectedDistance = 10000;
                int localTeam = LocalPlayer.GetTeam();
                float centerX = Screen.width * 0.5f;
                float centerY = Screen.height * 0.5f;
                int count = Entities.Count;

                for (int i = 0; i < count; i++)
                {
                    PlayerEntity player = Entities[i] as PlayerEntity;
                    if (player == null ||
                        player.IsMySelf() ||
                        player.IsDead() ||
                        player.GetTeam() == localTeam ||
                        !player.hasHitBox ||
                        !player.hasThirdPersonUnityObjects ||
                        BacktrackBoneUtility.ShouldSkipPlayer(player))
                    {
                        continue;
                    }

                    Vector3 bodyPosition = BacktrackCoordinateUtility.ToUnity(
                        BacktrackCoordinateUtility.GetRawPosition(player)) + TargetOffset;
                    Vector3 screenPosition = Camera.WorldToScreenPoint(bodyPosition);
                    if (screenPosition.z > 0f)
                    {
                        int distance = (int)(Math.Abs(screenPosition.x - centerX) +
                            Math.Abs(screenPosition.y - centerY));
                        if (distance < selectedDistance)
                        {
                            selectedDistance = distance;
                            selected = player;
                        }
                    }
                }

                HeldTarget = selected;
                TargetScreenDistance = selectedDistance;
            }
            else
            {
                HeldTarget = null;
                TargetScreenDistance = 10000;
            }
        }
    }

    internal static class BacktrackCoordinateUtility
    {
        public static Vector3 GetRawPosition(PlayerEntity player)
        {
            if (player == null)
            {
                return Vector3.zero;
            }

            return new Vector3((float)player.GetX(), (float)player.GetY(), (float)player.GetZ());
        }

        public static Vector3 ToUnity(Vector3 position)
        {
            return new Vector3(0f - position.y, position.z, position.x);
        }
    }

    internal static class BacktrackBoneUtility
    {
        private sealed class BoneCache
        {
            public int EntityId;
            public object WeaponInfo;
            public string WeaponName;
            public int BodyInstanceId;
            public bool IsSpecialWeapon;
            public Transform HeadTransform;
            public string BoneName;
            public float LastUpdateTime;
            public string Source;
        }

        private static readonly string[] BoneNames =
        {
            "Bip01_Head", "Bip01_Neck", "Bip01_Spine", "Bip01_Pelvis",
            "Bip01_L_UpperArm", "Bip01_L_Forearm", "Bip01_L_Hand",
            "Bip01_R_UpperArm", "Bip01_R_Forearm", "Bip01_R_Hand",
            "Bip01_L_Thigh", "Bip01_L_Calf", "Bip01_L_Foot",
            "Bip01_R_Thigh", "Bip01_R_Calf", "Bip01_R_Foot"
        };

        private static readonly string[] SpecialBoneNames =
        {
            "xinzang", "Bone05", "Bone06", "Bone09", "Bone07"
        };

        private static readonly string[] FallbackBoneNames =
        {
            "Bip01_Neck", "Bip01_Spine1", "Bip01_Spine", "Bip01_Head"
        };

        private static readonly int[] BoneHashes = CreateHashes(BoneNames);
        private static readonly int[] SpecialBoneHashes = CreateHashes(SpecialBoneNames);
        private static readonly int[] FallbackBoneHashes = CreateHashes(FallbackBoneNames);
        private static readonly Dictionary<string, int> DynamicHashes =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, BoneCache> Caches =
            new Dictionary<int, BoneCache>(32);

        private static int[] CreateHashes(string[] names)
        {
            int[] hashes = new int[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                hashes[i] = new IgnoreCaseString(names[i]).GetHashCode();
            }
            return hashes;
        }

        private static Transform FindRecursive(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }
            if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindRecursive(root.GetChild(i), name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private static Transform ResolveHead(PlayerEntity player)
        {
            if (player == null)
            {
                return null;
            }

            int entityId = player.GetId();
            if (!Caches.TryGetValue(entityId, out BoneCache cache))
            {
                cache = new BoneCache
                {
                    EntityId = entityId
                };
                Caches[entityId] = cache;
            }

            IEntitsWeaponInfo weaponInfo = player.currentWeapon?.WeaponInfo;
            string weaponName = weaponInfo == null ? string.Empty : weaponInfo.StringName;
            Transform bodyTransform = null;
            int bodyInstanceId = 0;
            if (player.thirdPersonUnityObjects != null && player.thirdPersonUnityObjects.ThirdTran != null)
            {
                bodyTransform = player.thirdPersonUnityObjects.ThirdTran.BodyTransform;
                if (bodyTransform != null)
                {
                    bodyInstanceId = bodyTransform.GetInstanceID();
                }
            }

            float now = Time.realtimeSinceStartup;
            if (now - cache.LastUpdateTime > 0.5f ||
                cache.WeaponInfo != weaponInfo ||
                !string.Equals(cache.WeaponName, weaponName, StringComparison.Ordinal) ||
                cache.BodyInstanceId != bodyInstanceId ||
                cache.HeadTransform == null)
            {
                cache.LastUpdateTime = now;
                cache.WeaponInfo = weaponInfo;
                cache.WeaponName = weaponName;
                cache.BodyInstanceId = bodyInstanceId;
                cache.HeadTransform = null;
                cache.BoneName = string.Empty;
                cache.IsSpecialWeapon = false;
                cache.Source = string.Empty;

                if (weaponInfo != null &&
                    !string.IsNullOrEmpty(weaponName) &&
                    weaponName.IndexOf("rpg_by_parasitism", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cache.IsSpecialWeapon = true;
                }

                Dictionary<int, Transform> transforms = player.hitBox?.BonetTransform;
                if (cache.IsSpecialWeapon)
                {
                    for (int i = 0; i < SpecialBoneNames.Length; i++)
                    {
                        if (transforms != null &&
                            transforms.TryGetValue(SpecialBoneHashes[i], out Transform transform) &&
                            transform != null)
                        {
                            cache.HeadTransform = transform;
                            cache.BoneName = SpecialBoneNames[i];
                            cache.Source = "SpecialDict";
                            break;
                        }
                    }

                    if (cache.HeadTransform == null && bodyTransform != null)
                    {
                        for (int i = 0; i < SpecialBoneNames.Length; i++)
                        {
                            Transform transform = FindRecursive(bodyTransform, SpecialBoneNames[i]);
                            if (transform != null)
                            {
                                cache.HeadTransform = transform;
                                cache.BoneName = SpecialBoneNames[i];
                                cache.Source = "SpecialRecurse";
                                break;
                            }
                        }
                    }

                    if (cache.HeadTransform == null)
                    {
                        for (int i = 0; i < FallbackBoneNames.Length; i++)
                        {
                            if (transforms != null &&
                                transforms.TryGetValue(FallbackBoneHashes[i], out Transform transform) &&
                                transform != null)
                            {
                                cache.HeadTransform = transform;
                                cache.BoneName = FallbackBoneNames[i];
                                cache.Source = "FallbackDict";
                                break;
                            }
                        }

                        if (cache.HeadTransform == null && bodyTransform != null)
                        {
                            for (int i = 0; i < FallbackBoneNames.Length; i++)
                            {
                                Transform transform = FindRecursive(bodyTransform, FallbackBoneNames[i]);
                                if (transform != null)
                                {
                                    cache.HeadTransform = transform;
                                    cache.BoneName = FallbackBoneNames[i];
                                    cache.Source = "FallbackRecurse";
                                    break;
                                }
                            }
                        }
                    }
                }

                if (cache.HeadTransform == null)
                {
                    cache.Source = cache.IsSpecialWeapon
                        ? "AllSpecialFailed_NormalHead"
                        : "NormalHead";
                    if (transforms != null &&
                        transforms.TryGetValue(BoneHashes[0], out Transform transform) &&
                        transform != null)
                    {
                        cache.HeadTransform = transform;
                        cache.BoneName = "Bip01_Head";
                    }
                    else if (bodyTransform != null)
                    {
                        transform = FindRecursive(bodyTransform, "Bip01_Head");
                        if (transform != null)
                        {
                            cache.HeadTransform = transform;
                            cache.BoneName = "Bip01_Head";
                        }
                    }
                }
            }

            return cache.HeadTransform;
        }

        public static Transform GetTransform(PlayerEntity player, string name)
        {
            if (player == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            if (name.Equals("Bip01_Head", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveHead(player);
            }
            if (player.hitBox != null && player.hitBox.BonetTransform != null)
            {
                if (!DynamicHashes.TryGetValue(name, out int hash))
                {
                    hash = new IgnoreCaseString(name).GetHashCode();
                    DynamicHashes[name] = hash;
                }
                if (player.hitBox.BonetTransform.TryGetValue(hash, out Transform transform))
                {
                    return transform;
                }
            }
            return null;
        }

        public static bool ShouldSkipPlayer(PlayerEntity player)
        {
            if (player == null)
            {
                return false;
            }

            bool isBoss = false;
            if (player.basicInfo != null && player.basicInfo.CareerInfo != null)
            {
                isBoss = string.Equals(
                    player.basicInfo.CareerInfo.Name,
                    "bossjy6001",
                    StringComparison.Ordinal);
            }
            if (isBoss)
            {
                return false;
            }

            bool hasState = player.HasState(1);
            bool isFrantic = false;
            PlayerInfoData playerInfo = player.GetPlayerInfo();
            if (playerInfo != null)
            {
                isFrantic = playerInfo.InFrantic;
            }
            return hasState || isFrantic;
        }
    }

    internal sealed class BacktrackManager : MonoBehaviour
    {
        private static BacktrackManager _instance;

        public static readonly Dictionary<int, BacktrackTargetHistory> Histories =
            new Dictionary<int, BacktrackTargetHistory>(32);
        public static bool Enabled
        {
            get => Config.BacktrackEnabled;
            set => Config.BacktrackEnabled = value;
        }

        public static int MaxBacktrackMs
        {
            get => Config.BacktrackMaxMs;
            set => Config.BacktrackMaxMs = value;
        }

        public static bool PrioritizeRealBody
        {
            get => Config.BacktrackPrioritizeRealBody;
            set => Config.BacktrackPrioritizeRealBody = value;
        }

        public static bool IgnoreWallShadows
        {
            get => Config.BacktrackIgnoreWallShadows;
            set => Config.BacktrackIgnoreWallShadows = value;
        }

        private static int _invalidFrames;
        private static int _localTeam = -1;
        private static float _deathReferenceTime = -1f;
        private static float _lastCleanupTime;
        private static readonly List<int> RemovalIds = new List<int>(32);

        public static void Initialize()
        {
            if (_instance == null)
            {
                GameObject gameObject = new GameObject("BacktrackManager_Core");
                DontDestroyOnLoad(gameObject);
                _instance = gameObject.AddComponent<BacktrackManager>();
            }
        }

        private void Update()
        {
            BacktrackEntityState.Update();
            if (Enabled)
            {
                if (BacktrackEntityState.Entities != null && BacktrackEntityState.LocalPlayer != null)
                {
                    _invalidFrames = 0;
                    if (!BacktrackEntityState.LocalPlayer.IsDead())
                    {
                        _deathReferenceTime = -1f;
                        if (BacktrackEntityState.LocalPlayer.GetTeam() != 0)
                        {
                            _localTeam = BacktrackEntityState.LocalPlayer.GetTeam();
                        }

                        int count = BacktrackEntityState.Entities.Count;
                        float now = Time.realtimeSinceStartup;
                        if (now - _lastCleanupTime > 0.25f)
                        {
                            _lastCleanupTime = now;
                            RemovalIds.Clear();
                            foreach (KeyValuePair<int, BacktrackTargetHistory> item in Histories)
                            {
                                bool found = false;
                                for (int i = 0; i < count; i++)
                                {
                                    if (BacktrackEntityState.Entities[i] is PlayerEntity player &&
                                        player.GetId() == item.Key)
                                    {
                                        found = true;
                                        break;
                                    }
                                }

                                if (!found)
                                {
                                    item.Value.MissingChecks++;
                                    if (item.Value.MissingChecks > 20)
                                    {
                                        RemovalIds.Add(item.Key);
                                    }
                                }
                                else
                                {
                                    item.Value.MissingChecks = 0;
                                }
                            }

                            for (int i = 0; i < RemovalIds.Count; i++)
                            {
                                Histories.Remove(RemovalIds[i]);
                            }
                        }

                        for (int i = 0; i < count; i++)
                        {
                            if (!(BacktrackEntityState.Entities[i] is PlayerEntity player))
                            {
                                continue;
                            }

                            int entityId = player.GetId();
                            if (!player.IsMySelf() &&
                                !player.IsDead() &&
                                player.GetTeam() != _localTeam)
                            {
                                if (BacktrackBoneUtility.ShouldSkipPlayer(player))
                                {
                                    continue;
                                }

                                if (!Histories.TryGetValue(entityId, out BacktrackTargetHistory history))
                                {
                                    history = new BacktrackTargetHistory(player);
                                    Histories[entityId] = history;
                                }
                                if (history.Player != player)
                                {
                                    history.Player = player;
                                }

                                history.UpdateTransforms();
                                if (!(now - history.LastRecordTime >= 0.05f))
                                {
                                    continue;
                                }

                                Vector3 body = BacktrackCoordinateUtility.ToUnity(
                                    BacktrackCoordinateUtility.GetRawPosition(player));
                                Vector3 head = history.HeadTransform != null
                                    ? history.HeadTransform.position
                                    : Vector3.zero;
                                if (float.IsNaN(head.x) || float.IsInfinity(head.x))
                                {
                                    head = Vector3.zero;
                                }
                                Vector3 spine = history.SpineTransform != null
                                    ? history.SpineTransform.position
                                    : body;
                                if (float.IsNaN(spine.x) || float.IsInfinity(spine.x))
                                {
                                    spine = body;
                                }

                                bool firstRecord = history.Count == 0;
                                BacktrackRecord previous = firstRecord
                                    ? null
                                    : history.Records[history.WriteIndex];
                                if (firstRecord ||
                                    (previous.BodyPosition - body).sqrMagnitude > 0.0025f ||
                                    (previous.SpinePosition - spine).sqrMagnitude > 0.0025f ||
                                    (previous.HeadPosition - head).sqrMagnitude > 0.0025f)
                                {
                                    history.LastRecordTime = now;
                                    history.WriteIndex = (history.WriteIndex + 1) % 128;
                                    if (history.Count < 128)
                                    {
                                        history.Count++;
                                    }

                                    BacktrackRecord record = history.Records[history.WriteIndex];
                                    record.BodyPosition = body;
                                    record.HeadPosition = head;
                                    record.SpinePosition = spine;
                                    record.CaptureTime = now;
                                    record.FrameNumber = Time.frameCount;
                                    record.HasBonePosition = head != Vector3.zero || spine != Vector3.zero;
                                }
                            }
                            else if (Histories.ContainsKey(entityId))
                            {
                                Histories.Remove(entityId);
                            }
                        }
                    }
                    else
                    {
                        if (_deathReferenceTime < 0f)
                        {
                            _deathReferenceTime = Time.realtimeSinceStartup;
                        }
                        BacktrackAimState.Reset();
                    }
                }
                else
                {
                    _invalidFrames++;
                    if (_invalidFrames > 300 && Histories.Count > 0)
                    {
                        ClearRecords();
                    }
                }
            }
            else
            {
                if (Histories.Count > 0)
                {
                    ClearRecords();
                }
                _deathReferenceTime = -1f;
                BacktrackAimState.Reset();
            }
        }

        public static List<BacktrackRecord> GetValidRecords(int entityId)
        {
            if (Histories.TryGetValue(entityId, out BacktrackTargetHistory history))
            {
                int frame = Time.frameCount;
                float referenceTime = _deathReferenceTime <= 0f
                    ? Time.realtimeSinceStartup
                    : _deathReferenceTime;
                if (frame == history.ValidCacheFrame &&
                    MaxBacktrackMs == history.ValidCacheWindow &&
                    Mathf.Abs(referenceTime - history.ValidCacheReferenceTime) < 0.01f)
                {
                    return history.ValidRecords;
                }

                history.ValidRecords.Clear();
                history.ValidCacheFrame = frame;
                history.ValidCacheWindow = MaxBacktrackMs;
                history.ValidCacheReferenceTime = referenceTime;
                int count = history.Count;
                int writeIndex = history.WriteIndex;
                for (int i = 0; i < count; i++)
                {
                    int index = (writeIndex - i + 128) % 128;
                    BacktrackRecord record = history.Records[index];
                    if (record.HasBonePosition)
                    {
                        if ((referenceTime - record.CaptureTime) * 1000f > MaxBacktrackMs)
                        {
                            return history.ValidRecords;
                        }
                        history.ValidRecords.Add(record);
                    }
                }
                return history.ValidRecords;
            }
            return null;
        }

        public static List<BacktrackRecord> GetDisplayRecords(int entityId)
        {
            if (Histories.TryGetValue(entityId, out BacktrackTargetHistory history))
            {
                int frame = Time.frameCount;
                if (frame == history.DisplayCacheFrame)
                {
                    return history.DisplayRecords;
                }

                history.DisplayRecords.Clear();
                history.DisplayCacheFrame = frame;
                List<BacktrackRecord> records = GetValidRecords(entityId);
                if (records == null || records.Count == 0)
                {
                    return history.DisplayRecords;
                }

                int count = records.Count;
                if (count > 16)
                {
                    Mathf.CeilToInt((float)count / 16f);
                }
                for (int i = 0; i < count; i++)
                {
                    history.DisplayRecords.Add(records[i]);
                }
                return history.DisplayRecords;
            }
            return null;
        }

        public static void ClearRecords()
        {
            Histories.Clear();
            _invalidFrames = 0;
            _deathReferenceTime = -1f;
            BacktrackAimState.Reset();
        }

        public static void PrepareCommand(ref UserCmd command)
        {
            if (!Enabled || BacktrackAimState.RecordIndex == -1)
            {
                return;
            }
            try
            {
                if (BacktrackEntityState.LocalPlayer != null && BacktrackEntityState.LocalPlayer.clientTime != null)
                {
                    command.PredicatedOnce = false;
                }
            }
            catch
            {
            }
        }

        public static void SendQueuedPackets(BattleServer server, int fallbackCount)
        {
            try
            {
                if (HookManager.chokedPackets == null ||
                    HookManager.chokedPackets.Count == 0 ||
                    server == null ||
                    server.UdpSocket == null)
                {
                    return;
                }

                int count = Mathf.Clamp(
                    BacktrackAimState.RecordIndex != -1
                        ? BacktrackAimState.RecordIndex
                        : fallbackCount,
                    1,
                    HookManager.chokedPackets.Count);
                for (int i = 0; i < count; i++)
                {
                    var packet = HookManager.chokedPackets[i];
                    if (packet != null && packet.FinalData != null)
                    {
                        server.UdpSocket.Send(packet.FinalData, packet.FinalLength);
                    }
                }
                if (count >= HookManager.chokedPackets.Count)
                {
                    HookManager.chokedPackets.Clear();
                }
                else
                {
                    HookManager.chokedPackets.RemoveRange(0, count);
                }
            }
            catch
            {
            }
        }
    }
}
