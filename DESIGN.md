# Phil's Vessel Naming: design

> Written by Claude (an AI). Phil designs and play-tests; he isn't a programmer.
> Working name. Design stage: nothing is built yet.

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
   `GameData/PhilsVesselNaming/Names/*.cfg` as `VESSEL_NAMES` nodes. Players and mods add names by adding
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

## Notes for building

- KSP 1.12's craft browser has a folder tree (`KSP.UI.Screens.DirectoryController` exists). Confirm in game.
- Stock KSP already names vessels per command part (`VesselNaming`: name, vessel type, priority). The
  highest-priority named part names a vessel after separation.
- Craft file headers hold `ship`, `description`, `type`, `size`, `vesselType`.
- Never sort or edit craft in Phil's real career save. Test on a copy.
- Name lists: `Tools/Check-Names.ps1` checks counts, duplicates, length, approved examples and overlap
  with PIP's program names (read from the PIP repo beside this one).
