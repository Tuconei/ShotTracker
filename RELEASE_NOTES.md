# v0.1.8 - Reusable Win Actions

ShotTracker now makes it easier to keep announcement settings consistent across
multiple winning rules.

## Added

- Configure a default win-action profile for highlight, bartender echo, message
  templates, and selected chat channels.
- Apply the default win-action profile to every winning rule at once.
- New winning rules inherit the default win-action profile automatically.
- Copy and paste win actions from one winning rule to another.
- Save an individual rule's win actions back as the new default profile.

## Validation

- Debug and Release builds pass against Dalamud SDK 15 with zero warnings.
- Accounting, trade verification, CSV synchronization, winning-range,
  win-action-profile, notification-template, and history-clearing tests pass.

# v0.1.6 - Configurable Wins and Prizes

ShotTracker now supports broader winning rules and customizable actions when a
player wins.

## Added

- Configure inclusive winning ranges such as `0-99` while retaining exact
  single-number rules.
- Select non-gil prizes as a payout type and record the named prize without
  changing jackpot accounting.
- Track external prizes in roll, player, night, history, and CSV statistics.
- Configure each winning rule to highlight its roll in the ledger.
- Send a private bartender echo with a customizable template.
- Send a customizable win message to any combination of Say, Yell, Shout,
  Party, Alliance, Free Company, Novice Network, PvP Team, linkshells, and
  cross-world linkshells.
- Use `{player}`, `{roll}`, `{rule}`, `{payout}`, `{prize}`, and `{award}`
  placeholders in notification templates.

## Preserved

- Target-based participant selection and verified multi-trade payments.
- CSV ledger export, idempotent multi-bartender synchronization, and stable
  record IDs.
- Existing `v0.1.5` configuration and CSV exports remain readable.

## Validation

- Debug and Release builds pass against Dalamud SDK 15 with zero warnings.
- Accounting, trade verification, CSV synchronization, winning-range,
  non-gil-prize, notification-template, and history-clearing tests pass.
