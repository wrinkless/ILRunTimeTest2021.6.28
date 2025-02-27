﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ILRuntime.CLR.Method;
using ILRuntime.CLR.TypeSystem;
using UnityEngine;
using ILRuntime.Runtime.Enviorment;
using ILRuntime.Runtime.Intepreter;
using AppDomain = ILRuntime.Runtime.Enviorment.AppDomain;

public class ILRuntimeWrapper : MonoSingleton<ILRuntimeWrapper>
{
    public  Action<string,object> TestActionDelegate;

    public AppDomain appDomain;

    private string bindClass;
    private IType classType;
    private ILTypeInstance instance;
    private IMethod updateMethod,fixedUpdateMethod,lateUpdateMethod, awakeMethod, startMethod, onDestroyMethod;
    
    private System.IO.MemoryStream m_fs, m_p;
    private bool m_isGameStart,m_startUpdate;

    public bool IsGameStart
    {
        get => m_isGameStart;
        set => m_isGameStart = value;
    }

    public override void Awake()
    {
        base.Awake();
        IsGameStart = false;
        m_startUpdate = false;
        appDomain = new ILRuntime.Runtime.Enviorment.AppDomain();
    }

    private void FixedUpdate()
    {
        appDomain.Invoke(fixedUpdateMethod, instance);
    }

    private void Update()
    {
        if (IsGameStart)
        {
            appDomain.Invoke(updateMethod, instance);
        }
    }

    private void LateUpdate()
    {
        if (IsGameStart)
        {
            appDomain.Invoke(lateUpdateMethod, instance);
        }
    }

    private void OnDestroy()
    {
        if (IsGameStart)
        {
            appDomain.Invoke(onDestroyMethod, instance);
        }
    }

    /// <summary>
    /// 加载dll，pdb
    /// </summary>
    /// <param name="dll"></param>
    /// <param name="pdb"></param>
    public void LoadHotFixAssembly(byte[] dll, byte[] pdb)
    {
        m_fs = new MemoryStream(dll);
        //m_p = new MemoryStream(pdb);
        try
        {
            appDomain.LoadAssembly(m_fs, null, new ILRuntime.Mono.Cecil.Pdb.PdbReaderProvider());
        }
        catch
        {
            Debug.LogError("加载热更DLL失败，请确保已经通过VS打开Assets/Samples/ILRuntime/1.6/Demo/HotFix_Project/HotFix_Project.sln编译过热更DLL");
            return;
        }
        appDomain.DebugService.StartDebugService(56000);
        InitializeILRuntime();
    }
    
   private void InitializeILRuntime()
    {
#if DEBUG && (UNITY_EDITOR || UNITY_ANDROID || UNITY_IPHONE)
        //由于Unity的Profiler接口只允许在主线程使用，为了避免出异常，需要告诉ILRuntime主线程的线程ID才能正确将函数运行耗时报告给Profiler
        appDomain.UnityMainThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif
        //这里做一些ILRuntime的注册，HelloWorld示例暂时没有需要注册的
        //Action<string> 的参数为一个string
        Debug.Log("主工程里注册委托");
        appDomain.DelegateManager.RegisterMethodDelegate<string,object>();
        
        //unityAction的委托转换器
        appDomain.DelegateManager.RegisterDelegateConvertor<UnityEngine.Events.UnityAction>((act) =>
        {
            return new  UnityEngine.Events.UnityAction(() =>
            {
                ((Action)act).Invoke();
            });
        });
    }
    /// <summary>
    /// 进入游戏
    /// </summary>
   public void EnterGame()
   {
       //HelloWorld，第一次方法调用
       //appDomain.Invoke("HotFix_Project.InstanceClass", "StaticFunTest", null, null);
       appDomain.DelegateManager.RegisterMethodDelegate<string>();
       IsGameStart = true;
       //开始调用热更工程
       InitHotFixMethod();
       
       //开始执行热更工程
       //appDomain.Invoke("HotFix_Project.MainBehaviour","Awake",null,null);
   }
    public void InitHotFixMethod()
    {
        bindClass = "HotFix_Project.MainBehaviour";
        if (IsGameStart)
        {
            classType = appDomain.LoadedTypes[bindClass];
            instance = (classType as ILType).Instantiate();

            awakeMethod = classType.GetMethod("Awake", 0);
            startMethod = classType.GetMethod("Start", 0);
            updateMethod = classType.GetMethod("Update", 0);
            onDestroyMethod = classType.GetMethod("OnDestroy", 0);
            fixedUpdateMethod = classType.GetMethod("FixedUpdate", 0);
            lateUpdateMethod = classType.GetMethod("LateUpdate", 0);
            
            if (awakeMethod!=null)
            {
               appDomain.Invoke(awakeMethod, instance);
            }
        }
        //开始调用热更工程的start
        appDomain.Invoke(startMethod, instance);
    }
}