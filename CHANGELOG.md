# Changelog

## 0.1.0 (2026-11-07)

First release, a pre-release.

- One library window in place of the stock craft loader and subassembly list, opened by the editor's
  Load button. It has Craft, Launchers and Subassemblies tabs.
- Families and marks (Thunderbolt I, II, III), filed as real folders: `Ships/<VAB|SPH>/<Category>/<Family>`.
  There are 22 build categories.
- The Save button asks whether to save over the open mark or as the next one. A new design gets a Save
  panel with its category, family and a dealt name.
- 1,250 themed names in 25 lists, dealt in a shuffled order for each save, never repeating a name in use.
  Players and mods can add more with `VESSEL_NAMES` nodes.
- Launchers saved as subassemblies, rated by tons to orbit and widest payload. The Launchers tab lists
  the ones that fit what's in the editor, and a click picks one up on the cursor.
- Spent stages from a picked launcher are named after it ("Oak 030 Stage 1", "Oak 030 Booster").
- Deleting moves files to the save's `Deleted craft` folder.
- `BuildsApi` for other mods: stable design ids, events when designs are placed or saved, and hangar
  copies shown in the library window.
