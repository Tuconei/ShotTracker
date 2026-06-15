# v0.1.5 - CSV Ledger Sync

ShotTracker can now export its complete ledger for spreadsheet review and
merge exports from multiple bartenders without double-counting shared records.

## Added

- Exports settings, winning rules, nights, player rounds, sales, and rolls to
  spreadsheet-friendly CSV.
- Imports and merges CSV data using stable record IDs.
- Recalculates intake, cuts, payouts, remaining rolls, and jackpot totals after
  a merge.
- Rejects attempts to combine unrelated active nights.
- Preserves each bartender's locally active player during a merge.
- Adds a confirmed action to clear closed-night history without changing the
  active night or current jackpot.

## Validation

- Release build passes against Dalamud SDK 15 with zero warnings.
- Accounting, trade verification, CSV round-trip, duplicate import, and history
  clearing regression tests pass.
