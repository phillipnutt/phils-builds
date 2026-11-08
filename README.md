# Phil's Builds

> **Written by an AI.** Every line of code in this mod was written by Claude (Anthropic's AI model).
> Phil Nutt designs it and play-tests it. He isn't a programmer, and no human has reviewed the code.
> Back up your saves before trying it.

A Kerbal Space Program 1.12 mod for naming and organizing your craft like a real space program. It's one
of Phil's mods, alongside [Phil's Iterative Parts](https://github.com/phillipnutt/phils-iterative-parts)
and [Phil's Part Hider](https://github.com/phillipnutt/phils-part-hider), and shares their look.

**This is an early version (0.1).** Saving, families and marks have been play-tested. Picking launchers
from the library and spent-stage names in flight are newer.

- **Families and marks.** Craft are saved into families, like Thunderbolt I, II, III. Older marks stay and
  still load. A program built in pieces is several families sharing a name, like Haven, Haven Lab and
  Haven Service.
- **Launchers rated by payload.** Save a launcher with its rating: tons to orbit and widest payload.
  The library then shows only the launchers that can lift what's in the editor. Click one to pick it up,
  just like a stock subassembly. Launcher names carry the payload code, so a 30-ton launcher is "Oak 030".
- **Themed names.** There are 1,250 suggested names in 25 lists, one for each kind of build: giants and
  mountains for launchers (sized to the payload), birds for landers, winds for spaceplanes, and so on.
  Each save deals them in its own shuffled order and never repeats a name you've used.
- **One library window.** It replaces the stock craft loader and subassembly list, and opens from the
  editor's Load button.
- **Spent-stage names.** A launcher picked from the library names the pieces that fall off it, such as
  "Cypress 010 Booster" and "Cypress 010 Stage 1", using KSP's own per-part vessel naming. They're debris,
  unless a piece has its own probe core or crew pod: then it stays flyable, under its stage name.

Craft are stored as real folders, `Ships/VAB/<Category>/<Family>/`, so they stay sorted in the stock
load dialog even without this mod. Subassemblies stay in the stock Subassemblies folder. What kind each one
is, and launcher ratings, are kept in `saves/<save>/PhilsBuilds.cfg`.

## Install

Copy `GameData/PhilsBuilds` from the release zip into your KSP `GameData` folder. No other mods needed.

While it's installed:
- The editor's Load button opens Phil's Builds' library in place of the stock craft loader.
- The stock Subassemblies tab in the parts list is hidden; subassemblies are in the library.
- The Save button asks where a new design goes, and for a filed one, whether to save over the open mark
  or as the next one.

Remove the folder to go back to stock. Your craft stay where they are.

## Using it

- **Save** a new design: pick its category, and take a dealt name or type your own. It's filed as the
  first mark of a new family.
- **Next mark:** load a mark, change it, press Save, and pick Save as next.
- **Launchers:** build one on its own, then press Save and pick Launcher at the end of "What is it?" (or
  click the drop box on the library's Launchers tab, or pick up its top part first and drop it there).
  Give it a rating, tons to orbit and widest payload, from your own test flights.
- **Change a launcher:** load it, change it, press Save, and pick Save over or Save as its next mark
  (Rockall 002 II). Upper stages work the same way.
- **Pick a launcher:** build the payload, open the Launchers tab, and click one that fits. It comes up
  on the cursor, ready to attach underneath.
- **Fly a launcher on its own:** with nothing in the editor, press Load on a launcher (or double-click it).
  It opens as the craft, named after itself.
- **Unfiled craft** from before you installed the mod are listed under Unfiled. File them into families
  from there.

## Adding names

Any mod or player can add names with a config file anywhere in GameData:

```
VESSEL_NAMES
{
	category = Stations
	theme = Harbors and harmony
	name = Lighthouse
}
```

Launcher lists also take `class = Light`, `Medium`, `Heavy` or `SuperHeavy`.

## For other mods

`PhilsBuilds.BuildsApi`, in `src/Api.cs`, lets another mod:
- read the library, where each design has a stable id;
- hear when parts from a design are placed in the editor or saved;
- show its own built copies of a design (a hangar) in the library window.

Nothing changes unless a mod uses it. Reference `PhilsBuilds.dll` and declare
`[KSPAssemblyDependency("PhilsBuilds", 0, 1)]`.

## Building

MSBuild on `PhilsBuilds.csproj`. It expects KSP at the default Steam location; pass `/p:KspDir="..."` for
another install. `Tools/Test-Core.ps1` checks the naming rules, `Tools/Check-Names.ps1` checks the name
lists, and `Tools/Make-Release.ps1` builds the release zip.

## License

MIT. Copyright Phil Nutt. See [LICENSE](LICENSE).
