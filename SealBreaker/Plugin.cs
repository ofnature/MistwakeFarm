using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using SealBreaker.Windows;
using SealBreaker.Services;
using System;

namespace SealBreaker;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/sealbreaker";

    public static Configuration  Config     { get; private set; } = null!;
    public static FarmController Controller { get; private set; } = null!;

    private readonly WindowSystem _windowSystem = new("SealBreaker");
    private readonly MainWindow   _mainWindow;
    private readonly IDalamudPluginInterface _pi;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        _pi = pluginInterface;
        _pi.Create<Service>();

        Config = _pi.GetPluginConfig() as Configuration ?? new Configuration();
        Config.EnsureGcTownNav();
        Controller = new FarmController();

        GcShopCatalog.EnsureInitialized();

        _mainWindow = new MainWindow();
        _windowSystem.AddWindow(_mainWindow);

        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Seal Breaker window"
        });

        _pi.UiBuilder.Draw         += DrawUI;
        _pi.UiBuilder.OpenConfigUi += DrawConfigUI;
        _pi.UiBuilder.OpenMainUi   += DrawMainUI;

        Service.PluginLog.Information("SealBreaker loaded.");
    }

    public void Dispose()
    {
        Controller.Dispose();
        _windowSystem.RemoveAllWindows();
        _mainWindow.Dispose();
        Service.CommandManager.RemoveHandler(CommandName);
        _pi.UiBuilder.Draw         -= DrawUI;
        _pi.UiBuilder.OpenConfigUi -= DrawConfigUI;
        _pi.UiBuilder.OpenMainUi   -= DrawMainUI;
    }

    private void OnCommand(string command, string args) => _mainWindow.Toggle();
    private void DrawUI()       => _windowSystem.Draw();
    private void DrawConfigUI() => _mainWindow.Toggle();
    private void DrawMainUI()   => _mainWindow.Toggle();
}
