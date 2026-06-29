using UnityEngine;

namespace SkyDome
{
    public class Loader : MonoBehaviour
    {
        private const string HookObjectName = "SkyDome_HookObject";
        private static GameObject _hookObject;

        public static void Load()
        {
#if Debug_Log
            ConsoleManager.EnsureConsole();
#endif

            if (_hookObject != null)
            {
                EnsureMainComponent(_hookObject);
                return;
            }

            GameObject hookObject = null;
            bool createdObject = false;

            try
            {
                hookObject = GameObject.Find(HookObjectName);
                if (hookObject == null)
                {
                    hookObject = new GameObject(HookObjectName);
                    createdObject = true;
                }

                EnsureMainComponent(hookObject);
                DontDestroyOnLoad(hookObject);
                _hookObject = hookObject;

#if Debug_Log
                global::System.Console.WriteLine($"[加载器] SkyDome 初始化成功，ID: {_hookObject.GetInstanceID()}");
#endif
            }
            catch (System.Exception ex)
            {
                if (createdObject && hookObject != null)
                {
                    DestroyImmediate(hookObject);
                }

                _hookObject = null;
#if Debug_Log
                global::System.Console.WriteLine($"[加载器] 初始化失败: {ex}");
#endif
                throw;
            }
        }

        public static void Unload()
        {
            GameObject hookObject = _hookObject ?? GameObject.Find(HookObjectName);
            if (hookObject == null) return;

            DestroyImmediate(hookObject);
            _hookObject = null;
#if Debug_Log
            global::System.Console.WriteLine("[加载器] SkyDome 资源已卸载");
#endif
        }

        private static void EnsureMainComponent(GameObject hookObject)
        {
            if (hookObject == null)
            {
                throw new MissingReferenceException("Hook object is null");
            }

            if (hookObject.GetComponent<Main>() == null)
            {
                hookObject.AddComponent<Main>();
            }
        }
    }
}

namespace t
{
    public class u : MonoBehaviour
    {
        public static void i()
        {
            SkyDome.Loader.Load();
        }

        public static void Unload()
        {
            SkyDome.Loader.Unload();
        }
    }
}
