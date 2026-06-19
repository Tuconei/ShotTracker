# v0.1.10 - Exhaustion Messages and Payout Funding

ShotTracker now gives bartenders clearer announcement controls and more control
over whether fixed gil awards affect the jackpot.

## Added

- Configure a paid-rolls-exhausted message that can send a bartender echo and
  selected public chat-channel messages when a participant's purchased rolls
  reach zero.
- Send win notifications before paid-rolls-exhausted notifications when a final
  roll wins.
- Queue notification commands with a short delay between sends to avoid in-game
  chat throttling or swallowed messages.
- Choose per fixed-gil winning rule whether the payout deducts from the jackpot
  or is paid by the house without reducing jackpot balance.
- Track jackpot-funded payout amounts separately in roll CSV data so synced
  ledgers reconstruct jackpot totals correctly.

## Changed

- The default win actions and paid-rolls-exhausted message settings are
  collapsible sections to keep Settings compact.
- Fixed gil payouts still count in player and night payout totals even when
  they are configured as house-funded.

## Validation

- Debug and Release builds pass against Dalamud SDK 15 with zero warnings.
- Accounting, trade verification, CSV synchronization, winning-range,
  house-funded fixed payout, win-action-profile, venue-profile,
  notification-template, paid-rolls-exhausted, and history-clearing tests pass.

# v0.1.9 - Venue Profiles

ShotTracker now supports reusable venue profiles for bartenders who work across
multiple venues with different game setups.

## Added

- Create named venue profiles from the current settings.
- Load saved venue profiles for pricing, split percentages, jackpot balance,
  default win actions, and winning rules.
- Overwrite a saved venue profile from the current setup.
- Rename or delete saved venue profiles from Settings.
- Prevent loading a different venue profile while a night is active.

## Validation

- Debug and Release builds pass against Dalamud SDK 15 with zero warnings.
- Accounting, trade verification, CSV synchronization, winning-range,
  win-action-profile, venue-profile, notification-template, and history-clearing
  tests pass.

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
