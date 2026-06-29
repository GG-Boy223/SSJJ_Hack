using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace SkyDome
{
    public class ConsoleManager : MonoBehaviour
    {
        private static readonly object SyncRoot = new object();
        private static ConsoleManager _instance;
        private static StreamWriter _consoleWriter;
        private static bool _consoleAllocated;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleTitle(string lpConsoleTitle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCP(uint wCodePageID);

        private const int STD_OUTPUT_HANDLE = -11;
        private const int SW_SHOW = 5;
        private const int SW_HIDE = 0;
        private const uint CP_UTF8 = 65001;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            EnsureConsole();
        }

        public static void EnsureConsole()
        {
#if Debug_Log
            lock (SyncRoot)
            {
                if (_consoleAllocated) return;
                if (!AllocConsole()) return;

                SetConsoleOutputCP(CP_UTF8);
                SetConsoleCP(CP_UTF8);
                global::System.Console.OutputEncoding = new UTF8Encoding(false);
                global::System.Console.InputEncoding = new UTF8Encoding(false);
                SetConsoleTitle("SkyDome Log");

                IntPtr stdHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (stdHandle == IntPtr.Zero || stdHandle == new IntPtr(-1))
                {
                    FreeConsole();
                    return;
                }

                var safeFileHandle = new SafeFileHandle(stdHandle, ownsHandle: false);
                var fileStream = new FileStream(safeFileHandle, FileAccess.Write);
                _consoleWriter = new StreamWriter(fileStream, new UTF8Encoding(false))
                {
                    AutoFlush = true
                };

                global::System.Console.SetOut(_consoleWriter);
                global::System.Console.SetError(_consoleWriter);
                _consoleAllocated = true;

                global::System.Console.WriteLine($"启动时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
#endif
        }

        public static void ShowConsole()
        {
#if Debug_Log
            IntPtr consoleWindow = GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero)
            {
                ShowWindow(consoleWindow, SW_SHOW);
            }
#endif
        }

        public static void HideConsole()
        {
#if Debug_Log
            IntPtr consoleWindow = GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero)
            {
                ShowWindow(consoleWindow, SW_HIDE);
            }
#endif
        }

        public static void ClearConsole()
        {
#if Debug_Log
            if (!_consoleAllocated) return;

            try
            {
                global::System.Console.Clear();
            }
            catch { }
#endif
        }

        private void OnDestroy()
        {
            if (_instance != this) return;

            lock (SyncRoot)
            {
                if (_consoleWriter != null)
                {
                    _consoleWriter.Flush();
                    _consoleWriter.Dispose();
                    _consoleWriter = null;
                }

                if (_consoleAllocated)
                {
                    FreeConsole();
                    _consoleAllocated = false;
                }
            }

            _instance = null;
        }

        private void OnApplicationQuit()
        {
            OnDestroy();
        }
    }
}
