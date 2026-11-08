# Phil's Builds: design

> Written by Claude (an AI). Phil designs and play-tests; he isn't a programmer.
> Called "Phil's Vessel Naming" until 2026-11-07, when Phil renamed it Phil's Builds: it's about the builds
> themselves (saving, sorting, launchers), not only their names.

A KSP 1.12 mod for naming, grouping and finding craft and subassemblies, to help roleplay a space program:
families with marks (Thunderbolt I, II, III), launchers rated by payload, themed name suggestions, and
one library window in place of the stock craft loader and subassembly list.

## Agreed with Phil (2026-11-06)

1. **Families and marks.** A family (Oak) has marks (Oak I, II, III, like PIP's versions) and may have
   variants (Oak 030 / 060 / 100: the same rocket at different sizes). Older marks are retired out of the
   way but never deleted.
2. **Launcher library.** Launchers are subassemblies rated by payload tons to orbit and payload width.
   "030" is Phil's shorthand for 30 t to orbit, and launcher names carry that code. Ratings are typed in by
   hand: Phil tests with NRAP test weights and reads the Δv readout.
3. **Picking a launcher.** The library lists only launchers that can lift what's in the editor. Clicking one
   picks it up on the cursor, like a stock subassembly, and you attach it yourself.
4. **Name lists.** Themed suggestions per build category, dealt in random order, skipping names already
   used in the save. You can always type your own. 22 categories (below), about 50 names each, in
   `GameData/PhilsBuilds/Names/*.cfg` as `VESSEL_NAMES` nodes. Players and mods add names by adding
   another node with the same `category`.
5. **One window, PIP's look.** It replaces both the stock craft loader and the subassembly UI. The yellow
   Load button opens it.
6. **Spent-stage names: try it** and see if we like it. Parts that came from a launcher name the pieces that
   fall off ("Cypress 010 Booster") instead of "<ship> Debris". Name only: they stay debris.

**Not doing:** anything on the green Launch button (no auto-attach); automatic payload rating from NRAP
flights ("bloat"); save-wide name themes (a big enough list would need an AI).

**Parked:** flight numbers at launch ("Evergreen 4"). Look at it later.

## Categories

Each category has its own name list, a library group and a stock vessel type (the map icon).

| Group | Category | Covers | Theme |
|---|---|---|---|
| Launch | Launchers | Rockets that lift payloads, rated in tons | Giants and mountains |
| Launch | Upper stages & tugs | Kick stages, transfer stages, orbital tugs | Beasts of burden and guides |
| Crewed | Capsules | Crew to orbit and back | Heroes and legendary voyagers |
| Crewed | Landers | Crewed landers | Birds and insects |
| Crewed | Deep-space ships | Interplanetary motherships | Naval virtues |
| Crewed | Rescue craft | Rescue and recovery | Guardians |
| Uncrewed | Probes | Orbiters and flybys | Explorers and navigators |
| Uncrewed | Science landers | Landers that sit still and measure | Watchers and lookouts |
| Uncrewed | Relays & comsats | Comms networks | Messengers and heralds |
| Uncrewed | Survey sats | Mapping and resource scanners | Keen eyes and mapmakers |
| Uncrewed | Tankers & depots | Fuel tankers, drones, depots | Springs, rivers and caravans |
| Uncrewed | Mining & ISRU | Drills, refineries | Diggers and prospectors |
| Stations & surface | Stations | Orbital stations | Harbors and harmony |
| Stations & surface | Bases | Surface outposts | Frontier settlements |
| Stations & surface | Rovers | Ground vehicles | Hardy land animals |
| Stations & surface | Hoppers & test vehicles | Prototypes, hop tests (get an "X-n" prefix) | Jumpers |
| Air & sea | Spaceplanes | Orbit-capable planes | Winds |
| Air & sea | Jets & aircraft | Atmospheric planes | Birds of prey |
| Air & sea | Cargo planes | Transports | Big carrying birds |
| Air & sea | Bombers & droppers | Bombers, seismic impactors, drop craft | Storms |
| Air & sea | Rotorcraft & VTOL | Helicopters, hover craft | Hovering creatures |
| Air & sea | Boats & subs | Seaplanes, boats, submarines | Sea creatures |

No name list:
- **Modules:** pieces of a bigger family take the family name plus a role ("DESU Drive", "Evergreen Lab").
  Roles: Core, Drive, Hab, Lab, Lander, Service, Tank, Pod, Node.
- **Building blocks:** utility subassemblies ("Control 1.25 Top", "Launch Tower") keep a plain
  function-and-size name.

## Proposed in mockup v1 (not agreed yet)

Mockup v1 has six screens: the Craft, Launchers and Subassemblies tabs, Save a new design, Save the next
mark, and Spent-stage names. Its example data uses Phil's own craft names.

- Window tabs: Craft, Launchers, Subassemblies. A category column on the left, families in the middle, the
  selected item's details on the right.
- A family holds members (its modules, by role), and each member has marks.
- Saving: the first save of a design that isn't in a family yet opens a Save panel (category, family, name
  with a "deal another" button, role, numbering style). Later saves overwrite the current mark as stock does.
  "Save as next mark" lives in the Save panel.
