# v0.1.2 - Incremental Trade Verification

ShotTracker now accumulates multiple incoming trades toward a requested total,
supporting purchases above FFXIV's per-trade gil limit.

## Added

- Accumulate multiple verified payments from the armed participant.
- Display verified, expected, and remaining gil while payment is pending.
- Accept any requested total that is an exact multiple of the configured shot
  price.
- Select the participant directly from the targeted player character.
- Recognize additional English incoming-gil message forms and log unrecognized
  gil messages for diagnosis.
- Reject overpayments and trades from other players without changing progress.

## Validation

- Debug and Release builds pass against Dalamud SDK 15.
- The accounting regression harness covers capped multi-trade payments,
  arbitrary payment chunks, overpayment rejection, shot-price multiples,
  wrong players, manual fallback, rerolls, jackpot caps, and night closeout.

## Current Limitation

Trade-message verification currently recognizes the English client message
format. Other client languages can use the manual fallback until localized
parsers are added.
