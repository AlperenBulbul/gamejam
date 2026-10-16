# CLAUDE.md: CIVIL GAME JAM 2026 (Unity)

48-hour game jam. **Development ends Sunday 18 Oct 2026, 18:00 (UTC+3).** Scope small, ship something playable.
Two people use Claude in this repo:
- **Programmer**: builds features.
- **Playtester/debugger**: reproduces bugs from `docs/BUGS.md`, fixes them, polishes game feel.
Two artists (characters/animation, world/environment) drop assets into `Game/Assets/_Project/Art` and `Animations`.

Read `docs/DESIGN.md` first for the current idea, scope and the animation contract.

## Layout
- Unity project: `Game/` (version in `Game/ProjectSettings/ProjectVersion.txt`)
- Our code: `Game/Assets/_Project/Scripts/` (editor-only code in `_Project/Editor/`)
- Main scene: `Game/Assets/_Project/Scenes/Main.unity` (**programmer owns it**)
- Personal test scenes: `Game/Assets/_Project/Scenes/Sandbox/<Name>.unity`
- Builds: Unity menu **Jam > Build Windows (zip)** writes to `/Builds` (git-ignored)

## How to work in this repo
- **Do not hand-edit `.unity`, `.prefab`, `.asset`, or `.meta` YAML** unless explicitly asked. GUID/fileID references break silently. Instead, write the C# and then give the human **exact Inspector steps**: which object, which component, which field to drag what into.
- Use editor scripts (`_Project/Editor`) if a setup step is repetitive.
- Before using input APIs, check `Game/Packages/manifest.json` for `com.unity.inputsystem` and match the project's input handling.
- Keep it jam-simple: plain MonoBehaviours, `[SerializeField] private` fields, one GameManager is fine. No DI frameworks, no premature abstraction.
- Avoid `Find*`/`GetComponent` in `Update`; cache in `Awake`. Null-check serialized references with a clear `Debug.LogError` naming the missing field.
- Tunable numbers (speed, damage, timers) go in serialized fields or a ScriptableObject so the playtester can tweak them without code.
- Every new script: one-line summary comment at the top.
- After finishing a change, list: files changed, Inspector/scene steps needed, how to test it.

## Debugging mode (playtester)
1. Take the bug from `docs/BUGS.md`. Make sure it can be reproduced and understood before changing anything.
2. Find the root cause and make the smallest fix. Don't refactor during the jam.
3. Update the bug's status in `docs/BUGS.md`. Commit as `fix: <bug id> <short description>`.
4. After Sunday 10:00 (feature freeze), only bug fixes and polish. No new features.

## Jam rules that affect code (from the rulebook; breaking them = disqualification)
- **AI declaration (rule 4):** after any meaningful AI contribution, **append a row to `docs/AI_LOG.md`** (category, tool, purpose, where it's used in the game). Do this yourself, every time. Small uses count too.
- **Premade assets (rule 3.3–3.6):** any library/asset/font/sound not made by the team goes in `docs/ASSET_CREDITS.md` with source + license. Assets whose license forbids redistribution (e.g. Unity Asset Store packages) go only in `Game/Assets/ThirdParty_NoRedistribute/` (git-ignored) and are documented, never committed.
- **No secrets (rule 3.7):** never commit passwords, tokens, or API keys.
- **Repo must be public and match the build (rules 3.7, 6.5):** don't suggest making it private; the final ZIP must be built from a pushed commit (`BUILD_INFO.txt` in the ZIP shows the commit).
- Target is **Windows** (desktop browser is allowed as an alternative).

## Commits
Small, often, pushed. Prefixes: `feat:`, `fix:`, `art:`, `anim:`, `audio:`, `polish:`, `docs:`. Always include the `.meta` file with its asset.
