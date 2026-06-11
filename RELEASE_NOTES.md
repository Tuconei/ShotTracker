# v0.1.0 - Initial Development Release

ShotTracker's first development release establishes the complete venue game
workflow and persistent accounting foundation.

## Included

- Record player gil trades and convert them into purchased rolls.
- Capture `/random` results only for the active participant.
- Configure multiple winning numbers, fixed payouts, jackpot-percentage
  payouts, and rerolls.
- Maintain a growing jackpot with configurable jackpot, house, and dealer
  splits.
- Track roll counters, outcomes, payouts, sales, and nightly totals.
- Persist active sessions, jackpot balance, settings, and prior night history.
- Manually enter rolls for testing or corrections.
- Package a Dalamud-ready release ZIP.

## Validation

- Debug and Release builds pass against Dalamud SDK 15.
- The accounting regression harness covers invalid splits, participant
  filtering, rerolls, overlapping payouts, jackpot caps, and night closeout.

## Development Status

This is an early development release. The core accounting behavior is tested,
but the plugin still needs an in-game smoke test for live trade entry,
localized `/random` payloads, and operator UI flow.
