using System;
using System.Collections.Generic;
using System.Linq;
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
    public WinActionProfile DefaultWinActionProfile { get; set; } = new();
    public Guid? ActiveVenueProfileId { get; set; }
    public List<VenueProfile> VenueProfiles { get; set; } = [];
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

    public VenueProfile CaptureVenueProfile(string name)
    {
        return new VenueProfile
        {
            Name = name.Trim().Length == 0 ? "New venue" : name.Trim(),
            ShotPrice = ShotPrice,
            JackpotPercent = JackpotPercent,
            HousePercent = HousePercent,
            DealerPercent = DealerPercent,
            JackpotBalance = JackpotBalance,
            DefaultWinActionProfile = DefaultWinActionProfile.Clone(),
            WinRules = [.. WinRules.Select(rule => rule.Clone())],
        };
    }

    public void SaveVenueProfile(VenueProfile profile)
    {
        var captured = CaptureVenueProfile(profile.Name);
        captured.Id = profile.Id;
        var index = VenueProfiles.FindIndex(existing => existing.Id == profile.Id);
        if (index >= 0)
            VenueProfiles[index] = captured;
        else
            VenueProfiles.Add(captured);

        ActiveVenueProfileId = captured.Id;
    }

    public void ApplyVenueProfile(VenueProfile profile)
    {
        var copy = profile.Clone();
        ShotPrice = Math.Max(1, copy.ShotPrice);
        JackpotPercent = Math.Max(0, copy.JackpotPercent);
        HousePercent = Math.Max(0, copy.HousePercent);
        DealerPercent = Math.Max(0, copy.DealerPercent);
        JackpotBalance = Math.Max(0, copy.JackpotBalance);
        DefaultWinActionProfile = copy.DefaultWinActionProfile;
        WinRules = copy.WinRules;
        ActiveVenueProfileId = copy.Id;
        PendingTrade = null;
    }
}
