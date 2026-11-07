// The suggested names: one list per build category (and per payload class for
// launchers), read from VESSEL_NAMES nodes in any mod's config files. Each save
// deals them in its own shuffled order and skips names it already uses.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhilsBuilds
{
    static class NameDecks
    {
        class Deck
        {
            public string Theme = "";
            public readonly List<string> Names = new List<string>();
        }

        static Dictionary<string, Deck> decks;

        public static string Key(string category, LauncherClass? cls = null) =>
            cls.HasValue ? category + "-" + cls.Value : category;

        static void Load()
        {
            if (decks != null) return;
            decks = new Dictionary<string, Deck>(StringComparer.OrdinalIgnoreCase);
            if (GameDatabase.Instance == null) return;
            int total = 0;
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("VESSEL_NAMES"))
            {
                string cat = node.GetValue("category");
                if (string.IsNullOrEmpty(cat)) continue;
                string cls = node.GetValue("class");
                string key = string.IsNullOrEmpty(cls) ? cat : cat + "-" + cls;
                if (!decks.TryGetValue(key, out Deck d)) decks[key] = d = new Deck();
                string theme = node.GetValue("theme");
                if (!string.IsNullOrEmpty(theme) && d.Theme.Length == 0) d.Theme = theme;
                foreach (string n in node.GetValues("name"))
                {
                    string name = Naming.Clean(n);
                    if (name.Length > 0 && !d.Names.Any(x => Naming.SameName(x, name))) { d.Names.Add(name); total++; }
                }
            }
            Debug.Log($"[PhilsBuilds] Name lists: {decks.Count} lists, {total} names");
        }

        static Deck Find(string key)
        {
            Load();
            if (decks.TryGetValue(key, out Deck d)) return d;
            // A launcher list without classes (an older or added file) serves every class.
            int dash = key.IndexOf('-');
            if (dash > 0 && decks.TryGetValue(key.Substring(0, dash), out d)) return d;
            return null;
        }

        public static string Theme(string key) => Find(key)?.Theme ?? "";

        public static int Count(string key) => Find(key)?.Names.Count ?? 0;

        public static int Unused(string key, ICollection<string> used) =>
            Find(key)?.Names.Count(n => !used.Contains(n)) ?? 0;

        // This save's order for the list: a shuffle seeded by the save and the list,
        // so the first suggestion stays the same until it's used.
        static List<string> Order(string key)
        {
            Deck d = Find(key);
            if (d == null) return new List<string>();
            int seed = Hash((HighLogic.SaveFolder ?? "") + "|" + key);
            var rng = new System.Random(seed);
            var list = d.Names.ToList();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                string t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
            return list;
        }

        // The next unused name from position `cursor` on (wrapping round), and the
        // position after it. Null when every name in the list is taken.
        public static string Deal(string key, ICollection<string> used, ref int cursor)
        {
            List<string> order = Order(key);
            if (order.Count == 0) return null;
            for (int k = 0; k < order.Count; k++)
            {
                int i = ((cursor + k) % order.Count + order.Count) % order.Count;
                if (!used.Contains(order[i]))
                {
                    cursor = i + 1;
                    return order[i];
                }
            }
            return null;
        }

        static int Hash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return h;
            }
        }
    }
}
