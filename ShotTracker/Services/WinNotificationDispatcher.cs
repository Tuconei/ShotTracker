using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using ShotTracker.Models;

namespace ShotTracker.Services;

public sealed class WinNotificationDispatcher
{
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
                        SendCommand($"/echo {echo}");
                }

                var channels = rule.ChatChannels ?? [];
                var message = WinNotificationFormatter.Format(rule.ChatMessage, round.PlayerName, roll, rule);
                if (message.Length == 0)
                    continue;

                foreach (var channel in channels)
                    SendCommand(WinNotificationFormatter.BuildChatCommand(channel, message));
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

    private static unsafe void SendCommand(string command)
    {
        var uiModule = UIModule.Instance();
        if (uiModule == null)
            throw new InvalidOperationException("The game UI is not available.");

        using var message = new Utf8String(command);
        uiModule->ProcessChatBoxEntry(&message);
    }
}
