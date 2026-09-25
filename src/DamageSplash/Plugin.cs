using BepInEx;
using BepInEx.Logging;
using DamageSplash.Core;
using DamageSplash.Patches;
using HarmonyLib;
using System;
using System.IO;
using UnityEngine;

namespace DamageSplash
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim.x86_64")]
    public sealed class DamageSplashPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.jumpingmushroom.damagesplash";
        public const string PluginName = "DamageSplash";
        public const string PluginVersion = "0.1.2";

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private float _nextCommandPoll;

        private void Awake()
        {
            Log = Logger;

            PluginConfig.Bind(base.Config);
            ConsoleCommands.Register();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(DamageSplashPlugin).Assembly);

            PluginConfig.Changed += OnConfigChanged;

            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void OnDestroy()
        {
            PluginConfig.Changed -= OnConfigChanged;
            DamageTextPatches.ApplyMaxDistance(unloading: true);
            EdgeFlash.Destroy();
            SplashPool.Clear();
            if (_harmony != null)
                _harmony.UnpatchSelf();
        }

        private void Update()
        {
            float now = Time.time;
            Demo.Update(now);
            EdgeFlash.Update(Time.deltaTime);
            if (PluginConfig.DevCommandFile.Value && now >= _nextCommandPoll)
            {
                _nextCommandPoll = now + 0.5f;
                RunCommandFile();
            }
        }

        /// <summary>
        /// Development aid: console commands dropped into a file next to the config, so the look
        /// can be driven from a shell while the game runs. The file is left alone until a world
        /// is loaded, so a sequence queued before the game starts runs once there is a player to
        /// show it to rather than being eaten at the main menu.
        /// </summary>
        private static void RunCommandFile()
        {
            if (Player.m_localPlayer == null || Console.instance == null)
                return;

            string path = Path.Combine(BepInEx.Paths.ConfigPath, "DamageSplash.cmd");
            if (!File.Exists(path))
                return;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
                File.Delete(path);
            }
            catch (Exception ex)
            {
                // Locked by the writer, or no permission: try again on the next poll.
                if (PluginConfig.Verbose.Value)
                    Log.LogDebug("command file not read: " + ex.Message);
                return;
            }

            Terminal console = Console.instance;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                Log.LogInfo("command file: " + line);
                // The game's own checks stay on: a file anyone can write must not run cheats
                // that devcommands has not unlocked. The splash commands need no unlocking.
                console.TryRunCommand(line, silentFail: false, skipAllowedCheck: false);
            }
        }

        /// <summary>Any setting changed: rebuild cached materials and hand them to the numbers
        /// already in flight, and push the distance cap into the live DamageText.</summary>
        private static void OnConfigChanged()
        {
            Styles.Invalidate();
            SplashPool.Rematerial();
            EdgeFlash.Invalidate();
            Compat.Detect();
            DamageTextPatches.ApplyMaxDistance();
        }
    }
}
