using System;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ShotTracker.Services;
using ShotTracker.Windows;

namespace ShotTracker;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/shottracker";

    public Configuration Configuration { get; }
    public SessionManager Sessions { get; }
    public WindowSystem WindowSystem { get; } = new("ShotTracker");

    private ConfigWindow ConfigWindow { get; }
    private MainWindow MainWindow { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Sessions = new SessionManager(Configuration);
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the ShotTracker venue game ledger.",
        });

        ChatGui.ChatMessage += OnChatMessage;
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    public void Dispose()
    {
        ChatGui.ChatMessage -= OnChatMessage;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        CommandManager.RemoveHandler(CommandName);

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    private void OnChatMessage(IHandleableChatMessage chatMessage)
    {
        if (chatMessage.LogKind != XivChatType.RandomNumber || Sessions.ActiveRound == null)
            return;

        if (!RollParser.TryParse(
                chatMessage.Message,
                PlayerState.IsLoaded ? PlayerState.CharacterName : string.Empty,
                out var playerName,
                out var roll))
        {
            Log.Warning("Could not parse random roll from: {Message}", chatMessage.Message.TextValue);
            return;
        }

        var result = Sessions.RecordRoll(playerName, roll);
        if (result.Success)
        {
            MainWindow.SetStatus(result.Message, false);
        }
        else if (!result.Message.StartsWith("Roll ignored", StringComparison.Ordinal))
        {
            Log.Debug("{Message}", result.Message);
        }
    }
}
