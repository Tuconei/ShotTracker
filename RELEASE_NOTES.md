# v0.1.3 - Structured Trade Detection

ShotTracker now listens to Dalamud's structured FFXIV log-message stream in
addition to formatted chat messages, fixing silent trade-detection failures.

## Added

- Parse structured `LogMessage` notifications before their formatted chat
  representation.
- Keep formatted chat parsing as a fallback.
- Display recent trade-event diagnostics directly in the pending-payment UI.
- Record structured row IDs, source entities, typed parameters, and formatted
  text in `/xllog`.
- Deduplicate events emitted by both structured and formatted callbacks.
- Log plugin startup so the active ShotTracker build is visible in `/xllog`.

## Validation

- Debug and Release builds pass against Dalamud SDK 15.
- The accounting regression harness passes.

## Current Limitation

Trade-message verification currently recognizes the English client message
format. Other client languages can use the manual fallback until localized
parsers are added.
