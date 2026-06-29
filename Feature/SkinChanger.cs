using SkyDome.Entity;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using NetData;

namespace SkyDome.Feature
{
    public class SkinChanger : MonoBehaviour
    {
        public static List<string> WeaponNames = new List<string>();
        public static List<string> CharacterNames = new List<string>();
        public static List<string> BackAccessoryNames = new List<string>();

        private static bool _isInitialized = false;

        // 初始化
        public static void Initialize()
        {
            if (_isInitialized) return;
            try
            {
                Type careerType = FindType("share.constant.PlayerCareerConstant");
                if (careerType != null) LoadConstantsFromType(careerType, CharacterNames);

                Type weaponType1 = FindType("Assets.Sources.Constant.Weapon.FreeWeaponConstant");
                Type weaponType2 = FindType("Assets.Sources.Constant.Weapon.WeaponConstant");
                if (weaponType1 != null) LoadConstantsFromType(weaponType1, WeaponNames);
                if (weaponType2 != null) LoadConstantsFromType(weaponType2, WeaponNames);

                Type accessoryType = FindType("share.constant.BackAccessoryConstant");
                if (accessoryType != null) LoadConstantsFromType(accessoryType, BackAccessoryNames);
                else BackAccessoryNames.AddRange(new List<string> { "jetpack", "wing_zhandouopen", "chibang1open" });

                _isInitialized = true;
            }
            catch { }
        }

        // 查找类型
        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }

        // 从类型加载常量字符串
        private static void LoadConstantsFromType(Type type, List<string> targetList)
        {
            if (type == null) return;
            FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == typeof(string))
                {
                    object val = field.GetValue(null);
                    if (val != null) targetList.Add(val.ToString());
                }
            }
        }

        // 武器
        public static void ChangeWeapon(string name)
        {
            if (PlayerUpdate.LocalEntity == null) return;
            PlayerUpdate.LocalEntity._entity.basicInfo.Current.CurrentWeaponName = name;
        }

        // 角色
        public static void ChangeCharacter(string name)
        {
            if (PlayerUpdate.LocalEntity == null) return;
            var data = PlayerUpdate.LocalEntity._entity.basicInfo.Current;
            data.Career = name;
            data.CurrentHandName = name;
        }

        // 背饰
        public static void ChangeBackAccessory(string name)
        {
            if (PlayerUpdate.LocalEntity == null) return;
            PlayerUpdate.LocalEntity._entity.basicInfo.Current.BackAccessory = name;
        }

        // 队伍
        public static void ChangeTeam(int teamId)
        {
            if (PlayerUpdate.LocalEntity == null) return;
            PlayerUpdate.LocalEntity._entity.basicInfo.Current.Team = teamId;
        }

        // 大小
        public static void ChangeScale(float scale)
        {
            if (PlayerUpdate.LocalEntity == null) return;
            PlayerUpdate.LocalEntity._entity.basicInfo.Current.Scale = scale;
        }

        // 头部大小
        public static void ChangeHeadEnlarge(float scale)
        {
            if (PlayerUpdate.LocalEntity == null) return;
            PlayerUpdate.LocalEntity._entity.basicInfo.Current.HeadEnlarge = scale;
        }

        // 透明度
        public static void ChangeAlpha(int alpha)
        {
            if (PlayerUpdate.LocalEntity == null) return;
            PlayerUpdate.LocalEntity._entity.basicInfo.Current.Alpha = alpha;
        }

        // 自身透明度
        public static void ChangeSelfAlpha(int alpha)
        {
            if (PlayerUpdate.LocalEntity == null) return;
            PlayerUpdate.LocalEntity._entity.basicInfo.Current.SelfAlpha = alpha;
        }
    }
}