- Files kept as real folders: `Ships/VAB/<Category>/<Family>/<Name>.craft`.
- Launcher classes from the rating: Light up to 5 t, Medium 5-20 t, Heavy 20-50 t, Super-heavy 50 t and up.
- Spent stages: radial pieces are "Booster", in-line pieces "Stage 1, 2…", fairing halves "Fairing".

## Built: 0.1 (2026-11-06, not yet tried in game)

Phil approved the mockup ("Yes build this bad boy out"). What the first build does, and where it differs
from the mockup:

- **Load button** opens the library. The stock Subassemblies category in the parts list is hidden.
- **Save button.** For a design in a family folder it saves over the open mark, the stock way (stock save
  writes into the folder the craft was loaded from). For a design not in a family it opens the Save panel,
  which also has "Save without filing". Ctrl+S is left stock.
- **Save… in the library** opens Save the next mark for a filed design.
- **Retiring is automatic:** older marks show as retired and still load. There's no "Retire this mark" link.
- **Delete** moves the file to `saves/<save>/Deleted craft/` so it can be got back.
- **Filing** an unfiled craft moves it into its family folder, renames it inside and copies its thumbnail.
  Saving a loaded unfiled craft into a family moves the old unfiled copy to Deleted craft.
- **Launcher re-rating:** a launcher whose name carries its code ("Oak 030") is renamed when its rating
  changes ("Oak 060").
- **Old-style subassemblies** like "Launch - Oak 030" are read as the Oak family, 30 t, when filed.
- **Spent stages** use per-part vessel naming:
  - Launcher parts are named at priority -1 as Debris.
  - The payload gets the ship name at priority 1, with its category's vessel type, so the map icon matches.
- **Not built yet:**
  - Fairing halves keep the stock "<ship> Debris" name. They're made in flight, so naming them needs a
    flight-scene hook.
  - Flight numbers are still parked.

Checks: `Tools/Test-Core.ps1` runs 67 checks on the naming rules; `Tools/Check-Names.ps1` checks the lists.

## Changed after Phil's first play-test (2026-11-06)

- **No member level** (Phil: "most things I'll be building are singular"). A family is one design line
  and its marks: Category, then Family, then Mark. A program built in pieces is several families sharing
  a name prefix (DESU, DESU Drive, DESU Habitation; Evergreen, Evergreen Lab), and sorting A to Z keeps
  them together. A family is the craft name without its mark, whatever its folder is called.
- **Save always asks** for a craft in a family: save over the open mark (picked first, so Save then Enter
  works) or save as the next one. The next mark counts up from the newest saved, not the one open.
- **The Save panel uses the name in KSP's name box.** If a family of that name exists, it adds to it.
  Only a craft still called "Untitled Space Craft" gets a dealt name.
- **Subassemblies:** Module and Building block are one kind, "Module or block", with a name and a group
  (a program like DESU, or a job like Control). Launchers and upper stages keep their ratings and marks.
- **The drop box** saves the whole craft when nothing is held.

## Changed after Phil's play-test with the suite (2026-11-07, built overnight for Phil to review)

Phil played the D: test copy with the Reusability Suite; its session wrote up what he hit in Phil's Builds.
Phil let both sessions build overnight without his sign-off ("Build freely"), so these await his review.
- **A launcher rated under the payload still shows its hangar copies.** Phil filed Kopje 005 at 5 t, built
  one, then opened the Launchers tab on a 5.5 t payload. Kopje was listed as too small, with no rows, and he
  read it as "the built Kopje wasn't recognised". Now too-small launchers keep their copy rows and hangar
  note, and the copies can still be picked: the rating is the player's own estimate, not a hard limit. The
  list heading says "Too small or too narrow for this payload", the line reads "Rated 5.0 t. This payload is
  5.5 t.", and a **Re-rate** button opens its rating.
