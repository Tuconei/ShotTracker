using System.Globalization;
using System.Text.RegularExpressions;

namespace ShotTracker.Services;

public static partial class TradeMessageParser
{
    public static bool TryParseIncoming(string message, out string playerName, out long amount)
    {
        playerName = string.Empty;
        amount = 0;

        var match = IncomingTradeRegex().Match(message.Trim());
        if (!match.Success)
            return false;

        playerName = match.Groups["player"].Value.Trim();
        var amountText = match.Groups["amount"].Value.Replace(",", string.Empty);
        return playerName.Length > 0 &&
               long.TryParse(amountText, NumberStyles.None, CultureInfo.InvariantCulture, out amount) &&
               amount > 0;
    }

    [GeneratedRegex(
        @"^(?<player>.+?)\s+trades you\s+(?<amount>\d[\d,]*)\s+gil\.$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex IncomingTradeRegex();
}
