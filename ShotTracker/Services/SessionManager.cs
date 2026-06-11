using System;
using System.Linq;
using ShotTracker.Models;

namespace ShotTracker.Services;

public sealed class SessionManager
{
    private readonly Configuration configuration;

    public SessionManager(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public NightSession? ActiveSession => configuration.ActiveSession;

    public PlayerRound? ActiveRound
    {
        get
        {
            var session = ActiveSession;
            if (session?.ActiveRoundId is not { } roundId)
                return null;

            return session.Rounds.FirstOrDefault(round => round.Id == roundId);
        }
    }

    public OperationResult StartNight()
    {
        if (ActiveSession != null)
            return OperationResult.Fail("A night is already active.");

        configuration.ActiveSession = new NightSession
        {
            StartingJackpot = configuration.JackpotBalance,
            EndingJackpot = configuration.JackpotBalance,
        };
        configuration.Save();
        return OperationResult.Ok("Night started.");
    }

    public OperationResult RecordTrade(string playerName, long amount)
    {
        var session = ActiveSession;
        if (session == null)
            return OperationResult.Fail("Start the night before recording a trade.");

        playerName = playerName.Trim();
        if (playerName.Length == 0)
            return OperationResult.Fail("Enter the participating player's name.");

        if (configuration.ShotPrice <= 0)
            return OperationResult.Fail("Shot price must be greater than zero.");

        if (amount <= 0 || amount % configuration.ShotPrice != 0)
            return OperationResult.Fail($"Trade must be a positive multiple of {configuration.ShotPrice:N0} gil.");

        var splitTotal = configuration.JackpotPercent +
                         configuration.HousePercent +
                         configuration.DealerPercent;
        if (!float.IsFinite(splitTotal) || splitTotal > 100)
            return OperationResult.Fail("House, dealer, and jackpot percentages cannot total more than 100%.");

        var activeRound = ActiveRound;
        if (activeRound != null &&
            !string.Equals(activeRound.PlayerName, playerName, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Fail($"Finish {activeRound.PlayerName}'s round before starting another player.");
        }

        if (activeRound == null)
        {
            activeRound = new PlayerRound { PlayerName = playerName };
            session.Rounds.Add(activeRound);
            session.ActiveRoundId = activeRound.Id;
        }

        var rolls = checked((int)(amount / configuration.ShotPrice));
        var jackpot = PercentOf(amount, configuration.JackpotPercent);
        var house = PercentOf(amount, configuration.HousePercent);
        var dealer = PercentOf(amount, configuration.DealerPercent);
        var allocated = jackpot + house + dealer;
        var reserve = amount - allocated;
        activeRound.PaidGil += amount;
        activeRound.PurchasedRolls += rolls;
        activeRound.RemainingRolls += rolls;

        session.TotalIntake += amount;
        session.JackpotContributions += jackpot;
        session.HouseCut += house;
        session.DealerCut += dealer;
        session.UnallocatedReserve += reserve;
        session.EndingJackpot += jackpot;
        configuration.JackpotBalance += jackpot;

        session.Sales.Add(new SaleRecord
        {
            RoundId = activeRound.Id,
            PlayerName = activeRound.PlayerName,
            Amount = amount,
            RollsPurchased = rolls,
            JackpotContribution = jackpot,
            HouseCut = house,
            DealerCut = dealer,
            UnallocatedReserve = reserve,
        });

        configuration.Save();
        return OperationResult.Ok(
            $"Recorded {amount:N0} gil from {activeRound.PlayerName}: {rolls} roll(s).");
    }

    public OperationResult RecordRoll(string sender, int value, bool wasManual = false)
    {
        var session = ActiveSession;
        var round = ActiveRound;
        if (session == null || round == null)
            return OperationResult.Fail("There is no active player.");

        if (!wasManual && !NamesMatch(round.PlayerName, sender))
            return OperationResult.Fail("Roll ignored because it was not from the active player.");

        if (round.RemainingRolls <= 0)
            return OperationResult.Fail($"{round.PlayerName} has no rolls remaining.");

        if (value is < 0 or > 999)
            return OperationResult.Fail("Roll must be between 0 and 999.");

        round.RemainingRolls--;

        var matches = configuration.WinRules
            .Where(rule => rule.Enabled && rule.Number == value)
            .ToList();
        var reroll = matches.Any(rule => rule.GrantsReroll);
        if (reroll)
            round.RemainingRolls++;

        var jackpotAtRoll = configuration.JackpotBalance;
        long requestedPayout = 0;
        foreach (var rule in matches)
        {
            var requested = rule.PayoutKind switch
            {
                PayoutKind.FixedGil => rule.FixedPayoutGil,
                PayoutKind.JackpotPercentage => PercentOf(jackpotAtRoll, rule.JackpotPayoutPercent),
                _ => 0,
            };

            requested = Math.Max(0, requested);
            requestedPayout = requested > long.MaxValue - requestedPayout
                ? long.MaxValue
                : requestedPayout + requested;
        }

        var payout = Math.Min(requestedPayout, jackpotAtRoll);
        configuration.JackpotBalance -= payout;
        session.EndingJackpot -= payout;
        round.TotalPayout += payout;
        session.TotalPayouts += payout;
        var labels = matches.Select(rule => rule.Label.Trim())
            .Where(label => label.Length > 0)
            .ToList();
        var outcome = labels.Count == 0 ? "No win" : string.Join(", ", labels);
        if (reroll)
            outcome += " + reroll";

        round.Rolls.Add(new RollRecord
        {
            Counter = round.Rolls.Count + 1,
            Value = value,
            WasManual = wasManual,
            GrantedReroll = reroll,
            Payout = payout,
            Outcome = outcome,
        });

        if (round.RemainingRolls == 0)
            FinishActiveRoundInternal();

        configuration.Save();
        return OperationResult.Ok(
            payout > 0
                ? $"{round.PlayerName} rolled {value} and won {payout:N0} gil."
                : $"{round.PlayerName} rolled {value}: {outcome}.");
    }

    public OperationResult FinishActiveRound()
    {
        var round = ActiveRound;
        if (round == null)
            return OperationResult.Fail("There is no active player.");

        FinishActiveRoundInternal();
        configuration.Save();
        return OperationResult.Ok($"{round.PlayerName}'s round was ended.");
    }

    public OperationResult CloseNight()
    {
        var session = ActiveSession;
        if (session == null)
            return OperationResult.Fail("There is no active night.");

        if (ActiveRound != null)
            FinishActiveRoundInternal();

        session.EndedAt = DateTimeOffset.Now;
        session.EndingJackpot = configuration.JackpotBalance;
        configuration.SessionHistory.Insert(0, session);
        configuration.ActiveSession = null;
        configuration.Save();
        return OperationResult.Ok("Night closed and saved to history.");
    }

    private void FinishActiveRoundInternal()
    {
        var session = ActiveSession;
        var round = ActiveRound;
        if (session == null || round == null)
            return;

        round.EndedAt = DateTimeOffset.Now;
        session.ActiveRoundId = null;
    }

    private static long PercentOf(long amount, float percent)
    {
        if (amount <= 0 || percent <= 0)
            return 0;

        return (long)Math.Floor(amount * (decimal)percent / 100m);
    }

    private static bool NamesMatch(string expected, string actual)
    {
        static string Normalize(string value)
        {
            var withoutWorld = value.Trim().Split(['@', '\uE05D'], 2)[0];
            var parts = withoutWorld.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', parts.Take(2));
        }

        var normalizedExpected = Normalize(expected);
        var normalizedActual = Normalize(actual);
        return string.Equals(normalizedExpected, normalizedActual, StringComparison.OrdinalIgnoreCase) ||
               normalizedActual.StartsWith(normalizedExpected + " ", StringComparison.OrdinalIgnoreCase);
    }
}