- **The hangar filter is "Built copies"**, not "With flown copies": it lists ready, never-flown copies too.
- **A launcher picked into an empty editor names the craft after itself** ("Kopje 005", not "Untitled Space
  Craft"), so the VAB's name box matches. Only a default name is replaced. The same goes for a hangar copy.
- **Load for launchers and upper stages.** With nothing in the editor, the launcher's panel offers Load
  (and Pick up beside it). Load opens the launcher as the whole craft, placed rather than held on the
  cursor, named after itself, with its stage names. Saves go to the facility's own folder until it's
  filed. Double-clicking a launcher in an empty editor loads it too.
- **The Save panel deals a new name** when the name box holds a launcher's or upper stage's name, since a
  payload built on a loaded launcher is a new design.
- **Fixed: spent-stage names never worked in flight.** KSP names a vessel after its highest-priority named
  part, but only counts priorities above 0 (`VesselNaming.FindPriorityNamePart`, used by `Part.decouple`
  and at launch). 0.1.0 put launcher parts at -1, so dropped stages got KSP's own "<craft> Debris". Now:
  - stage labels at priority 1, the payload's name at 3;
  - a launcher with no payload has its top part carry the craft's name at 2, or it would launch named
    after its own top stage;
  - craft tagged at the old values are upgraded the next time they're edited.
- **What counts as the payload** (after an independent review found clamps taking the craft's name):
  - With the payload built first, it's everything joined to the craft's root without crossing the launcher.
  - With the launcher placed first, it's whatever is built on the launcher's top piece.
  - Clamps, fins or boosters added to a lower stage are neither: they drop with their stage and don't count
    toward the payload's weight.
  - Only stage names of launchers in this save's library count, so names a player set by hand are left alone.
- **A dropped piece with a probe core or crew pod stays flyable.** It keeps its stage name but flies as a
  probe or ship, not debris, because KSP's [ and ] keys skip debris and Phil puts probe cores on first stages
  to land them. Suggested by the suite's session; pieces without one are still debris.
- **Pictures of launchers and subassemblies** (new, not asked for). KSP takes no thumbnails of
  subassemblies, so the Launchers and Subassemblies tabs showed a generic glyph. Phil's Builds takes its own
  with KSP's craft-thumbnail camera. It does this when one is saved from the drop box, and the first time an
  older one is picked up or loaded. The rest of the editor is hidden for that one render. The pictures sit
  in `<KSP>/thumbs` as `<save>_Subassemblies_<name>.png`, and follow renames and deletes.
- **What changed since the last mark** (new, not asked for). A craft's panel shows a line under its stats,
  like "SINCE MARK I: +2.3 t · +1,200 funds · +3 parts · +1 stage", so a family reads like PIP's
  iterations. Figures that didn't change are left out, and the line is hidden if none did.

## Fixed after Phil's play-test 2 (2026-11-08, tested in game: 127 checks, 0 failures)

Found by Phil with Argo I, Haytor 002 and Rockall 002; written up by the suite's session.
- **Load put a launcher underground.** A subassembly file keeps the parts where the cursor held them when
  it was saved: Haytor 002's root sat at 4.5 m with the rest hanging below the floor. KSP's editor load
  doesn't move a craft. After Load, Phil's Builds lifts the parts so the lowest model point is on the floor
  (the bottom of KSP's `EditorBounds.constructionBounds`: 0 m in the VAB), and redoes KSP's undo step
  (`ResetBackup`) so undo doesn't sink it again or lose the stage names. It only lifts, never lowers.
  Launch clamps don't count: in the editor `LaunchClamp.Update` stretches each tower down to y = 0 every
  frame, and just after a load they haven't stretched yet (the first in-game test failed on that).
- **A deleted launcher's stage names stayed on its parts.** Phil deleted Haytor 002 (07:35), then saved
  its reworked parts as Rockall 002 (07:37). With Haytor gone, "Haytor 002 Stage 2" no longer looked like
  a name this mod writes, so it wasn't stripped. Now:
  - Saving a launcher or upper stage strips any stage-shaped name at a stage's priority, whichever
    launcher wrote it (the parts get fresh names whenever it's picked up or loaded anyway).
  - The parts just saved as a launcher get its stage names at once. If they're the whole craft, and the
    craft is still named after a launcher (or not named), it takes the new launcher's name.
  - The library remembers launchers that were renamed or deleted (`FORMER_LAUNCHER` in PhilsBuilds.cfg).
    Their stage names still count as this mod's, so a craft built on one keeps relabelling as it's edited.
    A renamed launcher's stage names follow its new name.
- **"Built on" named a deleted launcher.** The note is kept (it's still what the craft was built on), and
  shown as "Haytor 002 · deleted", greyed, in the craft's panel, its row and the Save panel.
- **Saved launcher files kept the old design id.** `DesignSaved` fires after the file is written, so the
  suite's stamps reached only the live parts. A subassembly save now writes the file a second time after
  `DesignSaved` (only when something listens), from the same parts in the same order. The suite says craft
  files don't need this: it never trusts design ids in a file, and re-stamps on every placement.
- **Not built yet: saving over a launcher** (needs Phil's OK on a mockup). The Save button with a lone
  launcher open would ask "Save over Rockall 002" or "Save as Rockall 002 II", like a craft's marks. The
  Save panel's "What is it?" list would get Launcher and Upper stage, with the rating there.

## Banked ideas (Phil's play-test thoughts, not decided)

- **Merge holds a craft by its root part** (2026-11-06). A saved capsule with a heat shield below and a
  parachute on top has no free attach node on its root, so a merged copy can't be attached.
  - Stock workaround: drop it loose, use the Re-root tool on the heat shield, then pick it up again.
  - Possible fix: Merge could offer to hold it by its bottom part and re-root by itself, the way the
    Re-root tool does.
  - Phil isn't sure it's worth it. Banked until he's shared more thoughts.

## Hooks for Phil's Reusability Suite (built 2026-11-07, not yet tried in game)

Phil approved building them ("Yes, build them", in the suite's session). The code is in `src/Api.cs`.
Where the build differs from the plan below:
- `Saved.NewMark` became `IsNewFile`: true when the save wrote a new design file (a new design or a new
  mark), false when it saved over one.
- `PlaceHow` has a fourth value, `Copy`, for a hangar copy, and `Placed.CopyId` names the row picked.
- `BuildsApi.RefreshWindow()` redraws the window when the hangar changes.
- A New row picks up the design itself, as now, and fires `DesignPlaced` with `PickUp`. `ICopies.Build` is
  only called for hangar rows. The Craft tab shows hangar rows only, no New row.
- Designs are filed craft and filed subassemblies; unfiled ones aren't designs. Subassemblies have
  Facility "". Launchers and upper stages carry the category ids "Launchers" and "UpperStages".
- Filing a craft from the window's File… moves a file without the editor's parts, so it fires
  `LibraryChanged` but not `DesignSaved`.
- Pick-up now builds the parts itself (`CreateConstructFromTemplate`, then `SpawnConstruct`), as stock
  does inside. Spent-stage names are set the moment the launcher is held, also on an empty editor, which
  the old way missed.
- Ids live in `PhilsBuilds.cfg` as `DESIGN { key, id, line }`.

Phil's Reusability Suite is a separate career mod that requires Phil's Builds. Its design asks for six
optional hooks. The rule: **Phil's Builds without the suite behaves exactly as now.** Every hook is
something the suite reads or subscribes to. If nobody subscribes, nothing runs and nothing is drawn.

All of it lives in one public static class, `PhilsBuilds.BuildsApi`, plus a few plain data classes. The
suite references `PhilsBuilds.dll` directly.

1. **Read the library.**
   - `BuildsApi.Version` (an int) gives the API version.
   - `BuildsApi.Designs()` returns every craft and subassembly in the save as `Design` snapshots. Each has:
     - Id and FamilyId;
     - Name, Family, Mark (a number), MarkText and Kind (Craft, Launcher, UpperStage or Block);
     - Category and CategoryLabel, and Facility (VAB or SPH);
     - Tons and Width, for launchers and upper stages;
     - Group, Notes and Path.
   - `Find(id)` and `OfFile(path)` look one up.
   - A `LibraryChanged` event fires after any save, filing, rename, re-rating or delete.
   - **New: stable ids.** Designs are only known by file path today, and a path changes when a family is
     renamed, a craft is filed or a launcher is re-rated ("Oak 030" to "Oak 060").
     - Phil's Builds would give each design file an Id in its save file (`PhilsBuilds.cfg`), and each
       family a FamilyId.
     - It carries both across every move and rename it does.
     - Ids are given out the first time a design is seen, so existing saves need nothing done.
2. **Design placed.**
   - `event Action<Placed> DesignPlaced` fires after Load, Merge or pick-up from the library. It carries:
     - Design;
     - How (Load, Merge or PickUp);
     - Parts, where `Parts[i]` is slot *i*.
   - **A slot is the part's position in the design file**, the order KSP writes and reads parts.
   - For Merge and pick-up, Phil's Builds would build the craft itself, which also tells it each part's
     slot:
     1. `ShipConstruction.CreateConstructFromTemplate`, whose parts come back in file order.
     2. `EditorLogic.SpawnConstruct`.
     This is what stock's pick-up does inside anyway. Both calls are public in 1.12.5.
   - For a full Load, the editor's ship part list is in file order.
   - Confirm both orders in game before relying on them.
   - After stamping, the suite's own part module carries the stamps through saves, reverts and launches.
     Phil's Builds doesn't need to report anything again.
3. **Design saved.**
   - `event Action<Saved> DesignSaved` fires after Save over, Save as next mark, filing and subassembly
     saves. It carries:
     - Design;
     - NewMark;
     - Parts, in the order written: the editor ship's part list for craft, and the saved construct's for
       subassemblies.
   - Saving over a mark changes its slots. So the suite should keep its own copy of the design file with
     each build or launch record. Then a flown stage is always completed from the version it was built
     as, even if the file changes later.
4. **Hangar copies in the window.**
   - `BuildsApi.SetCopies(ICopies provider)` registers the suite (null removes it). Until something
     registers, the window is unchanged.
   - `ICopies` has:
     - `CopiesOf(design)`: the rows under a design, New included if wanted. Each row has Label ("#4"),
       Text, Warning ("missing 1 grid fin"), Price ("30,510 · 1.1 days"), Reserved and ReservedText.
     - `Note(design)`: the green line on the design's row ("2 flown first stages in the hangar").
     - `Detail(design, row)`: the side panel, with eyebrow, title, chips, cost lines, total, flight log and
       hint.
   - Phil's Builds draws all of it in its own look, as in the approved suite mockup (Launchers tab with
     hangar copies).
   - It works the same on the Craft and Launchers tabs. The rail gets a "Hangar" section with "With flown
     copies" and its count. Phil's Builds counts the designs whose `CopiesOf` has a flown row.
   - Reserved rows are greyed out and can't be picked.
   - The tab counts in the mockup (Craft 37, Launchers 6) are plain library counts. Phil's Builds can show
     them with or without the suite.
5. **Picking a copy.**
   - `ICopies.Build(design, row, Action<ShipConstruct> hold)` hands the pick to the suite, which builds
     the construct: the flown first stage plus new parts from the design.
   - It calls `hold(ship)` when ready, or `hold(null)` to cancel. It can take more than a frame.
   - Phil's Builds then closes the window, puts the construct on the cursor with `SpawnConstruct`, and
     names its spent stages the way it does for launchers now.
   - `SpawnConstruct` holds a construct by its root part. To hang it under a payload, the suite roots the
     construct at its top part. This is the same root-part issue as the banked Merge idea above.
6. **Is this filed?**
   - `BuildsApi.Current` is the open craft's `Design`, or null if it isn't filed.
   - `BuildsApi.OpenSave()` opens the Save panel, for the Build panel's "file it now" button.

Timing: the suite needs these at its own step 5 (the Phil's Builds link). Its first four steps don't need
them.

**The suite's reply (2026-11-07):** it accepts the plan as written, with three notes. Phil hasn't said to
build it yet.
- **When DesignPlaced fires:** once the parts exist, after `SpawnConstruct` or after a load finishes.
  - It also fires for constructs built through `ICopies.Build`. The suite skips parts it has already
    stamped.
  - A construct held on the cursor isn't in `EditorLogic.fetch.ship.parts` until it's attached.
    Exception: on an empty editor it becomes the ship. The event carries the `Part` objects themselves,
    so the suite can stamp them either way.
- **The suite keys on the stable design Ids.** It keeps its own copy of the design file with each build
  and launch record, as suggested in 3.
- **Phil's Builds strips nothing from saved files.** The suite gives fresh serials to any parts that Phil's
  Builds loads or merges. Only the suite's own hangar picks keep their serials.

## Notes for building

- KSP 1.12's craft browser has a folder tree (`KSP.UI.Screens.DirectoryController` exists). Confirm in game.
- Stock KSP already names vessels per command part (`VesselNaming`: name, vessel type, priority). The
  highest-priority named part names a vessel after separation.
- Craft file headers hold `ship`, `description`, `type`, `size`, `vesselType`.
- Never sort or edit craft in Phil's real career save. Test on a copy.
- Name lists: `Tools/Check-Names.ps1` checks counts, duplicates, length, approved examples and overlap
  with PIP's program names (read from the PIP repo beside this one).
