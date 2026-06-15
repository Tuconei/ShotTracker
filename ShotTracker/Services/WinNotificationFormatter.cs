using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShotTracker.Models;

namespace ShotTracker.Services;

public static class WinNotificationFormatter
{
    public static string Format(string template, string playerName, RollRecord roll, WinRule rule)
    {
        var prizes = string.Join(", ", roll.ExternalPrizes);
        var payout = roll.Payout > 0
            ? $"{roll.Payout.ToString("N0", CultureInfo.InvariantCulture)} gil"
            : string.Empty;
        var awardParts = new List<string>();
        if (payout.Length > 0)
            awardParts.Add(payout);
        if (prizes.Length > 0)
            awardParts.Add(prizes);
        var award = awardParts.Count == 0 ? rule.Label.Trim() : string.Join(" and ", awardParts);

        return Sanitize(template
            .Replace("{player}", playerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{roll}", roll.Value.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{rule}", rule.Label.Trim(), StringComparison.OrdinalIgnoreCase)
            .Replace("{payout}", payout, StringComparison.OrdinalIgnoreCase)
            .Replace("{prize}", prizes, StringComparison.OrdinalIgnoreCase)
            .Replace("{award}", award, StringComparison.OrdinalIgnoreCase));
    }

    public static string BuildChatCommand(WinChatChannel channel, string message)
    {
        var command = channel switch
        {
            WinChatChannel.Say => "/say",
            WinChatChannel.Yell => "/yell",
            WinChatChannel.Shout => "/shout",
            WinChatChannel.Party => "/party",
            WinChatChannel.Alliance => "/alliance",
            WinChatChannel.FreeCompany => "/freecompany",
            WinChatChannel.NoviceNetwork => "/novice",
            WinChatChannel.PvPTeam => "/pvpteam",
            WinChatChannel.Linkshell1 => "/linkshell1",
            WinChatChannel.Linkshell2 => "/linkshell2",
            WinChatChannel.Linkshell3 => "/linkshell3",
            WinChatChannel.Linkshell4 => "/linkshell4",
            WinChatChannel.Linkshell5 => "/linkshell5",
            WinChatChannel.Linkshell6 => "/linkshell6",
            WinChatChannel.Linkshell7 => "/linkshell7",
            WinChatChannel.Linkshell8 => "/linkshell8",
            WinChatChannel.CrossWorldLinkshell1 => "/cwlinkshell1",
            WinChatChannel.CrossWorldLinkshell2 => "/cwlinkshell2",
            WinChatChannel.CrossWorldLinkshell3 => "/cwlinkshell3",
            WinChatChannel.CrossWorldLinkshell4 => "/cwlinkshell4",
            WinChatChannel.CrossWorldLinkshell5 => "/cwlinkshell5",
            WinChatChannel.CrossWorldLinkshell6 => "/cwlinkshell6",
            WinChatChannel.CrossWorldLinkshell7 => "/cwlinkshell7",
            WinChatChannel.CrossWorldLinkshell8 => "/cwlinkshell8",
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
        };

        return $"{command} {Sanitize(message)}";
    }

    public static string GetChannelLabel(WinChatChannel channel)
    {
        var name = channel.ToString();
        if (name.StartsWith("CrossWorldLinkshell", StringComparison.Ordinal))
            return $"CW Linkshell {name["CrossWorldLinkshell".Length..]}";
        if (name.StartsWith("Linkshell", StringComparison.Ordinal))
            return $"Linkshell {name["Linkshell".Length..]}";

        return name switch
        {
            "FreeCompany" => "Free Company",
            "NoviceNetwork" => "Novice Network",
            "PvPTeam" => "PvP Team",
            _ => name,
        };
    }

    private static string Sanitize(string message)
    {
        var sanitized = message
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return new string(sanitized.Take(450).ToArray());
    }
}
