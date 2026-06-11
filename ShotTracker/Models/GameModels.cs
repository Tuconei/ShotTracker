using System;
using System.Collections.Generic;

namespace ShotTracker.Models;

[Serializable]
public enum PayoutKind
{
    FixedGil,
    JackpotPercentage,
}

[Serializable]
public sealed class WinRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "New rule";
    public int Number { get; set; }
    public PayoutKind PayoutKind { get; set; }
    public long FixedPayoutGil { get; set; }
    public float JackpotPayoutPercent { get; set; }
    public bool GrantsReroll { get; set; }
    public bool Enabled { get; set; } = true;
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
    public Guid? ActiveRoundId { get; set; }
    public List<PlayerRound> Rounds { get; set; } = [];
    public List<SaleRecord> Sales { get; set; } = [];
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
    public List<RollRecord> Rolls { get; set; } = [];
}

[Serializable]
public sealed class SaleRecord
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public Guid RoundId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public long Amount { get; set; }
    public int RollsPurchased { get; set; }
    public long JackpotContribution { get; set; }
    public long HouseCut { get; set; }
    public long DealerCut { get; set; }
    public long UnallocatedReserve { get; set; }
}

[Serializable]
public sealed class RollRecord
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public int Counter { get; set; }
    public int Value { get; set; }
    public bool WasManual { get; set; }
    public bool GrantedReroll { get; set; }
    public long Payout { get; set; }
    public string Outcome { get; set; } = "No win";
}

public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}
