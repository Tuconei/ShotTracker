using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using ShotTracker.Models;

namespace ShotTracker.Services;

public sealed class WinNotificationDispatcher
{
    private static readonly TimeSpan CommandSpacing = TimeSpan.FromMilliseconds(750);

    private readonly Configuration configuration;
    private readonly Queue<PendingCommand> pendingCommands = [];
    private DateTimeOffset nextCommandAt = DateTimeOffset.MinValue;

    public WinNotificationDispatcher(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void Dispatch(PlayerRound round, RollRecord roll, IReadOnlyList<WinRule> rules)
    {
        foreach (var rule in rules)
        {
            try
            {
                if (rule.SendEcho)
                {
                    var echo = WinNotificationFormatter.Format(rule.EchoMessage, round.PlayerName, roll, rule);
                    if (echo.Length > 0)
                        pendingCommands.Enqueue(new PendingCommand($"/echo {echo}", $"rule '{rule.Label}'"));
                }

                var channels = rule.ChatChannels ?? [];
                var message = WinNotificationFormatter.Format(rule.ChatMessage, round.PlayerName, roll, rule);
                if (message.Length == 0)
                    continue;

                foreach (var channel in channels)
                    pendingCommands.Enqueue(new PendingCommand(
                        WinNotificationFormatter.BuildChatCommand(channel, message),
                        $"rule '{rule.Label}'"));
            }
            catch (Exception exception)
            {
                Plugin.Log.Error(exception, "Failed to execute win notifications for rule {Rule}", rule.Label);
                Plugin.ChatGui.PrintError(
                    $"A win was recorded, but notifications failed for rule '{rule.Label}'.",
                    "ShotTracker");
            }
        }
    }

    public void DispatchPaidRollsExhausted(PlayerRound round)
    {
        var profile = configuration.PaidRollsExhaustedMessageProfile;
        try
        {
            if (profile.SendEcho)
            {
                var echo = WinNotificationFormatter.FormatPaidRollsExhausted(profile.EchoMessage, round);
                if (echo.Length > 0)
                    pendingCommands.Enqueue(new PendingCommand($"/echo {echo}", $"paid rolls exhausted for {round.PlayerName}"));
            }

            var channels = profile.ChatChannels ?? [];
            var message = WinNotificationFormatter.FormatPaidRollsExhausted(profile.ChatMessage, round);
            if (message.Length == 0)
                return;

            foreach (var channel in channels)
                pendingCommands.Enqueue(new PendingCommand(
                    WinNotificationFormatter.BuildChatCommand(channel, message),
                    $"paid rolls exhausted for {round.PlayerName}"));
        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, "Failed to execute paid rolls exhausted notification for {Player}", round.PlayerName);
            Plugin.ChatGui.PrintError(
                $"Paid rolls were exhausted for {round.PlayerName}, but notifications failed.",
                "ShotTracker");
        }
    }

    public void FlushOne()
    {
        if (DateTimeOffset.UtcNow < nextCommandAt)
            return;

        if (!pendingCommands.TryDequeue(out var pending))
            return;

        try
        {
            SendCommand(pending.Command);
            nextCommandAt = DateTimeOffset.UtcNow + CommandSpacing;
        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, "Failed to send queued notification for {Context}", pending.Context);
            Plugin.ChatGui.PrintError(
                $"A queued notification failed for {pending.Context}.",
                "ShotTracker");
        }
    }

    private static unsafe void SendCommand(string command)
    {
        var uiModule = UIModule.Instance();
        if (uiModule == null)
            throw new InvalidOperationException("The game UI is not available.");

        using var message = new Utf8String(command);
        uiModule->ProcessChatBoxEntry(&message);
    }

    private sealed record PendingCommand(string Command, string Context);
}
