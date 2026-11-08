// Checks the naming rules in src/Core.cs without KSP. Run Tools/Test-Core.ps1.
using System;
using PhilsBuilds;

static class CoreTests
{
    static int failed, passed;

    static void Is<T>(T actual, T expected, string what)
    {
        if (Equals(actual, expected)) { passed++; return; }
        failed++;
        Console.WriteLine($"FAIL {what}: got '{actual}', expected '{expected}'");
    }

    static void Parse(string name, bool ok, string rest = null, MarkStyle style = MarkStyle.Roman, int mark = 0)
    {
        bool got = Marks.TryParse(name, out string r, out MarkStyle s, out int m);
        Is(got, ok, $"parses '{name}'");
        if (!ok || !got) return;
        Is(r, rest, $"rest of '{name}'");
        Is(s, style, $"style of '{name}'");
        Is(m, mark, $"mark of '{name}'");
    }

    static int Main()
    {
        Parse("Haven Lab II", true, "Haven Lab", MarkStyle.Roman, 2);
        Parse("Comsat I", true, "Comsat", MarkStyle.Roman, 1);
        Parse("Evergreen Service I", true, "Evergreen Service", MarkStyle.Roman, 1);
        Parse("Magnolia Mk I", true, "Magnolia", MarkStyle.Mk, 1);
        Parse("Magnolia Mk 2", true, "Magnolia", MarkStyle.Mk, 2);
        Parse("Magnolia Mk. 3", true, "Magnolia", MarkStyle.Mk, 3);
        Parse("Sherpa Block III", true, "Sherpa", MarkStyle.Block, 3);
        Parse("Comsat B", true, "Comsat", MarkStyle.Letter, 2);
        Parse("Oak 060 II", true, "Oak 060", MarkStyle.Roman, 2);
        Parse("Oak 030", false);
        Parse("Gravioli Bomber", false);
        Parse("Haven", false);
        Parse("X-3 Cricket IV", true, "X-3 Cricket", MarkStyle.Roman, 4);

        Is(Marks.Format(MarkStyle.Roman, 14), "XIV", "roman 14");
        Is(Marks.Format(MarkStyle.Mk, 2), "Mk II", "mk 2");
        Is(Marks.Format(MarkStyle.Block, 1), "Block I", "block 1");
        Is(Marks.Format(MarkStyle.Letter, 8), "H", "letter 8");
        Is(Marks.Format(MarkStyle.Letter, 9), "J", "letter 9 skips I");
        Is(Marks.Short(MarkStyle.Mk, 3), "III", "short mk 3");

        Is(Naming.Craft("Haven", "Lab", MarkStyle.Roman, 1), "Haven Lab I", "craft name");
        Is(Naming.Craft("Evergreen", "", MarkStyle.Mk, 2), "Evergreen Mk II", "craft name, no role");
        Is(Naming.Launcher("Oak", 30, MarkStyle.Roman, 1), "Oak 030", "launcher mark 1");
        Is(Naming.Launcher("Oak", 60, MarkStyle.Roman, 2), "Oak 060 II", "launcher mark 2");
        Is(Naming.Launcher("Birch", 2.5f, MarkStyle.Roman, 1), "Birch 002", "launcher 2.5 t");
        Is(Naming.Launcher("Tiny", 0.4f, MarkStyle.Roman, 1), "Tiny 001", "launcher under a ton");
        Is(Naming.Launcher("Colossus", 1250, MarkStyle.Roman, 1), "Colossus 1250", "launcher over 999 t");
        Is(Naming.Stage("Sherpa", MarkStyle.Roman, 1), "Sherpa I", "upper stage");

        Is(Naming.ClassOf(1), LauncherClass.Light, "1 t light");
        Is(Naming.ClassOf(5), LauncherClass.Light, "5 t light");
        Is(Naming.ClassOf(10), LauncherClass.Medium, "10 t medium");
        Is(Naming.ClassOf(30), LauncherClass.Heavy, "30 t heavy");
        Is(Naming.ClassOf(100), LauncherClass.SuperHeavy, "100 t super-heavy");

        Is(Naming.Clean("Haven: Lab / I?"), "Haven Lab I", "clean file name");
        Is(Naming.Tons(7.43f), "7.4 t", "tons small");
        Is(Naming.Tons(183.2f), "183 t", "tons big");
        Is(Category.ByFolder("Relays and comsats")?.Id, "Relays", "category by folder");
        Is(Category.ByFolder("stations")?.Id, "Stations", "category by folder, any case");

        // Phil typed "Orpheus I" as a family name and got "Orpheus I I".
        Is(Naming.FamilyPart("Orpheus I"), "Orpheus", "typed mark dropped");
        Is(Naming.FamilyPart("Orpheus"), "Orpheus", "plain family kept");
        Is(Naming.FamilyPart("Magnolia Mk II"), "Magnolia", "typed Mk mark dropped");
        Is(Naming.LauncherFamily("Oak 030"), "Oak", "typed payload code dropped");
        Is(Naming.LauncherFamily("Oak 060 II"), "Oak", "typed code and mark dropped");
        Is(Naming.LauncherFamily("Kata Tjuta"), "Kata Tjuta", "two-word launcher kept");
        Is(Naming.InFamily("Orpheus II", "Orpheus"), true, "mark in family");
        Is(Naming.InFamily("Orpheusly", "Orpheus"), false, "longer word not in family");
        Is(Naming.SwapFamily("Orpheus I I", "Orpheus I", "Orpheus"), "Orpheus I", "rename family keeps mark");
        Is(Naming.SwapFamily("Orpheus II", "Orpheus I", "Orpheus"), "Orpheus II", "rename doesn't cut into a numeral");
        Is(Naming.SwapFamily("Haven Lab III", "Haven", "Harbor"), "Harbor Lab III", "rename with role");

        // What changed between marks, shown in the craft panel.
        Is(Naming.MarkChanges(10.0f, 12.3f, 20000, 21200, 40, 43, 3, 3), "+2.3 t · +1,200 funds · +3 parts", "mark changes, growth");
        Is(Naming.MarkChanges(12.3f, 10.0f, 21200, 20000, 43, 42, 3, 2), "-2.3 t · -1,200 funds · -1 part · -1 stage", "mark changes, shrink");
        Is(Naming.MarkChanges(10.0f, 10.02f, 20000, 20000.4f, 40, 40, 3, 3), "", "mark changes, too small to show");
        Is(Naming.MarkChanges(0, 10f, 0, 20000, 0, 40, 0, 3), "", "mark changes, older mark unknown");
        Is(Naming.MarkChanges(9.0f, 25.2f, 1000, 2000, 10, 11, 1, 2), "+16 t · +1,000 funds · +1 part · +1 stage", "mark changes, big mass in whole tons");

        // Stage names: one rule for parts in the editor and in saved files.
        Func<string, bool> ours = l => l == "Oak 030" || l == "Haytor 002";
        Is(StageNames.LauncherPart("Oak 030 Stage 2"), "Oak 030", "launcher of a stage name");
        Is(StageNames.LauncherPart("Oak 030 Booster"), "Oak 030", "launcher of a booster name");
        Is(StageNames.LauncherPart("Oak 030"), "Oak 030", "bare launcher name");
        Is(StageNames.IsLabel("Oak 030 Stage 1", true, 1, ours), true, "our stage label");
        Is(StageNames.IsLabel("Oak 030 Stage 4", false, 1, ours), true, "our flyable stage label");
        Is(StageNames.IsLabel("Oak 030", true, 1, ours), true, "a just-tagged part (bare name, debris)");
        Is(StageNames.IsLabel("Oak 030 Stage 1", true, -1, ours), true, "0.1.0's label priority");
        Is(StageNames.IsLabel("Oak 030 Stage 1", true, 2, ours), false, "wrong priority");
        Is(StageNames.IsLabel("Oak 030", false, 1, ours), false, "a ship named like a launcher isn't a label");
        Is(StageNames.IsLabel("Kerbal X Booster", true, 1, ours), false, "a hand-typed booster name is left alone");
        Is(StageNames.IsLabel("Kerbal X Booster", true, 1, ours, anyLauncher: true), true, "saving a stage takes any stage-shaped name");
        Is(StageNames.IsLabel("Kerbal X", true, 1, ours, anyLauncher: true), false, "but not any debris name");
        Is(StageNames.IsLabel("", true, 1, ours), false, "no name");

        // Paths.
        Is(Paths.Same(@"C:\KSP\saves\a\Ships\VAB", @"c:\ksp\saves\a\ships\vab\"), true, "same folder, any case and trailing slash");
        Is(Paths.Same(@"C:\KSP\x.craft", @"C:\KSP\y.craft"), false, "different files");
        Is(Paths.Same(null, @"C:\KSP"), false, "no path");
        Is(Paths.Under(@"C:\KSP\Ships\VAB", @"C:\KSP\Ships\VAB\Capsules\Argo"), "Capsules/Argo", "folder under the root");
        Is(Paths.Under(@"C:\KSP\Ships\VAB", @"C:\KSP\Ships\VAB\"), "", "the root itself");
        Is(Paths.Under(@"C:\KSP\Ships\VAB", @"C:\KSP\Ships\VABX\Capsules"), null, "a folder that only starts the same");
        Is(Paths.Under(@"C:\KSP\Ships\VAB", @"C:\KSP\Ships\SPH"), null, "somewhere else");

        Is(SubKinds.IsStage(SubKind.Launcher) && SubKinds.IsStage(SubKind.UpperStage) && !SubKinds.IsStage(SubKind.Block) && !SubKinds.IsStage(SubKind.Unfiled), true, "stage kinds");

        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
