using System.Globalization;
using System.Text;
using TMPro;

namespace DamageSplash.Core
{
    /// <summary>"splash" console command. Output also goes to the BepInEx log.</summary>
    public static class ConsoleCommands
    {
        private static bool _registered;

        public static void Register()
        {
            if (_registered)
                return;
            _registered = true;

            new Terminal.ConsoleCommand("splash",
                "DamageSplash: demo | burst <n> | preset <name> | anim <name> | fonts | reload | stats",
                delegate (Terminal.ConsoleEventArgs args)
                {
                    string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "help";
                    switch (sub)
                    {
                        case "demo": Say(args.Context, "DamageSplash: " + Demo.Start(args.Length > 2 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float hold) ? hold : 0f)); break;
                        case "burst": Say(args.Context, "DamageSplash: " + Demo.Burst(args.Length > 2 && int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 40)); break;
                        case "rain": Say(args.Context, "DamageSplash: " + Demo.Rain(args.Length > 2 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float secs) ? secs : 30f)); break;
                        case "dot": Say(args.Context, "DamageSplash: " + Demo.Dot(args.Length > 2 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float dsecs) ? dsecs : 12f)); break;
                        case "stop": Demo.Stop(); Say(args.Context, "DamageSplash: stopped"); break;
                        case "set": Set(args); break;
                        case "get": Get(args); break;
                        case "preset": Preset(args); break;
                        case "anim": Anim(args); break;
                        case "fonts": FontList(args.Context); break;
                        case "reload": PluginConfig.Reload(); Say(args.Context, "DamageSplash: config reloaded from disk."); break;
                        case "stats": Say(args.Context, Stats()); break;
                        case "conflicts": Conflicts(args.Context); break;
                        case "flash": EdgeFlash.Trigger(); Say(args.Context, "DamageSplash: " + (EdgeFlash.Ready ? "flashed" : "no canvas to flash on yet")); break;
                        case "on": PluginConfig.Enabled.Value = true; Say(args.Context, "DamageSplash: on"); break;
                        case "off": PluginConfig.Enabled.Value = false; Say(args.Context, "DamageSplash: off (vanilla numbers)"); break;
                        default:
                            Say(args.Context, "splash demo [seconds]  - one of every kind of number around you, optionally held that long");
                            Say(args.Context, "splash burst [n]       - n random numbers at once (default 40)");
                            Say(args.Context, "splash rain [seconds]  - a steady trickle to tune against (default 30, 0 stops)");
                            Say(args.Context, "splash dot [seconds]   - fake burning ticks, to see them merge (default 12)");
                            Say(args.Context, "splash set <name> <v>  - change one setting live, e.g. splash set gravity 6");
                            Say(args.Context, "splash get [filter]    - print settings and their values");
                            Say(args.Context, "splash preset <name>   - " + string.Join(" | ", PluginConfig.PresetNames));
                            Say(args.Context, "splash anim <name>     - " + string.Join(" | ", PluginConfig.AnimPresetNames));
                            Say(args.Context, "splash fonts           - every font the numbers can use");
                            Say(args.Context, "splash reload          - re-read the config file");
                            Say(args.Context, "splash stats           - pool and material state");
                            Say(args.Context, "splash conflicts       - other mods that redraw the numbers");
                            Say(args.Context, "splash flash           - test the screen-edge flash");
                            Say(args.Context, "splash on|off");
                            break;
                    }
                }, optionsFetcher: () => new System.Collections.Generic.List<string> { "demo", "burst", "rain", "dot", "stop", "set", "get", "preset", "anim", "fonts", "reload", "stats", "conflicts", "flash", "on", "off" });
        }

        private static void Say(Terminal ctx, string line)
        {
            ctx.AddString(line);
            DamageSplashPlugin.Log.LogInfo(line);
        }

        private static void Set(Terminal.ConsoleEventArgs args)
        {
            if (args.Length < 4)
            {
                Say(args.Context, "splash set <setting> <value>, for example \"splash set gravity 6\" or \"splash set Font Valheim-Norse\"");
                return;
            }
            Say(args.Context, "DamageSplash: " + PluginConfig.SetByName(args[2], args[3]));
        }

        private static void Get(Terminal.ConsoleEventArgs args)
        {
            System.Collections.Generic.List<string> lines = PluginConfig.Describe(args.Length > 2 ? args[2] : null);
            Say(args.Context, "DamageSplash: " + lines.Count + " setting(s)");
            foreach (string line in lines)
                Say(args.Context, "  " + line);
        }

        private static void Preset(Terminal.ConsoleEventArgs args)
        {
            string name = args.Length > 2 ? args[2] : "";
            foreach (string p in PluginConfig.PresetNames)
            {
                if (string.Equals(p, name, System.StringComparison.OrdinalIgnoreCase) && p != PluginConfig.PresetCustom)
                {
                    PluginConfig.ApplyPreset(p);
                    Say(args.Context, "DamageSplash: preset " + p);
                    return;
                }
            }
            Say(args.Context, "DamageSplash: preset is " + PluginConfig.Preset.Value + "; choose " + string.Join(" | ", PluginConfig.PresetNames));
        }

        private static void Anim(Terminal.ConsoleEventArgs args)
        {
            string name = args.Length > 2 ? args[2] : "";
            foreach (string p in PluginConfig.AnimPresetNames)
            {
                if (string.Equals(p, name, System.StringComparison.OrdinalIgnoreCase) && p != PluginConfig.PresetCustom)
                {
                    PluginConfig.ApplyAnimPreset(p);
                    Say(args.Context, "DamageSplash: animation " + p);
                    return;
                }
            }
            Say(args.Context, "DamageSplash: animation is " + PluginConfig.AnimPreset.Value + "; choose " + string.Join(" | ", PluginConfig.AnimPresetNames));
        }

        private static void FontList(Terminal ctx)
        {
            Fonts.Refresh();
            PluginConfig.RebindFontChoices(Fonts.Names);
            TMP_FontAsset current = Fonts.Resolve(PluginConfig.FontName.Value);
            Say(ctx, "DamageSplash: " + (Fonts.Names.Count - 1) + " font(s) loaded; config says \"" + PluginConfig.FontName.Value
                + "\", using " + (current != null ? current.name : "the vanilla prefab font" + (SplashPool.PrefabFont != null ? " (" + SplashPool.PrefabFont.name + ")" : "")));
            foreach (string n in Fonts.Names)
                Say(ctx, "  " + n);
        }

        private static void Conflicts(Terminal ctx)
        {
            Compat.Detect();
            if (Compat.Conflict == null)
                Say(ctx, "DamageSplash: no other damage-number mod found; drawing the numbers.");
            else
                Say(ctx, "DamageSplash: " + Compat.Conflict + " also redraws the numbers; DamageSplash is "
                    + (Compat.ShouldYield ? "standing aside." : "drawing anyway (YieldToOtherMods is off), which means the two are racing."));
        }

        private static string Stats()
        {
            var sb = new StringBuilder(256);
            sb.Append("DamageSplash: ").Append(PluginConfig.Enabled.Value ? "on" : "off")
              .Append(", preset ").Append(PluginConfig.Preset.Value).Append('/').Append(PluginConfig.AnimPreset.Value)
              .Append(", pool ").Append(SplashPool.ActiveCount).Append(" active / ").Append(SplashPool.FreeCount).Append(" free")
              .Append(SplashPool.Ready ? "" : " (not ready)")
              .Append(", prefab font ").Append(SplashPool.PrefabFont != null ? SplashPool.PrefabFont.name : "none")
              .Append(", prefab shader ").Append(SplashPool.PrefabMaterial != null ? SplashPool.PrefabMaterial.shader.name : "none")
              .Append(", visibility ").Append(PluginConfig.Visibility.Value)
              .Append(Compat.Conflict == null ? ", no conflicts" : (Compat.ShouldYield ? ", yielding to " : ", racing ") + Compat.Conflict)
              .Append(", game ").Append(Version.CurrentVersion);
            return sb.ToString();
        }
    }
}
