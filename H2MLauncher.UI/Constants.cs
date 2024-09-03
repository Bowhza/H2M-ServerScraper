﻿using System.IO;

namespace H2MLauncher.UI
{
    public static class Constants
    {
        public static readonly string LocalDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterH2MLauncher");

        public static readonly string LogFilePath = Path.Combine(LocalDir, "log.txt");

        public static readonly string LauncherSettingsFileName = "launchersettings.json";

        public static readonly string LauncherSettingsFilePath = Path.Combine(LocalDir, LauncherSettingsFileName);

        /// <summary>
        /// The key of the <see cref="Core.Settings.H2MLauncherSettings"/> section in the configuration.
        /// </summary>
        public const string LauncherSettingsSection = "H2MLauncher";

        /// <summary>
        /// The key of the <see cref="Core.Settings.ResourceSettings"/> section in the configuration.
        /// </summary>
        public const string ResourceSection = "Resource";

        /// <summary>
        /// The key of the <see cref="Core.Settings.MatchmakingSettings"/> section in the configuration.
        /// </summary>
        public const string MatchmakingSection = "Matchmaking";

        /// <summary>
        /// The injection key for the default <see cref="Core.Settings.H2MLauncherSettings"/>.
        /// </summary>
        public const string DefaultSettingsKey = "DefaultSettings";
    }
}
