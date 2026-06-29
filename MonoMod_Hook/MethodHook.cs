
using DotNetDetour;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MonoHook
{
    /// <summary>
    /// Hook 类，用来 Hook 某个 C# 方法
    /// </summary>
    public unsafe class MethodHook
    {
        public string tag;
        public bool isHooked { get; private set; }
        public bool isPlayModeHook { get; private set; }

        public MethodBase targetMethod { get; private set; }       // 需要被hook的目标方法
        public MethodBase replacementMethod { get; private set; }  // 被hook后的替代方法
        public MethodBase proxyMethod { get; private set; }        // 目标方法的代理方法(可以通过此方法调用被hook后的原方法)

        private IntPtr _targetPtr;          // 目标方法被 jit 后的地址指针
        private IntPtr _replacementPtr;
        private IntPtr _proxyPtr;

        private CodePatcher _codePatcher;



        /// <summary>
        /// 创建一个 Hook
        /// </summary>
        /// <param name="targetMethod">需要替换的目标方法</param>
        /// <param name="replacementMethod">准备好的替换方法</param>
        /// <param name="proxyMethod">如果还需要调用原始目标方法，可以通过此参数的方法调用，如果不需要可以填 null</param>
        public MethodHook(MethodBase targetMethod, MethodBase replacementMethod, MethodBase proxyMethod, string data = "")
        {
            this.targetMethod = targetMethod;
            this.replacementMethod = replacementMethod;
            this.proxyMethod = proxyMethod;
            this.tag = data;

            CheckMethod();
        }

        public void Install()
        {

            if (isHooked)
                return;

            DoInstall();

            isPlayModeHook = Application.isPlaying;
        }

        public void Uninstall()
        {
            if (!isHooked)
                return;

            _codePatcher.RemovePatch();

            isHooked = false;
            HookPool.RemoveHooker(targetMethod);
        }

        #region private
        private void DoInstall()
        {
            if (targetMethod == null || replacementMethod == null)
                return;

            HookPool.AddHook(targetMethod, this);

            if (_codePatcher == null)
            {
                if (GetFunctionAddr())
                {
#if Debug_Log
                    global::System.Console.WriteLine($"Original [{targetMethod.DeclaringType.Name}.{targetMethod.Name}]: {HookUtils.HexToString(_targetPtr.ToPointer(), 64, -16)}");
                    global::System.Console.WriteLine($"Original [{replacementMethod.DeclaringType.Name}.{replacementMethod.Name}]: {HookUtils.HexToString(_replacementPtr.ToPointer(), 64, -16)}");

                    if (proxyMethod != null)
                    {
                        global::System.Console.WriteLine($"Original [{proxyMethod.DeclaringType.Name}.{proxyMethod.Name}]: {HookUtils.HexToString(_proxyPtr.ToPointer(), 64, -16)}");
                    }
#endif

                    CreateCodePatcher();
                    _codePatcher.ApplyPatch();

#if Debug_Log
                    global::System.Console.WriteLine($"New [{targetMethod.DeclaringType.Name}.{targetMethod.Name}]: {HookUtils.HexToString(_targetPtr.ToPointer(), 64, -16)}");
                    global::System.Console.WriteLine($"New [{replacementMethod.DeclaringType.Name}.{replacementMethod.Name}]: {HookUtils.HexToString(_replacementPtr.ToPointer(), 64, -16)}");

                    if (proxyMethod != null)
                    {
                        global::System.Console.WriteLine($"New [{proxyMethod.DeclaringType.Name}.{proxyMethod.Name}]: {HookUtils.HexToString(_proxyPtr.ToPointer(), 64, -16)}");
                    }
#endif
                }
            }

            isHooked = true;
        }

        private void CheckMethod()
        {
            if (targetMethod == null || replacementMethod == null)
#if Debug_Log
                throw new Exception("MethodHook:targetMethod and replacementMethod and proxyMethod can not be null");

#else
                return;
#endif
            if (targetMethod.IsAbstract)
            {
#if Debug_Log
                string methodName = $"{targetMethod.DeclaringType.Name}.{targetMethod.Name}";
                throw new Exception($"WRANING: you can not hook abstract method [{methodName}]");
#else
                return;
#endif
            }

        }

        private void CreateCodePatcher()
        {
            long addrOffset = Math.Abs(_targetPtr.ToInt64() - _proxyPtr.ToInt64());

            if (_proxyPtr != IntPtr.Zero)
                addrOffset = Math.Max(addrOffset, Math.Abs(_targetPtr.ToInt64() - _proxyPtr.ToInt64()));

            if (LDasm.IsARM())
            {
                if (IntPtr.Size == 8)
                    _codePatcher = new CodePatcher_arm64_near(_targetPtr, _replacementPtr, _proxyPtr);
                else if (addrOffset < ((1 << 25) - 1))
                    _codePatcher = new CodePatcher_arm32_near(_targetPtr, _replacementPtr, _proxyPtr);
                else if (addrOffset < ((1 << 27) - 1))
                    _codePatcher = new CodePatcher_arm32_far(_targetPtr, _replacementPtr, _proxyPtr);
                else
                {
#if Debug_Log
                    throw new Exception("address of target method and replacement method are too far, can not hook");
#else
                    return;
#endif
                }
            }
            else
            {
                if (IntPtr.Size == 8)
                {
                    if (addrOffset < 0x7fffffff) // 2G
                        _codePatcher = new CodePatcher_x64_near(_targetPtr, _replacementPtr, _proxyPtr);
                    else
                        _codePatcher = new CodePatcher_x64_far(_targetPtr, _replacementPtr, _proxyPtr);
                }
                else
                    _codePatcher = new CodePatcher_x86(_targetPtr, _replacementPtr, _proxyPtr);
            }
        }

        /// <summary>
        /// 获取对应函数jit后的native code的地址
        /// </summary>
        private bool GetFunctionAddr()
        {
            _targetPtr = GetFunctionAddr(targetMethod);
            _replacementPtr = GetFunctionAddr(replacementMethod);
            _proxyPtr = GetFunctionAddr(proxyMethod);

            if (_targetPtr == IntPtr.Zero || _replacementPtr == IntPtr.Zero)
                return false;

            if (proxyMethod != null && _proxyPtr == null)
                return false;

            if (_replacementPtr == _targetPtr)
            {
#if Debug_Log
                throw new Exception($"the addresses of target method {targetMethod.Name} and replacement method {replacementMethod.Name} can not be same");
#else
                return false;
#endif
            }

            if (LDasm.IsThumb(_targetPtr) || LDasm.IsThumb(_replacementPtr))
            {
#if Debug_Log
                throw new Exception("does not support thumb arch");
#else
                return false;
#endif
            }

            return true;
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)] // 好像在 IL2CPP 里无效
        private struct __ForCopy
        {
            public long __dummy;
            public MethodBase method;
        }
        /// <summary>
        /// 获取方法指令地址
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        private IntPtr GetFunctionAddr(MethodBase method)
        {
            if (method == null)
                return IntPtr.Zero;

            if (!LDasm.IsIL2CPP())
                return method.MethodHandle.GetFunctionPointer();
            else
            {
                /*
                    // System.Reflection.MonoMethod
                    typedef struct Il2CppReflectionMethod
                    {
                        Il2CppObject object;
                        const MethodInfo *method;
                        Il2CppString *name;
                        Il2CppReflectionType *reftype;
                    } Il2CppReflectionMethod;

                    typedef Il2CppClass Il2CppVTable;
                    typedef struct Il2CppObject
                    {
                        union
                        {
                            Il2CppClass *klass;
                            Il2CppVTable *vtable;
                        };
                        MonitorData *monitor;
                    } Il2CppObject;

                typedef struct MethodInfo
                {
                    Il2CppMethodPointer methodPointer; // this is the pointer to native code of method
                    InvokerMethod invoker_method;
                    const char* name;
                    Il2CppClass *klass;
                    const Il2CppType *return_type;
                    const ParameterInfo* parameters;
                // ...
                }
                 */

                __ForCopy __forCopy = new __ForCopy() { method = method };

                long* ptr = &__forCopy.__dummy;
                ptr++; // addr of _forCopy.method

                IntPtr methodAddr = IntPtr.Zero;
                if (sizeof(IntPtr) == 8)
                {
                    long methodDataAddr = *(long*)ptr;
                    byte* ptrData = (byte*)methodDataAddr + sizeof(IntPtr) * 2; // offset of Il2CppReflectionMethod::const MethodInfo *method;

                    long methodPtr = 0;
                    methodPtr = *(long*)ptrData;
                    methodAddr = new IntPtr(*(long*)methodPtr); // MethodInfo::Il2CppMethodPointer methodPointer;
                }
                else
                {
                    int methodDataAddr = *(int*)ptr;
                    byte* ptrData = (byte*)methodDataAddr + sizeof(IntPtr) * 2; // offset of Il2CppReflectionMethod::const MethodInfo *method;

                    int methodPtr = 0;
                    methodPtr = *(int*)ptrData;
                    methodAddr = new IntPtr(*(int*)methodPtr);
                }
                return methodAddr;
            }
        }

    }

#endregion
}
