# ShotTracker

ShotTracker is a Dalamud venue-operator plugin for running gil-based `/random`
shot games in FFXIV.

## Features

- Arms an expected participant and gil amount, then credits rolls after a
  matching incoming trade system message.
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

The operator enters the expected participant and gil amount before the trade.
ShotTracker then listens for the English-client incoming trade system message
and credits rolls only when both the normalized character name and exact gil
amount match. Mismatches remain visible and do not grant rolls.

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
3. Enter the participant's visible character name and expected gil, then select
   **Wait for Matching Trade**.
4. Complete the in-game trade. ShotTracker credits the rolls after the matching
   incoming trade system message appears.
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
