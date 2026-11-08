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
- Fixed: a loaded launcher or upper stage could sit mostly under the VAB floor. It's lifted so its lowest
  point is on the floor, and undo doesn't sink it again.
- Fixed: a launcher saved from another launcher's parts kept the old one's stage names ("Haytor 002 Stage 2")
  when the old one had been deleted first. Saving a launcher now gives the parts its own stage names, and a
  craft that's only that launcher takes its name.
- Launchers that are renamed or deleted are remembered, so craft built on them keep working stage names
  (a renamed launcher's follow its new name).
- A craft built on a launcher that's since been deleted says so: "BUILT ON Haytor 002 · deleted".
- A saved launcher or subassembly file is written again after other mods are told about the save, so
  anything they put on the parts (the Reusability Suite's design id) is in the file too.
- Save with a launcher or upper stage open on its own (loaded, or saved from the editor) asks "Save over
  Rockall 002" or "Save as Rockall 002 II", as it does for a craft's marks. A link saves it as a new craft
  instead. The drop box asks the same when nothing is held.
- The Save panel's "What is it?" list ends with Launcher and Upper stage, which save the craft to the
  Launchers tab with its rating.
- Re-rating a launcher that's open in the editor renames the craft along with it.
- Save has a third choice beside Save over and Save as the next mark: Save as new design, which goes on
  to the usual "What is it?" panel. Launchers and upper stages get it too, in place of the link.
- A family's Rename button is now Rename / move: it can also move the family to another category. Every
  mark moves, keeping its design id, so hangar copies stay with it.
- Fixed: KSP's folder list that slides out from the Save button saved craft straight into a folder, past
  the library, leaving a loose copy outside the family folder. The list is switched off.
- A loose copy of a filed mark (a file with the mark's name outside its family folder) saves like the mark
  itself: over it, as the next mark, or as a new design. The loose copy then goes to Deleted craft.
- A design put into an empty editor as the whole craft (a hangar copy, or a craft merged in) takes the
  design's name ("Gallant I", not "Untitled Space Craft") and saves into its family folder.
- After loading a craft from outside the craft folders (a subassembly, a hangar copy), saves go to the
  building's own folder, not to a family folder left open before.
- Buttons say what they do: Load when the editor is empty and it becomes the craft, Add to craft when it
  goes onto the craft there. ("Pick up" is gone.)
- The hangar copy panel can show a second button beside Load, from the mod that keeps the hangar: the
  Reusability Suite's "Load as stored". BuildsApi version 2 adds `CopyDetail.OpenLabel` and `Open` for it;
  everything in version 1 is unchanged.

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
