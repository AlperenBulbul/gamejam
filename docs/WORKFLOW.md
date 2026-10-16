# Team workflow (48h)

## Roles & what each person "owns"

Merge conflicts in Unity scenes are the #1 way jams lose hours. The rule is **one owner per file**.

| Person | Owns (only they edit) | Delivers to others as |
|---|---|---|
| Programmer (+Claude) | `Scripts/`, `Scenes/Main.unity`, `ProjectSettings/` | Scripts, wiring in Main |
| Playtester (+Claude) | `docs/BUGS.md`, own Sandbox scene; fixes in `Scripts/` **after telling the programmer which file** | Bug fixes, tuning values, builds |
| Character artist | `Art/Characters/`, `Animations/`, `Prefabs/Characters/`, `ArtSource/Characters/` | Character prefabs + Animator Controllers |
| World artist | `Art/Environment/`, `Art/UI/`, `Prefabs/Environment/`, `ArtSource/Environment/` | Environment prefabs / tile palettes |

- **Need to touch `Main.unity`?** Say "taking Main" in the team chat, edit, commit, push, then say "Main free".
- Everyone tests in their **own** scene: `Scenes/Sandbox/<YourName>.unity`. Drag prefabs from there into Main only when the Main owner says go.
- Artists build **prefabs**, not scene objects. The programmer drops the prefab into Main. Updating the prefab later updates Main automatically, with no conflict.
- Binary files (`.png`, `.psd`, `.wav`, `.fbx`) can't be merged. Whoever owns the file is the only one who saves it.

## Daily git loop (everyone, every 30–60 min)
1. **Save** in Unity (Ctrl+S) and in your art tool.
2. **Pull** first (GitHub Desktop: *Fetch* then *Pull*).
3. **Commit** small: one feature/asset per commit. Always include the `.meta` files.
4. **Push**.

Everyone works on `main`. There's no time for long branches. The programmer may use a short branch for something risky and merge it within a few hours.

**Golden rules**
- Move/rename/delete assets **inside Unity**, never in Explorer, or the `.meta` files and their references break.
- Never commit only a `.meta` without its file (or the reverse).
- Files > 50 MB are blocked by the commit hook. Compress them or keep them out of the repo.
- Same Unity version for everyone (see README).
- Artists: [GitHub Desktop](https://desktop.github.com/) is the easiest client. Clone, then run `setup.bat` once.

## Art specs (fill in once the idea is picked)
- 2D or 3D: `[ ]`
- Pixels Per Unit / scale reference: `[e.g. 32 PPU; character = 1 unit tall]`
- Max texture size: 2048 · Format: PNG (transparent)
- Naming: `chr_<name>_<action>_<frame>.png`, `env_<thing>_<variant>.png`, `ui_<thing>.png`, `sfx_<thing>.wav`
- Pivots: characters at the feet (bottom center)
- Raw/working files (`.psd`, `.aseprite`, `.blend`, `.kra`) go in `/ArtSource/...`, and exported files go in `Game/Assets/_Project/Art/...`
- **Using any AI tool or premade asset? Log it immediately** in `docs/AI_LOG.md` / `docs/ASSET_CREDITS.md`.

## Timeline (all UTC+3)

| When | Milestone |
|---|---|
| **Fri 18:00** | Theme announced. Brainstorm 45 min max, then pick one idea and fill in `DESIGN.md` |
| Fri 19:00 | Art style test (1 character, 1 tile/prop). Programmer starts greybox |
| **Fri 23:00** | Core mechanic playable with placeholder cubes |
| **Sat 12:00** | Core loop complete (start → play → win/lose → restart). **First Windows build + first playtest** |
| Sat 18:00 | Real art + animations in. Second playtest |
| Sat 24:00 | Content complete: all levels/screens, sound in |
| **Sun 10:00** | **Feature freeze.** Only bugs, polish, juice |
| **Sun 12:00** | Organizers' Git help deadline. Repo must be public and pushed by now (check from a logged-out browser) |
| Sun 14:00 | Final build tested on a **different** PC. Record ≤1 min trailer (MP4) |
| **Sun 16:00** | **Submit** (2h buffer) and check the receipt |
| Sun 18:00 | Hard deadline: no new uploads can start after this |
