# v0.1.1 - Trade Verification

ShotTracker now verifies incoming gil payments through FFXIV's trade system
message before granting purchased rolls.

## Added

- Arm an expected participant and exact gil amount before accepting payment.
- Credit rolls only after a matching incoming trade system message.
- Reject and display trades with the wrong player or amount.
- Mark ledger sales as chat-verified or manually entered.
- Keep an explicit manual fallback for unsupported client languages or
  corrections.

## Validation

- Debug and Release builds pass against Dalamud SDK 15.
- The accounting regression harness covers exact payment matching, wrong
  players, wrong amounts, comma-formatted gil, manual fallback, invalid splits,
  rerolls, jackpot caps, and night closeout.

## Current Limitation

Trade-message verification currently recognizes the English client message
format. Other client languages can use the manual fallback until localized
parsers are added.
