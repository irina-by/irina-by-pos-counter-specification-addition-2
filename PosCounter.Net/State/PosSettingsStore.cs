using System;
using Microsoft.Win32;
using PosCounter.Net.Models;

namespace PosCounter.Net.State
{
    public static class PosSettingsStore
    {
        private const string RegistryPath = @"Software\PosCounter.Net";
        private static PosSettings _settings;

        public static PosSettings Current => _settings ?? (_settings = Load());

        public static PosSettings Load()
        {
            var settings = new PosSettings();
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    settings.DefaultLayer = GetString(key, "default_layer", settings.DefaultLayer);
                    settings.LastLayer = GetString(key, "last_layer", settings.LastLayer);
                    settings.Mode = GetString(key, "mode", settings.Mode);
                    settings.LayerScope = GetString(key, "layer_scope", settings.LayerScope);
                    settings.ViewMode = GetString(key, "view_mode", settings.ViewMode);
                    settings.AutoOpenExcel = GetString(key, "auto_open_excel", "1") == "1";
                    settings.CountAllInModel = GetString(key, "count_all_in_model", settings.CountAllInModel ? "1" : "0") == "1";
                }
            }
            catch
            {
                // Fallback to in-memory defaults.
            }

            return settings;
        }

        public static void Save()
        {
            Save(Current);
        }

        public static void Save(PosSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    key.SetValue("default_layer", settings.DefaultLayer ?? string.Empty);
                    key.SetValue("last_layer", settings.LastLayer ?? string.Empty);
                    key.SetValue("mode", settings.Mode ?? "layer");
                    key.SetValue("layer_scope", settings.LayerScope ?? "current");
                    key.SetValue("view_mode", settings.ViewMode ?? "dwg");
                    key.SetValue("auto_open_excel", settings.AutoOpenExcel ? "1" : "0");
                    key.SetValue("count_all_in_model", settings.CountAllInModel ? "1" : "0");
                }
            }
            catch
            {
                // Keep settings in memory only if registry is unavailable.
            }
        }

        private static string GetString(RegistryKey key, string valueName, string fallback)
        {
            var value = key.GetValue(valueName, fallback);
            return value?.ToString() ?? fallback;
        }
    }
}
