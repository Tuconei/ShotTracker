using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ShotTracker.Services;

public static partial class TradeMessageParser
{
    public static bool TryParseIncoming(string message, out string playerName, out long amount)
    {
        playerName = string.Empty;
        amount = 0;

        foreach (var regex in new[]
                 {
                     PlayerTradesYouRegex(),
                     ReceiveFromPlayerRegex(),
                     PlayerGivesYouRegex(),
                     ReceiveGilRegex(),
                 })
        {
            var match = regex.Match(Normalize(message));
            if (!match.Success)
                continue;

            playerName = match.Groups["player"].Value.Trim();
            var amountText = match.Groups["amount"].Value
                .Replace(",", string.Empty)
                .Replace(".", string.Empty)
                .Replace(" ", string.Empty);
            return long.TryParse(
                       amountText,
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out amount) &&
                   amount > 0;
        }

        return false;
    }

    public static bool LooksLikeGilMessage(string message) =>
        Normalize(message).Contains("gil", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string message) =>
        WhitespaceRegex().Replace(message.Trim(), " ");

    [GeneratedRegex(
        @"^(?<player>.+?)\s+trades you\s+(?<amount>\d[\d,.\s]*)\s+gil[.!]?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PlayerTradesYouRegex();

    [GeneratedRegex(
        @"^You\s+(?:receive|received|obtain|obtained)\s+(?<amount>\d[\d,.\s]*)\s+gil\s+from\s+(?<player>.+?)[.!]?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ReceiveFromPlayerRegex();

    [GeneratedRegex(
        @"^(?<player>.+?)\s+(?:gives|gave)\s+you\s+(?<amount>\d[\d,.\s]*)\s+gil[.!]?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PlayerGivesYouRegex();

    [GeneratedRegex(
        @"^You\s+(?:receive|received|obtain|obtained)\s+(?<amount>\d[\d,.\s]*)\s+gil[.!]?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ReceiveGilRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
