using Dalamud.Plugin.Ipc;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace MistwakeFarm.Services;

/// <summary>
/// Wraps all third-party plugin IPC calls.
/// Each property lazily resolves the IPC gate so it won't throw if a
/// plugin isn't loaded — callers check IsAvailable before calling.
/// </summary>
internal static class IpcManager
{
    // ── AutoDuty ──────────────────────────────────────────────
    private static ICallGateSubscriber<uint, int, bool, object?>? _autoDutyRun;
    private static ICallGateSubscriber<object?>?                  _autoDutyStop;
    private static ICallGateSubscriber<bool>?                     _autoDutyIsStopped;

    public static bool AutoDutyAvailable
    {
        get
        {
            try
            {
                _autoDutyRun       ??= Service.PluginInterface.GetIpcSubscriber<uint, int, bool, object?>("AutoDuty.Run");
                _autoDutyStop      ??= Service.PluginInterface.GetIpcSubscriber<object?>("AutoDuty.Stop");
                _autoDutyIsStopped ??= Service.PluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped");
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>Start AutoDuty. dungeonId = territory content ID, runs = number of runs.</summary>
    public static void AutoDutyRun(uint dungeonId, int runs)
    {
        try { _autoDutyRun?.InvokeAction(dungeonId, runs, false); }
        catch (Exception ex) { Service.PluginLog.Error(ex, "AutoDuty.Run IPC failed"); }
    }

    public static void AutoDutyStop()
    {
        try { _autoDutyStop?.InvokeAction(); }
        catch (Exception ex) { Service.PluginLog.Error(ex, "AutoDuty.Stop IPC failed"); }
    }

    public static bool AutoDutyIsStopped()
    {
        try { return _autoDutyIsStopped?.InvokeFunc() ?? true; }
        catch { return true; }
    }

    // ── vnavmesh ──────────────────────────────────────────────
    private static ICallGateSubscriber<Vector3, bool, Task<bool>>? _vnavPathfind;
    private static ICallGateSubscriber<bool>?                       _vnavIsRunning;
    private static ICallGateSubscriber<bool>?                       _vnavIsReady;
    private static ICallGateSubscriber<object?>?                    _vnavStop;

    public static bool VnavAvailable
    {
        get
        {
            try
            {
                _vnavPathfind  ??= Service.PluginInterface.GetIpcSubscriber<Vector3, bool, Task<bool>>("vnavmesh.SimpleMove.PathfindAndMoveTo");
                _vnavIsRunning ??= Service.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsRunning");
                _vnavIsReady   ??= Service.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
                _vnavStop      ??= Service.PluginInterface.GetIpcSubscriber<object?>("vnavmesh.Nav.Stop");
                return true;
            }
            catch { return false; }
        }
    }

    public static async Task<bool> VnavMoveToAsync(Vector3 dest, bool fly = false)
    {
        try
        {
            if (_vnavPathfind == null) return false;
            return await _vnavPathfind.InvokeFunc(dest, fly);
        }
        catch (Exception ex) { Service.PluginLog.Error(ex, "vnavmesh pathfind IPC failed"); return false; }
    }

    public static bool VnavIsRunning()
    {
        try { return _vnavIsRunning?.InvokeFunc() ?? false; }
        catch { return false; }
    }

    public static bool VnavIsReady()
    {
        try { return _vnavIsReady?.InvokeFunc() ?? false; }
        catch { return false; }
    }

    public static void VnavStop()
    {
        try { _vnavStop?.InvokeAction(); }
        catch (Exception ex) { Service.PluginLog.Error(ex, "vnavmesh stop IPC failed"); }
    }

    // ── Lifestream ────────────────────────────────────────────
    private static ICallGateSubscriber<string, bool>? _lifestreamExecute;
    private static ICallGateSubscriber<bool>?          _lifestreamIsBusy;

    public static bool LifestreamAvailable
    {
        get
        {
            try
            {
                _lifestreamExecute ??= Service.PluginInterface.GetIpcSubscriber<string, bool>("Lifestream.ExecuteCommand");
                _lifestreamIsBusy  ??= Service.PluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
                return true;
            }
            catch { return false; }
        }
    }

    public static bool LifestreamExecute(string command)
    {
        try { return _lifestreamExecute?.InvokeFunc(command) ?? false; }
        catch (Exception ex) { Service.PluginLog.Error(ex, "Lifestream.ExecuteCommand IPC failed"); return false; }
    }

    public static bool LifestreamIsBusy()
    {
        try { return _lifestreamIsBusy?.InvokeFunc() ?? false; }
        catch { return false; }
    }

    /// <summary>Clears all cached IPC subscribers so they re-resolve on next use.</summary>
    public static void Reset()
    {
        _autoDutyRun       = null;
        _autoDutyStop      = null;
        _autoDutyIsStopped = null;
        _vnavPathfind      = null;
        _vnavIsRunning     = null;
        _vnavIsReady       = null;
        _vnavStop          = null;
        _lifestreamExecute = null;
        _lifestreamIsBusy  = null;
    }
}
