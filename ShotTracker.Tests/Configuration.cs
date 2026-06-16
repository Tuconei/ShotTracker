using System.Collections.Generic;
using ShotTracker.Models;

namespace ShotTracker;

public sealed class Configuration
{
    public int ShotPrice { get; set; } = 100;
    public float JackpotPercent { get; set; } = 50;
    public float HousePercent { get; set; } = 40;
    public float DealerPercent { get; set; } = 10;
    public long JackpotBalance { get; set; }
    public List<WinRule> WinRules { get; set; } = [];
    public NightSession? ActiveSession { get; set; }
    public PendingTrade? PendingTrade { get; set; }
    public List<NightSession> SessionHistory { get; set; } = [];

    public static Configuration CreateDefault()
    {
        return new Configuration
        {
            WinRules =
            [
                new()
                {
                    Label = "Perfect roll",
                    Number = 777,
                    PayoutKind = PayoutKind.JackpotPercentage,
                    JackpotPayoutPercent = 100,
                },
                new()
                {
                    Label = "Lucky reroll",
                    Number = 7,
                    GrantsReroll = true,
                },
            ],
        };
    }

    public void Save()
    {
    }
}
