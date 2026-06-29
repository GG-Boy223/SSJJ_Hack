using System.Reflection;
using System;

namespace SkyDome.Extension
{
    public static class ReflectionExtensions
    {
        private const BindingFlags DefaultBindingFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static T GetFieldValue<T>(this object source, string fieldName)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("Field name cannot be null or whitespace.", nameof(fieldName));

            Type type = source.GetType();
            FieldInfo field = type.GetField(fieldName, DefaultBindingFlags);

            return field is null
                ? throw new ArgumentException($"Field '{fieldName}' not found in type {type.Name}.")
                : (T)field.GetValue(source);
        }

        public static void InvokeMethod(this object target, string methodName, params object[] parameters)
        {
            if (target is null)
                throw new ArgumentNullException(nameof(target));

            MethodInfo method = target.GetType().GetMethod(methodName, DefaultBindingFlags) ?? throw new ArgumentException($"Method '{methodName}' not found in type {target.GetType().Name}.");
            method.Invoke(target, parameters);
        }

        public static T InvokeMethod<T>(this object target, string methodName, params object[] parameters)
        {
            if (target is null)
                throw new ArgumentNullException(nameof(target));

            MethodInfo method = target.GetType().GetMethod(methodName, DefaultBindingFlags);
            return method is null
                ? throw new ArgumentException($"Method '{methodName}' not found in type {target.GetType().Name}.")
                : (T)method.Invoke(target, parameters);
        }

        public static bool TryInvokeMethod(this object target, string methodName, params object[] parameters)
        {
            try
            {
                InvokeMethod(target, methodName, parameters);
                return true;
            }
            catch (Exception ex)
            {
                #if Debug_Log
                global::System.Console.WriteLine($"调用方法 '{methodName}' 失败: {ex.Message}");
                #endif
                return false;
            }
        }

        public static bool TryInvokeMethod<T>(this object target, string methodName, out T result, params object[] parameters)
        {
            result = default;

            try
            {
                result = InvokeMethod<T>(target, methodName, parameters);
                return true;
            }
            catch (Exception ex)
            {
                #if Debug_Log
                global::System.Console.WriteLine($"调用方法 '{methodName}' 失败: {ex.Message}");
                #endif
                return false;
            }
        }
    }
}