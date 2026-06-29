using SkyDome.Entity;
using SSJJBase.String;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Utilities
{
    public static class PlayerUtility
    {
        public static Dictionary<IgnoreCaseString, Transform> GetPlayerAllTransform(this PlayerInfo player)
        {
            return player.ThirdPersonUnityObjects?.AllPlayerTransforms;
        }
        public static Transform GetPlayerTransform(this PlayerInfo player, string name)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Transform name cannot be null or empty", nameof(name));

            var transforms = player.ThirdPersonUnityObjects?.AllPlayerTransforms;
            if (transforms == null || transforms.Count == 0)
                return null;

            IgnoreCaseString targetBone = name;

            foreach (var kvp in transforms)
            {
                if (kvp.Key.Equals(targetBone))
                {
                    return kvp.Value;
                }
            }

            return null;
        }
        public static Transform GetPlayerTransform(this PlayerEntity player, string name)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Transform name cannot be null or empty", nameof(name));

            var transforms = player.thirdPersonUnityObjects?.AllPlayerTransforms;
            if (transforms == null || transforms.Count == 0)
                return null;

            IgnoreCaseString targetBone = name;

            foreach (var kvp in transforms)
            {
                if (kvp.Key.Equals(targetBone))
                {
                    return kvp.Value;
                }
            }

            return null;
        }
        public static Transform GetValidHeadNub(this PlayerInfo player)
        {
            Transform headNub = player.GetPlayerTransform("Bip01_HeadNub");
            if (headNub != null)
                return headNub;

            Transform head = player.GetPlayerTransform("Bip01_Head");
            if (head == null)
                return null; 

            if (head.childCount == 0)
            {
                var fakeHeadNub = new GameObject("fake_HeadNub")
                {
                    transform =
            {
                parent = head,
                localPosition = new Vector3(-21.7f, 0f, 0f)
            }
                };
                return fakeHeadNub.transform;
            }
            return head.GetChild(0);
        }
        public static float Length(this Vector3 vector)
        {
            return Mathf.Sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
        }


    }
}
