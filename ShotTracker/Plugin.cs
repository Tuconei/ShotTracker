using System;
using System.Reflection;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.SubKinds;
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
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/shottracker";

    public Configuration Configuration { get; }
    public SessionManager Sessions { get; }
    public CsvSyncService CsvSync { get; }
    public WindowSystem WindowSystem { get; } = new("ShotTracker");

    private WinNotificationDispatcher WinNotifications { get; }
    private ConfigWindow ConfigWindow { get; }
    private MainWindow MainWindow { get; }
    private string lastTradeFingerprint = string.Empty;
    private DateTimeOffset lastTradeAt = DateTimeOffset.MinValue;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? Configuration.CreateDefault();
        Sessions = new SessionManager(Configuration);
        CsvSync = new CsvSyncService(Configuration);
        WinNotifications = new WinNotificationDispatcher();
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the ShotTracker venue game ledger.",
        });

        ChatGui.ChatMessage += OnChatMessage;
        ChatGui.LogMessage += OnLogMessage;
        Sessions.WinRecorded += WinNotifications.Dispatch;
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information(
            "ShotTracker {Version} loaded; chat and structured log listeners are active.",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown");
    }

    public void Dispose()
    {
        ChatGui.ChatMessage -= OnChatMessage;
        ChatGui.LogMessage -= OnLogMessage;
        Sessions.WinRecorded -= WinNotifications.Dispatch;
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

    public bool TryGetTargetedPlayerName(out string playerName)
    {
        if (TargetManager.Target is IPlayerCharacter player)
        {
            playerName = player.Name.TextValue;
            return playerName.Length > 0;
        }

        playerName = string.Empty;
        return false;
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    private void OnChatMessage(IHandleableChatMessage chatMessage)
    {
        var originalText = chatMessage.OriginalMessage.ExtractText();

        if (Sessions.PendingTrade != null)
        {
            if (TradeMessageParser.TryParseIncoming(
                    originalText,
                    out var tradePlayer,
                    out var tradeAmount))
            {
                ConfirmParsedTrade(tradePlayer, tradeAmount);
                return;
            }
        }

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

    private void OnLogMessage(ILogMessage message)
    {
        if (Sessions.PendingTrade == null)
            return;

        var source = message.SourceEntity?.Name.ExtractText() ?? string.Empty;

        string formatted;
        try
        {
            formatted = message.FormatLogMessageForDebugging().ExtractText();
        }
        catch (Exception exception)
        {
            formatted = $"<format failed: {exception.Message}>";
        }

        if (!TradeMessageParser.TryParseIncoming(formatted, out var playerName, out var amount))
            return;

        if (playerName.Length == 0)
            playerName = source;

        ConfirmParsedTrade(playerName, amount);
    }

    private void ConfirmParsedTrade(string playerName, long amount)
    {
        var pending = Sessions.PendingTrade;
        if (pending == null)
            return;

        if (playerName.Length == 0)
            playerName = pending.PlayerName;

        var fingerprint = $"{SessionManager.NormalizeName(playerName)}:{amount}";
        var now = DateTimeOffset.UtcNow;
        if (fingerprint == lastTradeFingerprint && now - lastTradeAt < TimeSpan.FromSeconds(1))
            return;

        lastTradeFingerprint = fingerprint;
        lastTradeAt = now;
        var result = Sessions.ConfirmIncomingTrade(playerName, amount);
        MainWindow.SetStatus(result.Message, !result.Success);
    }
}
