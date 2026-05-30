using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using MistwakeFarm.Windows;
using MistwakeFarm.Services;
using System;

namespace MistwakeFarm;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/mistwake";

    public static Configuration  Config     { get; private set; } = null!;
    public static FarmController Controller { get; private set; } = null!;

    private readonly WindowSystem _windowSystem = new("MistwakeFarm");
    private readonly MainWindow   _mainWindow;
    private readonly IDalamudPluginInterface _pi;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        _pi = pluginInterface;
        _pi.Create<Service>();

        Config     = _pi.GetPluginConfig() as Configuration ?? new Configuration();
        Controller = new FarmController();

        _mainWindow = new MainWindow();
        _windowSystem.AddWindow(_mainWindow);

        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Mistwake Farm window"
        });

        _pi.UiBuilder.Draw         += DrawUI;
        _pi.UiBuilder.OpenConfigUi += DrawConfigUI;
        _pi.UiBuilder.OpenMainUi   += DrawMainUI;

        Service.PluginLog.Information("MistwakeFarm loaded.");
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
