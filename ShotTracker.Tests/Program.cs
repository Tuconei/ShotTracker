using ShotTracker;
using ShotTracker.Models;
using ShotTracker.Services;

RunInvalidSplitTest();
RunTradeVerificationTest();
RunNightLifecycleTest();
Console.WriteLine("All ShotTracker accounting tests passed.");

static void RunInvalidSplitTest()
{
    var configuration = new Configuration
    {
        JackpotPercent = 60,
        HousePercent = 50,
    };
    var manager = new SessionManager(configuration);

    Assert(manager.StartNight().Success, "Night should start.");
    Assert(!manager.RecordTrade("Test Player", 100).Success, "Invalid split should fail.");
    Assert(configuration.ActiveSession!.Rounds.Count == 0, "Failed trade must not create a round.");
    Assert(configuration.ActiveSession.TotalIntake == 0, "Failed trade must not alter intake.");
}

static void RunTradeVerificationTest()
{
    Assert(
        TradeMessageParser.TryParseIncoming(
            "Test Player trades you 100,000 Gil.",
            out var parsedPlayer,
            out var parsedAmount),
        "Incoming trade message should parse.");
    Assert(parsedPlayer == "Test Player", "Trade parser should preserve the player name.");
    Assert(parsedAmount == 100_000, "Trade parser should parse comma-formatted gil.");

    var configuration = new Configuration
    {
        ShotPrice = 100_000,
    };
    var manager = new SessionManager(configuration);
    Assert(manager.StartNight().Success, "Night should start.");
    Assert(
        manager.ArmTradeVerification("Test Player", 200_000).Success,
        "Valid expected trade should arm.");
    Assert(manager.ActiveRound == null, "Arming a trade must not grant rolls.");

    Assert(
        !manager.ConfirmIncomingTrade("Other Player", 200_000).Success,
        "Wrong player should not verify.");
    Assert(manager.ActiveRound == null, "Wrong player must not grant rolls.");
    Assert(configuration.PendingTrade != null, "Wrong player should leave verification armed.");

    Assert(
        !manager.ConfirmIncomingTrade("Test Player", 100_000).Success,
        "Wrong amount should not verify.");
    Assert(manager.ActiveRound == null, "Wrong amount must not grant rolls.");
    Assert(configuration.PendingTrade!.LastObservedAmount == 100_000, "Mismatch should be visible.");

    Assert(
        manager.ConfirmIncomingTrade("Test Player@Example", 200_000).Success,
        "Matching cross-world player and amount should verify.");
    Assert(configuration.PendingTrade == null, "Successful verification should clear pending state.");
    Assert(manager.ActiveRound!.RemainingRolls == 2, "Verified trade should grant purchased rolls.");
    Assert(
        configuration.ActiveSession!.Sales.Single().WasVerified,
        "Verified sale should be marked in the ledger.");

    Assert(
        manager.ArmTradeVerification("Test Player", 100_000).Success,
        "Additional expected trade should arm.");
    Assert(
        manager.RecordTradeManually("Test Player", 100_000).Success,
        "Manual override should record the trade.");
    Assert(configuration.PendingTrade == null, "Manual override should clear pending verification.");
    Assert(
        !configuration.ActiveSession.Sales.Last().WasVerified,
        "Manual override should remain visibly unverified.");
}

static void RunNightLifecycleTest()
{
    var configuration = new Configuration
    {
        JackpotBalance = 1_000,
        WinRules =
        [
            new WinRule { Label = "Reroll", Number = 7, GrantsReroll = true },
            new WinRule
            {
                Label = "Half A",
                Number = 777,
                PayoutKind = PayoutKind.JackpotPercentage,
                JackpotPayoutPercent = 50,
            },
            new WinRule
            {
                Label = "Half B",
                Number = 777,
                PayoutKind = PayoutKind.JackpotPercentage,
                JackpotPayoutPercent = 50,
            },
            new WinRule
            {
                Label = "Fixed",
                Number = 1,
                PayoutKind = PayoutKind.FixedGil,
                FixedPayoutGil = 500,
            },
        ],
    };
    var manager = new SessionManager(configuration);

    Assert(manager.StartNight().Success, "Night should start.");
    Assert(manager.RecordTrade("Test Player", 300).Success, "Trade should be accepted.");
    Assert(manager.ActiveRound!.RemainingRolls == 3, "Trade should buy three rolls.");
    Assert(configuration.JackpotBalance == 1_150, "Trade should add 50% to jackpot.");

    Assert(!manager.RecordRoll("Other Player", 42).Success, "Other player's roll should be ignored.");
    Assert(manager.ActiveRound.RemainingRolls == 3, "Ignored roll must not be consumed.");

    Assert(manager.RecordRoll("Test Player", 7).Success, "Reroll result should be accepted.");
    Assert(manager.ActiveRound.RemainingRolls == 3, "Reroll should replace the consumed roll.");

    Assert(manager.RecordRoll("Test Player", 777).Success, "Winning roll should be accepted.");
    Assert(configuration.JackpotBalance == 0, "Two 50% wins should use the same pre-roll jackpot.");
    Assert(manager.ActiveRound.RemainingRolls == 2, "Winning roll should consume one roll.");

    Assert(manager.RecordTrade("Test Player", 100).Success, "Top-up trade should be accepted.");
    Assert(configuration.JackpotBalance == 50, "Top-up should replenish jackpot.");
    Assert(manager.RecordRoll("Test Player", 1).Success, "Fixed win should be accepted.");
    Assert(configuration.JackpotBalance == 0, "Fixed payout should be capped at jackpot balance.");

    var session = configuration.ActiveSession!;
    Assert(session.TotalIntake == 400, "Night intake should include all trades.");
    Assert(session.JackpotContributions == 200, "Jackpot contributions should total correctly.");
    Assert(session.HouseCut == 160, "House cut should total correctly.");
    Assert(session.DealerCut == 40, "Dealer cut should total correctly.");
    Assert(session.TotalPayouts == 1_200, "Payout total should include capped awards.");

    Assert(manager.CloseNight().Success, "Night should close.");
    Assert(configuration.ActiveSession == null, "Closed night should no longer be active.");
    Assert(configuration.SessionHistory.Count == 1, "Closed night should be stored in history.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
