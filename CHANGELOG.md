# Changelog

## Unreleased

- A launcher rated below the payload still shows its hangar copies, which can still be picked, and has a
  Re-rate button.
- The hangar filter on the Launchers tab is "Built copies": it counts ready copies, not only flown ones.
- A launcher or upper stage picked into an empty editor names the craft after itself.
- With nothing in the editor, a launcher or upper stage can be loaded as the craft itself (Load, or a
  double-click), as well as picked up.
- The Save panel deals a new name when the craft still carries a launcher's name.
- Fixed: spent stages never got their names in flight ("Oak 030 Stage 1"). KSP only lets parts with a
  naming priority above 0 name a vessel, and 0.1.0 used -1. Craft tagged by 0.1.0 are fixed the next time
  they're edited.
- A dropped piece with its own probe core or crew pod stays flyable (a probe or ship) under its stage name;
  only pieces without one are debris. KSP's [ and ] keys skip debris.
- A launcher flown on its own launches under the craft's name.
- Launchers and subassemblies have pictures in the library, taken when they're saved from the drop box,
  or the first time an older one is picked up or loaded.
- A craft's panel shows what changed since the mark before it ("+2.3 t · +1,200 funds · +3 parts").

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
