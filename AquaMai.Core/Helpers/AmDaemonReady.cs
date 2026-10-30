using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;

namespace AquaMai.Core.Helpers;

/**
 * amdaemon 就绪前拿不到完整的游戏数据目录：
 * option 包要等 amdaemon 就绪后才挂载，这时候去扫描只会看到 StreamingAssets 里的数据包，
 * 结果被缓存下来的话后面所有调用方都会看不到 option 包里的内容。
 *
 * 所以需要扫描目录的功能统一走 RunWhenReady，等 amdaemon 就绪后再执行，
 * 顺便也不占用启动时的耗时。
 */
public static class AmDaemonReady
{
    private static readonly List<Action> _pending = [];

    public static bool IsReady { get; private set; }

    // 已经就绪就直接执行，否则排队等就绪后执行
    public static void RunWhenReady(Action action)
    {
        if (action == null) return;
        if (IsReady)
        {
            Run(action);
            return;
        }

        _pending.Add(action);
    }

    // PowerOnProcess 在 amdaemon 就绪后调用它，此时 option 包都已挂载
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MAI2System.SystemConfig), "initializeAfterAMDaemonReady")]
    public static void MarkReady()
    {
        if (IsReady) return;
        IsReady = true;

        foreach (var action in _pending) Run(action);
        _pending.Clear();
    }

    private static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            // 一个延迟任务失败不能影响其它延迟任务和游戏本身
            MelonLogger.Error($"[AmDaemonReady] Deferred action failed: {e}");
        }
    }
}
