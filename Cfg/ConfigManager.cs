using System;
using System.IO;
using System.Reflection;
using UnityEngine;


namespace SkyDome.Cfg
{
    public static class ConfigManager
    {
        private static readonly string ConfigDirectory = Path.Combine(Application.persistentDataPath, "SkyConfigs");
        private static readonly FieldInfo[] Fields = typeof(Config).GetFields(BindingFlags.Public | BindingFlags.Static);

        // 获取配置文件路径
        private static string GetConfigPath(string configName)
        {
            return Path.Combine(ConfigDirectory, configName);
        }

        // 保存配置
        public static void SaveConfig(string configName)
        {
            try
            {
                // 确保目录存在
                if (!Directory.Exists(ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigDirectory);
                }

                string configPath = GetConfigPath(configName);

                using (StreamWriter writer = new StreamWriter(configPath, false))
                {
                    foreach (var field in Fields)
                    {
                        object value = field.GetValue(null);
                        string line = $"{field.Name}={SerializeValue(value)}";
                        writer.WriteLine(line);
                    }
                }
                #if Debug_Log
                global::System.Console.WriteLine($"[配置] 已保存到: {configPath}");
                #endif
            }
            catch (Exception ex)
            {
                #if Debug_Log
                global::System.Console.WriteLine($"[配置] 保存失败: {ex.Message}");
                #endif
            }
        }

        // 加载配置
        public static void LoadConfig(string configName)
        {
            try
            {
                string configPath = GetConfigPath(configName);

                if (!File.Exists(configPath))
                {
                    #if Debug_Log
                    global::System.Console.WriteLine($"[配置] 配置文件 {configName} 不存在");
                    #endif
                    return;
                }

                using (StreamReader reader = new StreamReader(configPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        string[] parts = line.Split('=');
                        if (parts.Length != 2) continue;

                        string fieldName = parts[0].Trim();
                        string valueStr = parts[1].Trim();

                        FieldInfo field = Array.Find(Fields, f => f.Name == fieldName);
                        if (field == null) continue;

                        try
                        {
                            object value = DeserializeValue(valueStr, field.FieldType);
                            field.SetValue(null, value);
                        }
                        catch (Exception ex)
                        {
                            #if Debug_Log
                            global::System.Console.WriteLine($"[配置] 字段 {fieldName} 解析失败: {ex.Message}");
                            #endif
                        }
                    }
                }

                if (Config.BacktrackMaxMs <= 0)
                {
                    Config.BacktrackMaxMs = 200;
                }
                #if Debug_Log
                global::System.Console.WriteLine($"[配置] 已加载配置: {configName}");
                #endif
            }
            catch (Exception ex)
            {
                #if Debug_Log
                global::System.Console.WriteLine($"[配置] 加载失败: {ex.Message}");
                #endif
            }
        }

        // 删除配置
        public static void DeleteConfig(string configName)
        {
            try
            {
                string configPath = GetConfigPath(configName);
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                    #if Debug_Log
                    global::System.Console.WriteLine($"[配置] 已删除配置: {configName}");
                    #endif
                }
            }
            catch (Exception ex)
            {
                #if Debug_Log
                global::System.Console.WriteLine($"[配置] 删除失败: {ex.Message}");
                #endif
            }
        }

        // 获取所有配置名称
        public static string[] GetAllConfigNames()
        {
            try
            {
                if (!Directory.Exists(ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigDirectory);
                    return new string[0];
                }

                string[] files = Directory.GetFiles(ConfigDirectory);
                string[] names = new string[files.Length];

                for (int i = 0; i < files.Length; i++)
                {
                    names[i] = Path.GetFileName(files[i]);
                }

                return names;
            }
            catch (Exception ex)
            {
                #if Debug_Log
                global::System.Console.WriteLine($"[配置] 获取配置列表失败: {ex.Message}");
                #endif
                return new string[0];
            }
        }

        // 序列化值
        private static string SerializeValue(object value)
        {
            if (value == null) return "null";

            Type type = value.GetType();
            if (type == typeof(bool) || type == typeof(int) || type == typeof(float) || type == typeof(string))
                return value.ToString();

            if (type == typeof(KeyCode))
                return ((int)(KeyCode)value).ToString();

            return value.ToString();
        }

        // 反序列化值
        private static object DeserializeValue(string valueStr, Type targetType)
        {
            if (valueStr == "null") return null;

            if (targetType == typeof(bool))
                return bool.Parse(valueStr);

            if (targetType == typeof(int))
                return int.Parse(valueStr);

            if (targetType == typeof(float))
                return float.Parse(valueStr);

            if (targetType == typeof(string))
                return valueStr;

            if (targetType == typeof(KeyCode))
                return (KeyCode)int.Parse(valueStr);

            return null;
        }
    }
}
