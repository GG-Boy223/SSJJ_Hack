using System.Threading;
using System.Windows.Forms;
using UnityEngine;
using Assets.Scripts.QuickRuntimeConsole;

namespace SkyDome.RuntimeConsole
{
    public class QuickRuntimeConsoleGUI : MonoBehaviour
    {
        private static ExternalConsoleWindow _consoleWindow;
        private static Thread _uiThread;
        private bool _wasOpen = false;

        private void Start()
        {
            if (_consoleWindow == null)
            {
                _uiThread = new Thread(() =>
                {
                    System.Windows.Forms.Application.EnableVisualStyles();
                    System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                    _consoleWindow = new ExternalConsoleWindow();
                    System.Windows.Forms.Application.Run(_consoleWindow);
                });
                _uiThread.SetApartmentState(ApartmentState.STA);
                _uiThread.IsBackground = true;
                _uiThread.Start();
            }
        }

        private void Update()
        {
            if (ConsoleStatus.ConsoleOpened && !_wasOpen)
            {
                ShowConsole();
                _wasOpen = true;
            }
            else if (!ConsoleStatus.ConsoleOpened && _wasOpen)
            {
                _wasOpen = false;
            }
        }

        private void ShowConsole()
        {
            if (_consoleWindow != null && _consoleWindow.IsHandleCreated)
            {
                _consoleWindow.Invoke(new System.Action(() =>
                {
                    _consoleWindow.ShowConsole();
                }));
            }
        }

        private void OnApplicationQuit()
        {
            if (_consoleWindow != null && !_consoleWindow.IsDisposed)
            {
                _consoleWindow.Invoke(new System.Action(() =>
                {
                    _consoleWindow.Close();
                }));
            }
        }
    }
}
