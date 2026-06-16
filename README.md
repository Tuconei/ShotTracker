# ShotTracker

ShotTracker is a Dalamud plugin for Final Fantasy XIV venue operators who run
gil-based `/random` shot games. It tracks payments, rolls, jackpot accounting,
win rules, payouts, nightly summaries, and multi-bartender CSV sync from one
in-game window.

Open the plugin with:

```text
/shottracker
```

## What It Does

- Verifies incoming gil trades before crediting rolls.
- Supports large purchases across multiple trades.
- Tracks the active participant and listens only to that player's `/random`
  rolls.
- Calculates rolls from a configurable gil-per-shot price.
- Splits every sale between jackpot, house, dealer, and optional reserve.
- Carries the jackpot between nights.
- Supports exact winning numbers and inclusive winning ranges such as `0-99`.
- Supports fixed gil payouts, percentage-of-jackpot payouts, and named non-gil
  prizes.
- Caps gil payouts to the current jackpot so balances do not go negative.
- Supports reroll awards.
- Highlights winning ledger rows.
- Sends configurable bartender echoes and chat-channel win messages.
- Exports and imports CSV ledgers for spreadsheet review and bartender sync.
- Saves active nights and closed-night history through Dalamud configuration.

## Main Workflow

1. Open `/shottracker`.
2. Click **Settings** and configure pricing, split percentages, jackpot, and
   win rules.
3. Click **Start Night**.
4. Target the participant and click **Use Target**, or type their visible
   character name manually.
5. Enter the total gil they are buying in for.
6. Click **Wait for Matching Trade**.
7. Complete the in-game trade or trades.
8. Have the participant roll with `/random`.
9. Continue until the participant has no rolls remaining, or click **End
   Player**.
10. Click **Close Night** when the event is finished.

Manual payment and manual roll buttons are available for corrections, testing,
or cases where chat verification is not usable.

## Settings

### Pricing and Split

The settings window controls:

- **Gil per shot**: the amount of gil required for one roll.
- **Jackpot %**: the share of each sale added to the jackpot.
- **House %**: the share owed to the house.
- **Dealer %**: the share owed to the dealer.
- **Current jackpot**: the carried jackpot balance between nights.

The jackpot, house, and dealer percentages must total 100% or less. Any
remainder is tracked as unallocated reserve. For example, a 50/40/10 split
fully allocates each sale, while a 50/35/10 split leaves 5% in reserve.

The current jackpot can only be manually edited when no night is active.

### Winning Rules

Each winning rule can define:

- A label shown in the ledger.
- An exact winning number such as `777`.
- An inclusive winning range such as `0-99`.
- A payout type.
- Whether the rule grants a reroll.
- Whether matching rolls are highlighted.
- Optional echo and public chat messages.

Supported payout types:

- **Fixed gil**: pays a configured gil amount from the jackpot.
- **Jackpot percentage**: pays a percentage of the jackpot at the time of the
  roll.
- **Non-gil prize**: records a named prize, such as a minion or item, without
  changing jackpot accounting.

If multiple rules match one roll, ShotTracker combines their gil payouts and
records all matching prize names. Gil payouts are capped to the jackpot balance
available at the time of the roll.

## Trade Verification

When a trade verification is armed, ShotTracker watches incoming gil messages
for the expected participant and expected total.

This is useful when a participant buys more than the per-trade gil limit:

```text
Expected total: 2,000,000 gil
Trade 1:        1,000,000 gil
Trade 2:        1,000,000 gil
Result:         rolls are credited after the full amount is verified
```

Wrong-player trades and overpayments are shown in the window but do not advance
verification. The trade amount must be a positive multiple of the configured
shot price.

Verified sales are marked as **Chat** in the ledger. Manual fallback entries are
marked as **Manual**.

Trade parsing currently targets English client text, including messages like:

```text
Player Name trades you 100,000 gil.
You receive 100,000 gil from Player Name.
Player Name gives you 100,000 gil.
```

Use manual entry if your client language or chat format is not recognized.

## Roll Tracking

ShotTracker listens for `RandomNumber` chat messages while a player is active.
Only rolls from the active participant are accepted; rolls from other players
are ignored.

For every accepted roll, the ledger records:

- Roll counter.
- Roll value.
- Matched outcome labels.
- Gil payout.
- External prizes.
- Whether a reroll was granted.
- Whether the roll came from chat or manual entry.

When the participant's remaining rolls reach zero, their round ends
automatically.

## Win Notifications

Each win rule can send a private bartender echo and/or public messages to
selected chat channels. Supported channels include Say, Yell, Shout, Party,
Alliance, Free Company, Novice Network, PvP Team, linkshells, and cross-world
linkshells.

Message templates support these placeholders:

```text
{player}  participant name
{roll}    roll value
{rule}    matching rule label
{payout}  gil payout text
{prize}   non-gil prize text
{award}   combined payout/prize summary
```

Example:

```text
Congratulations {player}! You rolled {roll} and won {award}!
```

Messages are sanitized to a single line and limited before being sent.

## CSV Export and Sync

Expand **CSV export and sync** in the main window to export or import a ledger.
By default, exports are written to:

```text
Documents\ShotTracker
```

The CSV includes:

- Current settings.
- Winning rules.
- Active and closed nights.
- Player rounds.
- Sales.
- Rolls.
- Stable record IDs used for merging.

Imports are idempotent: importing the same file multiple times does not
duplicate sales or rolls. ShotTracker recalculates totals after merging.

### Multi-Bartender Sync

For multiple bartenders:

1. One operator starts the night.
2. That operator exports the active night.
3. Every other bartender imports that file before recording activity.
4. Bartenders exchange exports during the night.
5. Each bartender uses **Import and Merge CSV** to pick up new records.

ShotTracker rejects a CSV that contains a different active-night ID, which
prevents accidentally combining unrelated pots.

## Night History

Closing a night saves its final totals into history and preserves the ending
jackpot as the carried jackpot. The history view shows recent closed nights with
intake, payouts, external prizes, house cut, dealer cut, and ending jackpot.

**Clear Stored History** removes closed-night history after confirmation. It
does not clear the active night or current jackpot. Export first if you may need
the history later.

## Accounting Model

When a sale is accepted:

1. `trade amount / shot price` rolls are added to the active participant.
2. Jackpot, house, dealer, and reserve amounts are calculated from the sale.
3. Jackpot contribution is added to the carried jackpot.
4. Rolls deduct winning gil payouts from the jackpot.
5. Non-gil prizes are counted separately and do not affect the jackpot.

House and dealer totals represent what is owed to each party for the active or
closed night.

## Building From Source

Requirements:

- XIVLauncher and Dalamud installed in the default location, or `DALAMUD_HOME`
  set to the Dalamud development directory.
- .NET 10 SDK.

Build:

```powershell
dotnet build ShotTracker.sln
```

Run the accounting regression harness:

```powershell
dotnet run --project ShotTracker.Tests
```

The development DLL is written to:

```text
ShotTracker/bin/x64/Debug/ShotTracker.dll
```

Add that DLL in Dalamud Settings > Experimental > Dev Plugin Locations.

## License

ShotTracker is distributed under the license in [LICENSE.md](LICENSE.md).
