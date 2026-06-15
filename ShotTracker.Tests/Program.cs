using ShotTracker;
using ShotTracker.Models;
using ShotTracker.Services;

RunInvalidSplitTest();
RunTradeVerificationTest();
RunNightLifecycleTest();
RunCsvSyncTest();
RunClearHistoryTest();
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
    Assert(
        TradeMessageParser.TryParseIncoming(
            "You receive 1,000,000 gil from Test Player.",
            out parsedPlayer,
            out parsedAmount),
        "Incoming trade message with a trailing player should parse.");
    Assert(parsedPlayer == "Test Player", "Trailing player name should parse.");
    Assert(parsedAmount == 1_000_000, "One million gil should parse.");

    var configuration = new Configuration
    {
        ShotPrice = 100_000,
    };
    var manager = new SessionManager(configuration);
    Assert(manager.StartNight().Success, "Night should start.");
    Assert(
        !manager.ArmTradeVerification("Test Player", 250_000).Success,
        "Requested totals must remain exact shot-price multiples.");
    Assert(
        manager.ArmTradeVerification("Test Player", 2_000_000).Success,
        "Valid expected trade should arm.");
    Assert(manager.ActiveRound == null, "Arming a trade must not grant rolls.");

    Assert(
        !manager.ConfirmIncomingTrade("Other Player", 1_000_000).Success,
        "Wrong player should not verify.");
    Assert(manager.ActiveRound == null, "Wrong player must not grant rolls.");
    Assert(configuration.PendingTrade != null, "Wrong player should leave verification armed.");

    Assert(
        manager.ConfirmIncomingTrade("Test Player", 1_000_000).Success,
        "First capped trade should verify as a partial payment.");
    Assert(manager.ActiveRound == null, "Partial payment must not grant rolls.");
    Assert(configuration.PendingTrade!.ReceivedAmount == 1_000_000, "Partial payment should accumulate.");
    Assert(
        !manager.ConfirmIncomingTrade("Test Player", 1_000_001).Success,
        "A chunk larger than the remaining amount should be rejected.");
    Assert(
        configuration.PendingTrade.ReceivedAmount == 1_000_000,
        "Rejected overpayment must not change accumulated progress.");

    Assert(
        manager.ConfirmIncomingTrade("Test Player@Example", 1_000_000).Success,
        "Second capped trade should complete verification.");
    Assert(configuration.PendingTrade == null, "Successful verification should clear pending state.");
    Assert(manager.ActiveRound!.RemainingRolls == 20, "Accumulated verified trades should grant all purchased rolls.");
    Assert(
        configuration.ActiveSession!.Sales.Single().WasVerified,
        "Verified sale should be marked in the ledger.");
    Assert(
        configuration.ActiveSession.Sales.Single().Amount == 2_000_000,
        "Ledger should record the requested total once.");

    Assert(manager.ArmTradeVerification("Test Player", 300_000).Success, "Any shot-price multiple should arm.");
    Assert(manager.ConfirmIncomingTrade("Test Player", 125_000).Success, "Arbitrary trade chunks should accumulate.");
    Assert(manager.ConfirmIncomingTrade("Test Player", 175_000).Success, "A final chunk should complete the total.");
    Assert(manager.ActiveRound.RemainingRolls == 23, "Three additional rolls should be credited.");

    Assert(manager.ArmTradeVerification("Test Player", 100_000).Success, "Additional expected trade should arm.");
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

static void RunCsvSyncTest()
{
    var first = new Configuration
    {
        JackpotBalance = 1_000,
        ShotPrice = 250,
        JackpotPercent = 60,
        HousePercent = 30,
        DealerPercent = 10,
        WinRules = [new WinRule { Label = "Winner, big", Number = 777, FixedPayoutGil = 100 }],
    };
    var firstManager = new SessionManager(first);
    Assert(firstManager.StartNight().Success, "First bartender should start the shared night.");

    var seedPath = Path.Combine(Path.GetTempPath(), $"ShotTracker-seed-{Guid.NewGuid():N}.csv");
    var mergedPath = Path.Combine(Path.GetTempPath(), $"ShotTracker-merged-{Guid.NewGuid():N}.csv");
    try
    {
        Assert(new CsvSyncService(first).Export(seedPath).Success, "Seed export should succeed.");

        var second = new Configuration();
        Assert(new CsvSyncService(second).Import(seedPath).Success, "Second bartender should import the shared night.");
        var secondManager = new SessionManager(second);
        Assert(secondManager.ActiveSession?.Id == first.ActiveSession?.Id, "Imported active night should keep its ID.");
        Assert(second.ShotPrice == 250, "Import should sync the shot price.");
        Assert(second.WinRules.Single().Label == "Winner, big", "Import should sync CSV-escaped winning rules.");

        Assert(firstManager.RecordTrade("First Player", 500, true).Success, "First sale should record.");
        Assert(firstManager.RecordRoll("First Player", 123).Success, "First roll should record.");
        Assert(secondManager.RecordTrade("Second Player", 750, true).Success, "Second sale should record.");
        Assert(secondManager.RecordRoll("Second Player", 777).Success, "Second bartender's roll should record.");
        Assert(new CsvSyncService(second).Export(mergedPath).Success, "Merged export should succeed.");

        var firstSync = new CsvSyncService(first);
        Assert(firstSync.Import(mergedPath).Success, "First bartender should merge the second export.");
        Assert(
            firstManager.ActiveRound?.PlayerName == "First Player",
            "Import should not replace the local bartender's active player.");
        Assert(first.ActiveSession!.Sales.Count == 2, "Merged night should contain both unique sales.");
        Assert(first.ActiveSession.Rounds.Count == 2, "Merged night should contain both player rounds.");
        Assert(first.ActiveSession.TotalIntake == 1_250, "Merged intake should be recalculated from unique sales.");
        Assert(firstSync.Import(mergedPath).Success, "Importing the same file twice should succeed.");
        Assert(first.ActiveSession.Sales.Count == 2, "Repeated import must not duplicate sales.");
        Assert(
            first.ActiveSession.Rounds.Sum(round => round.Rolls.Count) == 2,
            "Repeated import must not duplicate rolls.");

        var unrelated = new Configuration();
        Assert(new SessionManager(unrelated).StartNight().Success, "Unrelated night should start.");
        Assert(
            !new CsvSyncService(unrelated).Import(seedPath).Success,
            "Import should reject a different active-night ID.");
    }
    finally
    {
        File.Delete(seedPath);
        File.Delete(mergedPath);
    }
}

static void RunClearHistoryTest()
{
    var activeSession = new NightSession { StartingJackpot = 900, EndingJackpot = 900 };
    var configuration = new Configuration
    {
        JackpotBalance = 900,
        ActiveSession = activeSession,
        SessionHistory =
        [
            new NightSession { EndedAt = DateTimeOffset.Now.AddDays(-1) },
            new NightSession { EndedAt = DateTimeOffset.Now.AddDays(-2) },
        ],
    };
    var manager = new SessionManager(configuration);

    var result = manager.ClearHistory();
    Assert(result.Success, "Stored history should clear.");
    Assert(configuration.SessionHistory.Count == 0, "All closed-night history should be removed.");
    Assert(ReferenceEquals(configuration.ActiveSession, activeSession), "Active night must be preserved.");
    Assert(configuration.JackpotBalance == 900, "Current jackpot must be preserved.");
    Assert(!manager.ClearHistory().Success, "Clearing empty history should report that nothing was removed.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
