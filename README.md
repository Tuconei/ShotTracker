# ShotTracker

ShotTracker is a Dalamud venue-operator plugin for running gil-based `/random`
shot games in FFXIV.

## Features

- Arms an expected participant and total gil amount, accumulates matching
  incoming trades, then credits rolls when the requested total is reached.
- Fills the participant name from the targeted player character.
- Listens only to `RandomNumber` chat messages from the active participant.
- Supports any number of winning-number rules.
- Supports fixed-gil or percentage-of-jackpot payouts.
- Supports winning numbers that grant a free reroll.
- Tracks each roll's counter, result, matched rules, payout, and source.
- Splits every sale between jackpot, house, dealer, and an optional unallocated
  reserve.
- Carries the jackpot balance between nights.
- Shows total intake, payouts, house cut, dealer cut, and sale history.
- Persists an active night and prior night summaries through Dalamud config.

## Accounting model

The operator enters the expected participant and total gil amount before
trading. ShotTracker listens for English-client incoming trade messages and
accumulates payments from the armed participant. This supports totals above the
game's per-trade gil limit. Rolls and accounting are credited once, after the
verified payments reach the requested total. Wrong-player trades and
overpayments remain visible and do not change verification progress.

Manual entry remains available as an explicitly labeled fallback. The ledger
marks each sale as chat-verified or manual.

The trade must be an exact multiple of the configured shot price. On acceptance:

1. `trade / shot price` rolls are added to the active participant.
2. The configured jackpot, house, and dealer percentages are accrued.
3. Any percentage left below 100% is shown as unallocated reserve.

All configured win payouts come from the jackpot. A payout can be a fixed gil
amount or a percentage of the jackpot balance at the time of the roll. Payouts
are capped at the available jackpot, so the ledger never becomes negative.

Ending a night saves its totals to history. The house and dealer figures are the
amounts owed to each party for that night.

## Usage

1. Open settings and configure the shot price, revenue split, jackpot, and win
   rules.
2. Use `/shottracker` and select **Start Night**.
3. Target the participant and select **Use Target**, or enter their visible
   character name manually. Enter the total gil and select
   **Wait for Matching Trade**.
4. Complete one or more in-game trades. ShotTracker displays accumulated and
   remaining gil, then credits the rolls when the total is reached.
5. Have that participant use `/random`. Rolls from other players are ignored.
6. Use manual payment or roll entry only for fallback, corrections, or testing.
7. Select **Close Night** to preserve the final ledger and cuts.

Trade-message verification currently targets the English FFXIV client text
(`Name trades you 100,000 Gil.`). Other client languages should use the manual
fallback until localized message parsing is added.

## Building

Requirements:

- XIVLauncher and Dalamud installed in the default location, or `DALAMUD_HOME`
  set to the Dalamud development directory.
- .NET 10 SDK.

Build with:

```powershell
dotnet build ShotTracker.sln
```

Run the accounting regression harness with:

```powershell
dotnet run --project ShotTracker.Tests
```

The development DLL is written to:

```text
ShotTracker/bin/x64/Debug/ShotTracker.dll
```

Add that DLL under Dalamud Settings > Experimental > Dev Plugin Locations.
