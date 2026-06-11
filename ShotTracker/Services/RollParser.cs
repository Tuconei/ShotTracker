using System;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace ShotTracker.Services;

public static partial class RollParser
{
    public static bool TryParse(
        SeString message,
        string localPlayerName,
        out string playerName,
        out int rollValue)
    {
        playerName = message.Payloads
            .OfType<PlayerPayload>()
            .Select(payload => payload.PlayerName)
            .FirstOrDefault() ?? string.Empty;

        var numbers = message.Payloads
            .OfType<TextPayload>()
            .SelectMany(payload => NumberRegex().Matches(payload.Text ?? string.Empty))
            .Select(match => int.TryParse(match.Value, out var value) ? value : -1)
            .Where(value => value >= 0)
            .Take(2)
            .ToList();

        if (numbers.Count == 0)
        {
            rollValue = -1;
            return false;
        }

        rollValue = numbers.Count == 1 ? numbers[0] : Math.Min(numbers[0], numbers[1]);
        if (playerName.Length > 0)
            return true;

        var fallback = EnglishRollRegex().Match(message.TextValue);
        if (!fallback.Success)
            return false;

        playerName = fallback.Groups["player"].Value.Trim();
        if (string.Equals(playerName, "You", StringComparison.OrdinalIgnoreCase))
            playerName = localPlayerName;

        return playerName.Length > 0;
    }

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(
        @"^Random!\s+(?<player>.+?)\s+rolls?.*?\d+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EnglishRollRegex();
}
