using System;
using System.Collections.Generic;
using System.Linq;
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
    public WindowSystem WindowSystem { get; } = new("ShotTracker");
    public IReadOnlyList<string> TradeDiagnostics => tradeDiagnostics;

    private ConfigWindow ConfigWindow { get; }
    private MainWindow MainWindow { get; }
    private readonly List<string> tradeDiagnostics = [];
    private string lastTradeFingerprint = string.Empty;
    private DateTimeOffset lastTradeAt = DateTimeOffset.MinValue;

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
        ChatGui.LogMessage += OnLogMessage;
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
            AddTradeDiagnostic(
                $"Chat {chatMessage.LogKind}: sender='{chatMessage.OriginalSender.ExtractText()}', " +
                $"text='{originalText}'");

            if (TradeMessageParser.TryParseIncoming(
                    originalText,
                    out var tradePlayer,
                    out var tradeAmount))
            {
                ConfirmParsedTrade(tradePlayer, tradeAmount, "chat");
                return;
            }

            Log.Debug(
                "Pending trade chat event. Type={ChatType}, Sender={Sender}, Message={Message}",
                chatMessage.LogKind,
                chatMessage.OriginalSender.ExtractText(),
                originalText);
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
        var parameters = Enumerable.Range(0, message.ParameterCount)
            .Select(index =>
            {
                if (message.TryGetIntParameter(index, out var intValue))
                    return $"{index}=int:{intValue}";

                if (message.TryGetStringParameter(index, out var stringValue))
                    return $"{index}=text:'{stringValue.ExtractText()}'";

                return $"{index}=other";
            });

        string formatted;
        try
        {
            formatted = message.FormatLogMessageForDebugging().ExtractText();
        }
        catch (Exception exception)
        {
            formatted = $"<format failed: {exception.Message}>";
        }

        AddTradeDiagnostic(
            $"LogMessage #{message.LogMessageId}: source='{source}', " +
            $"params=[{string.Join(", ", parameters)}], text='{formatted}'");
        Log.Information(
            "Pending trade LogMessage #{LogMessageId}: Source={Source}, Parameters=[{Parameters}], Text={Text}",
            message.LogMessageId,
            source,
            string.Join(", ", parameters),
            formatted);

        if (!TradeMessageParser.TryParseIncoming(formatted, out var playerName, out var amount))
            return;

        if (playerName.Length == 0)
            playerName = source;

        ConfirmParsedTrade(playerName, amount, $"LogMessage #{message.LogMessageId}");
    }

    private void ConfirmParsedTrade(string playerName, long amount, string source)
    {
        var pending = Sessions.PendingTrade;
        if (pending == null)
            return;

        if (playerName.Length == 0)
            playerName = pending.PlayerName;

        var fingerprint = $"{SessionManager.NormalizeName(playerName)}:{amount}";
        var now = DateTimeOffset.UtcNow;
        if (fingerprint == lastTradeFingerprint && now - lastTradeAt < TimeSpan.FromSeconds(1))
        {
            AddTradeDiagnostic($"Ignored duplicate {amount:N0} gil event from {playerName} ({source}).");
            return;
        }

        lastTradeFingerprint = fingerprint;
        lastTradeAt = now;
        var result = Sessions.ConfirmIncomingTrade(playerName, amount);
        AddTradeDiagnostic($"{source}: {result.Message}");
        MainWindow.SetStatus(result.Message, !result.Success);
    }

    public void AddTradeDiagnostic(string message)
    {
        tradeDiagnostics.Insert(0, $"{DateTime.Now:T} {message}");
        if (tradeDiagnostics.Count > 12)
            tradeDiagnostics.RemoveRange(12, tradeDiagnostics.Count - 12);
    }
}
