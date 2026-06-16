using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShotTracker.Models;

[Serializable]
public enum PayoutKind
{
    FixedGil,
    JackpotPercentage,
    NonGilPrize,
}

[Serializable]
public enum WinChatChannel
{
    Say,
    Yell,
    Shout,
    Party,
    Alliance,
    FreeCompany,
    NoviceNetwork,
    PvPTeam,
    Linkshell1,
    Linkshell2,
    Linkshell3,
    Linkshell4,
    Linkshell5,
    Linkshell6,
    Linkshell7,
    Linkshell8,
    CrossWorldLinkshell1,
    CrossWorldLinkshell2,
    CrossWorldLinkshell3,
    CrossWorldLinkshell4,
    CrossWorldLinkshell5,
    CrossWorldLinkshell6,
    CrossWorldLinkshell7,
    CrossWorldLinkshell8,
}

[Serializable]
public sealed class WinRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "New rule";
    public int Number { get; set; }
    public int? RangeEnd { get; set; }
    public PayoutKind PayoutKind { get; set; }
    public long FixedPayoutGil { get; set; }
    public float JackpotPayoutPercent { get; set; }
    public string ExternalPrize { get; set; } = string.Empty;
    public bool GrantsReroll { get; set; }
    public bool HighlightWinningRoll { get; set; } = true;
    public bool SendEcho { get; set; }
    public string EchoMessage { get; set; } = "WIN: {player} rolled {roll} ({rule}) - {award}";
    public string ChatMessage { get; set; } = "Congratulations {player}! You rolled {roll} and won {award}!";
    public List<WinChatChannel> ChatChannels { get; set; } = [];
    public bool Enabled { get; set; } = true;

    public string WinningRangeText =>
        RangeEnd is { } end && end != Number
            ? $"{Number}-{end}"
            : Number.ToString(CultureInfo.InvariantCulture);

    public bool Matches(int value)
    {
        var end = RangeEnd ?? Number;
        return value >= Number && value <= end;
    }

    public bool TrySetWinningRange(string text)
    {
        var parts = text.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var start) ||
            start is < 0 or > 999)
        {
            return false;
        }

        var end = start;
        if (parts.Length == 2 &&
            (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out end) ||
             end is < 0 or > 999 ||
             end < start))
        {
            return false;
        }

        Number = start;
        RangeEnd = end == start ? null : end;
        return true;
    }

    public WinActionProfile ToActionProfile() =>
        new()
        {
            HighlightWinningRoll = HighlightWinningRoll,
            SendEcho = SendEcho,
            EchoMessage = EchoMessage,
            ChatMessage = ChatMessage,
            ChatChannels = [.. ChatChannels],
        };

    public void ApplyActionProfile(WinActionProfile profile)
    {
        HighlightWinningRoll = profile.HighlightWinningRoll;
        SendEcho = profile.SendEcho;
        EchoMessage = profile.EchoMessage;
        ChatMessage = profile.ChatMessage;
        ChatChannels = [.. profile.ChatChannels];
    }

    public WinRule Clone() =>
        new()
        {
            Id = Id,
            Label = Label,
            Number = Number,
            RangeEnd = RangeEnd,
            PayoutKind = PayoutKind,
            FixedPayoutGil = FixedPayoutGil,
            JackpotPayoutPercent = JackpotPayoutPercent,
            ExternalPrize = ExternalPrize,
            GrantsReroll = GrantsReroll,
            HighlightWinningRoll = HighlightWinningRoll,
            SendEcho = SendEcho,
            EchoMessage = EchoMessage,
            ChatMessage = ChatMessage,
            ChatChannels = [.. ChatChannels],
            Enabled = Enabled,
        };
}

[Serializable]
public sealed class WinActionProfile
{
    public bool HighlightWinningRoll { get; set; } = true;
    public bool SendEcho { get; set; }
    public string EchoMessage { get; set; } = "WIN: {player} rolled {roll} ({rule}) - {award}";
    public string ChatMessage { get; set; } = "Congratulations {player}! You rolled {roll} and won {award}!";
    public List<WinChatChannel> ChatChannels { get; set; } = [];

    public WinActionProfile Clone() =>
        new()
        {
            HighlightWinningRoll = HighlightWinningRoll,
            SendEcho = SendEcho,
            EchoMessage = EchoMessage,
            ChatMessage = ChatMessage,
            ChatChannels = [.. ChatChannels],
        };
}

[Serializable]
public sealed class VenueProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New venue";
    public int ShotPrice { get; set; } = 100_000;
    public float JackpotPercent { get; set; } = 50;
    public float HousePercent { get; set; } = 40;
    public float DealerPercent { get; set; } = 10;
    public long JackpotBalance { get; set; }
    public WinActionProfile DefaultWinActionProfile { get; set; } = new();
    public List<WinRule> WinRules { get; set; } = [];

    public VenueProfile Clone() =>
        new()
        {
            Id = Id,
            Name = Name,
            ShotPrice = ShotPrice,
            JackpotPercent = JackpotPercent,
            HousePercent = HousePercent,
            DealerPercent = DealerPercent,
            JackpotBalance = JackpotBalance,
            DefaultWinActionProfile = DefaultWinActionProfile.Clone(),
            WinRules = [.. WinRules.Select(rule => rule.Clone())],
        };
}

[Serializable]
public sealed class NightSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? EndedAt { get; set; }
    public long StartingJackpot { get; set; }
    public long EndingJackpot { get; set; }
    public long TotalIntake { get; set; }
    public long JackpotContributions { get; set; }
    public long HouseCut { get; set; }
    public long DealerCut { get; set; }
    public long UnallocatedReserve { get; set; }
    public long TotalPayouts { get; set; }
    public int ExternalPrizesAwarded { get; set; }
    public Guid? ActiveRoundId { get; set; }
    public List<PlayerRound> Rounds { get; set; } = [];
    public List<SaleRecord> Sales { get; set; } = [];
}

[Serializable]
public sealed class PendingTrade
{
    public string PlayerName { get; set; } = string.Empty;
    public long ExpectedAmount { get; set; }
    public long ReceivedAmount { get; set; }
    public DateTimeOffset ArmedAt { get; set; } = DateTimeOffset.Now;
    public string LastObservedPlayer { get; set; } = string.Empty;
    public long? LastObservedAmount { get; set; }
}

[Serializable]
public sealed class PlayerRound
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PlayerName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? EndedAt { get; set; }
    public long PaidGil { get; set; }
    public int PurchasedRolls { get; set; }
    public int RemainingRolls { get; set; }
    public long TotalPayout { get; set; }
    public int ExternalPrizesWon { get; set; }
    public List<RollRecord> Rolls { get; set; } = [];
}

[Serializable]
public sealed class SaleRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public Guid RoundId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public long Amount { get; set; }
    public int RollsPurchased { get; set; }
    public long JackpotContribution { get; set; }
    public long HouseCut { get; set; }
    public long DealerCut { get; set; }
    public long UnallocatedReserve { get; set; }
    public bool WasVerified { get; set; }
}

[Serializable]
public sealed class RollRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public int Counter { get; set; }
    public int Value { get; set; }
    public bool WasManual { get; set; }
    public bool GrantedReroll { get; set; }
    public long Payout { get; set; }
    public List<string> ExternalPrizes { get; set; } = [];
    public bool IsWin { get; set; }
    public bool HighlightWin { get; set; }
    public string Outcome { get; set; } = "No win";
}

public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}
