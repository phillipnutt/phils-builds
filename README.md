# Phil's Builds

> **Written by an AI.** Every line of code in this mod was written by Claude (Anthropic's AI model).
> Phil Nutt designs it and play-tests it. He isn't a programmer, and no human has reviewed the code.
> Back up your saves before trying it.

A Kerbal Space Program 1.12 mod for naming and organizing your craft like a real space program.

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
  "Cypress 010 Booster" and "Cypress 010 Stage 1", using KSP's own per-part vessel naming. They stay debris.

Craft are stored as real folders, `Ships/VAB/<Category>/<Family>/`, so they stay sorted in the stock
load dialog even without this mod. Subassemblies stay in the stock Subassemblies folder. What kind each one
is, and launcher ratings, are kept in `saves/<save>/PhilsBuilds.cfg`.

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

Nothing changes unless a mod uses it.

## License

MIT. Copyright Phil Nutt.
