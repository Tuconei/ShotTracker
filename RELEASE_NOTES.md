# v0.1.4 - Production Trade Verification

Trade verification is confirmed working through Dalamud's structured FFXIV
log-message stream. Temporary live diagnostics have been removed for normal
venue use.

## Changed

- Retains structured `LogMessage` trade detection and formatted-chat fallback.
- Retains duplicate-event protection and incremental payment tracking.
- Removes the temporary trade diagnostics from the plugin window.
- Removes per-event trade logging from `/xllog`.
- Keeps one startup entry so the loaded ShotTracker version remains visible.

## Validation

- Debug and Release builds pass against Dalamud SDK 15.
- The accounting regression harness passes.

## Current Limitation

Trade-message verification currently recognizes the English client message
format. Other client languages can use the manual fallback until localized
parsers are added.
