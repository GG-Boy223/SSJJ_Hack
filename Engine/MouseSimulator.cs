using Assets.Scripts.Input;
using SkyDome.Cfg;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDome.Engine
{
    public class MouseSimulator : IDeviceInput
    {
        public enum InputState
        {
            None = 0,
            TrueKeep = 1,
            TrueOnce = 2,
            FalseKeep = 3,
            FalseOnce = 4
        }

        public static event Action PreInputCallback;
        public static Vector2 ForceAxisDelta = Vector2.zero;
        public static Vector2 ForceAxisPersistent = Vector2.zero;

        private static readonly Dictionary<KeyCode, InputState> _forcedKeys =
            new Dictionary<KeyCode, InputState>();

        private static readonly Dictionary<int, InputState> _forcedMouseButtons =
            new Dictionary<int, InputState>();

        public bool AnyKey()
        {
            try
            {
                PreInputCallback?.Invoke();
            }
            catch (Exception ex)
            {
                #if Debug_Log
                global::System.Console.WriteLine($"PreInputCallback 错误: {ex}");
                #endif
            }

            return HasActiveKeyInput() ||
                   HasActiveMouseInput() ||
                   Input.anyKey;
        }


        public bool AnyKeyDown()
        {
            return HasKeyDownInput() || Input.anyKeyDown;
        }

        public float GetAxis(string axis)
        {
            float baseValue = Input.GetAxis(axis);

            if (axis == "Mouse X")
            {
                return baseValue + ConsumeAxisDelta(ref ForceAxisDelta.x);
            }

            if (axis == "Mouse Y")
            {
                return baseValue + ConsumeAxisDelta(ref ForceAxisDelta.y);
            }

            return baseValue;
        }

        public bool GetKey(KeyCode keyCode)
        {
            if (TryGetKeyState(keyCode, out InputState state))
            {
                return ProcessKeyState(keyCode, state);
            }

            return Input.GetKey(keyCode);
        }

        public bool GetKeyDown(KeyCode keyCode)
        {
            if (TryGetKeyState(keyCode, out InputState state))
            {
                return state == InputState.TrueOnce;
            }

            return Input.GetKeyDown(keyCode);
        }

        public bool GetMouseButton(int button)
        {
            if (TryGetMouseState(button, out InputState state))
            {
                return ProcessMouseState(button, state);
            }

            return !Config.AntiMouse1 || Contexts.sharedInstance.player.myPlayerEntity.currentWeapon.Weapon >= 3 || Contexts.sharedInstance.weapon.currentWeaponEntity.basicInfo.Info.WeaponType == 5 || button != 1 ? Input.GetMouseButton(button) : button == 0;
        }

        public bool GetMouseButtonDown(int button)
        {
            return Input.GetMouseButtonDown(button);
        }

        public bool GetMouseButtonUp(int button)
        {
            return Input.GetMouseButtonUp(button);
        }

        public static void ForceMouseButton(int mouseButton, InputState state)
        {
            _forcedMouseButtons[mouseButton] = state;
        }

        public static void ForceKey(KeyCode keyCode, InputState state)
        {
            _forcedKeys[keyCode] = state;
        }

        private static float ConsumeAxisDelta(ref float axisValue)
        {
            float value = axisValue;
            axisValue = 0f;
            return value;
        }

        private bool HasActiveKeyInput()
        {
            foreach (var state in _forcedKeys.Values)
            {
                if (state != InputState.None && state != InputState.FalseKeep)
                    return true;
            }
            return false;
        }

        private bool HasActiveMouseInput()
        {
            foreach (var state in _forcedMouseButtons.Values)
            {
                if (state != InputState.None && state != InputState.FalseKeep)
                    return true;
            }
            return false;
        }

        private bool HasKeyDownInput()
        {
            foreach (var state in _forcedKeys.Values)
            {
                if (state == InputState.TrueOnce)
                    return true;
            }
            return false;
        }

        private bool TryGetKeyState(KeyCode keyCode, out InputState state)
        {
            return _forcedKeys.TryGetValue(keyCode, out state) && state != InputState.None;
        }

        private bool TryGetMouseState(int button, out InputState state)
        {
            return _forcedMouseButtons.TryGetValue(button, out state) && state != InputState.None;
        }

        private bool ProcessKeyState(KeyCode keyCode, InputState state)
        {
            switch (state)
            {
                case InputState.TrueKeep:
                    return true;

                case InputState.TrueOnce:
                    _forcedKeys[keyCode] = InputState.None;
                    return true;

                case InputState.FalseKeep:
                    return false;

                case InputState.FalseOnce:
                    _forcedKeys[keyCode] = InputState.None;
                    return false;

                default:
                    return Input.GetKey(keyCode);
            }
        }

        private bool ProcessMouseState(int button, InputState state)
        {
            switch (state)
            {
                case InputState.TrueKeep:
                    return true;

                case InputState.TrueOnce:
                    _forcedMouseButtons[button] = InputState.None;
                    return true;

                case InputState.FalseKeep:
                    return false;

                case InputState.FalseOnce:
                    _forcedMouseButtons[button] = InputState.None;
                    return false;

                default:
                    return Input.GetMouseButton(button);
            }
        }
    }
}