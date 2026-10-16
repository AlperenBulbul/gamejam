# Design doc (one page, keep it current)

## Theme
**[announced Fri 18:00]**

## Pitch (one sentence)
[You are a ___ who must ___ before ___.]

## How it uses the theme
[Theme is worth 25/100. It should be in the **mechanic**, not just the art.]

## Core loop
1. [player does X]
2. [game responds with Y]
3. [pressure/goal Z]. Win when ___, lose when ___

## Scope
**Must have (by Sat 12:00):**
- [ ]
- [ ]

**Should have (by Sat 24:00):**
- [ ]

**Nice to have (only if ahead):**
- [ ]

**Cut list** (things we decided not to do):
-

## Judging (100 pts), what we're optimizing
| Criterion | Pts | Our plan |
|---|---|---|
| Playability | 25 | Clear controls, tutorial in first 10 s, no softlocks |
| Theme fit | 25 | |
| Creativity | 20 | |
| Visual/audio cohesion | 15 | One consistent style + SFX on every action |
| Technical solidity | 15 | No crashes; test build on another PC |

Presentation: 3 minutes total including the ≤1 min video, no Q&A. Jury plays during it.

## World / setting
[Place, mood, palette (3–5 hex colors), reference images]

## Characters
| Character | Role | Notes |
|---|---|---|
| | Player | |

## Animation contract (artists ⇄ programmer)
Agree on these names **before** making animations. The code will use exactly these strings.

| Character | Animator states | Parameters (type) |
|---|---|---|
| Player | Idle, Run, Jump, Hurt, Die | `Speed` (float), `IsGrounded` (bool), `Hurt` (trigger), `Dead` (trigger) |
| | | |

## Audio list
| Sound | Trigger | Status |
|---|---|---|
| | | |
