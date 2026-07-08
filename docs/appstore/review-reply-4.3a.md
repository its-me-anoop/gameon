# Resolution Center reply — Guideline 4.3(a), submission fd880674-4641-4e42-9d36-843002440e91

Context: v1.0 (build 8) was rejected 2026-07-08 under 4.3(a) — Design — Spam
("similar binary, metadata, and/or concept as apps submitted by other
developers"). The plan is NOT to argue alone: v1.3 (build 11) adds visibly
distinctive content (Math Pop learning mode + five themes), and this reply
accompanies the new binary. Paste the text below into Resolution Center when
the 1.3 build is attached to the submission.

---

Hello, and thank you for the review.

We understand the concern — the merge-puzzle category is crowded. We'd like
to explain why Gravitile is an original work, and what we've added in the new
build (1.3) to make its distinctiveness unmistakable.

**Original engine and assets.** Gravitile is not a template or repackaged
code. Every line was written for this app: a deterministic engine in a
custom Swift package where **gravity rotates 90° after every move** and the
whole board tumbles, chaining cascade merges — a mechanic we have not found
in any other App Store title. The sound set is synthesized by our own tool,
the color palettes are hand-derived in OKLCH, and the UI is entirely custom
SwiftUI. We're happy to provide the full git history, the balance-simulation
reports, and our design documents as evidence of original authorship.

**What's new in 1.3 — content no similar app has:**

1. **Math Pop, an arithmetic-learning mode** (Home → "Math Pop"). Tiles
   carry small numbers, and two tiles merge only when they **add up to the
   stage target** ("Make 5" → "Make 10" → … → "Make 16"). Each completed
   bond pops off the board showing its equation ("3 + 7 = 10"), and tiles
   are colored using the **Cuisenaire rod system** used in real classrooms —
   so children practice number bonds, the foundation of early arithmetic,
   while playing the same tumbling-gravity game. This mode is free,
   original, and designed with young players in mind.

2. **Five hand-tuned color themes** (Settings → Theme): Ember, Tidepool,
   Meadow, Aurora and Sorbet — including two full light themes — each a
   complete palette for board, tiles and chrome.

3. Alongside the modes already in the binary you reviewed: the rotating-
   gravity cascade system, a globally-seeded Daily with streaks, Zen and
   Sprint, the Stasis hold powerup, iced "boulder" tiles, a standalone
   Apple Watch game, rendered share cards, and detailed statistics.

**To verify quickly on your device:** launch the app → tap "Math Pop" (the
card with the NEW badge) → swipe: tiles that sum to the target pop with
their equation; the compass shows gravity turning after every swipe. Then
Settings → Theme to switch between the five palettes live.

We're an independent developer and this game is our own design from first
principles. If any specific similarity to another app concerns the review
team, we'd genuinely appreciate a pointer to it so we can address it
directly.

Thank you for taking a second look at build 11 (version 1.3).

---

## Submission checklist (ASC, manual — do not automate without approval)

1. Wait for build 11 to finish processing on TestFlight (upload via the
   `release.yml` GitHub workflow — never archive locally on the beta Mac).
2. On the rejected 1.0 submission: update the version string to 1.3 (or
   create the 1.3 appStoreVersion and move the submission), attach build 11.
3. Push refreshed metadata from docs/appstore/listing.md (description,
   promo text, keywords, review notes) — note Tools/publish_metadata.py is
   still v1.0-hardcoded; update or push by hand.
4. New screenshots: include one of Math Pop (Sorbet theme reads best) and
   one of the theme picker. `GRAVITILE_ROUTE=math` + `GRAVITILE_THEME=sorbet`
   debug env vars stage them quickly.
5. Paste the reply above into Resolution Center, then resubmit.
