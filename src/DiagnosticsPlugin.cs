using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;

namespace StonewardsTogglerDiagnostics
{
    [BepInPlugin("com.thereisnouser.stonewards.togglerdiagnostics", "Toggler Diagnostics", "0.1.0")]
    public sealed class DiagnosticsPlugin : BaseUnityPlugin
    {
        private bool _done;
        private float _nextAttempt;
        private static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private void Update()
        {
            if (_done || Time.unscaledTime < _nextAttempt)
                return;
            _nextAttempt = Time.unscaledTime + 5f;
            try
            {
                var upgrades = Resources.FindObjectsOfTypeAll<RogueLikeUpgradeSO>();
                if (upgrades == null || upgrades.Length == 0)
                    return;

                Dump(upgrades);
                _done = true;
            }
            catch (Exception e)
            {
                Logger.LogError("Upgrade inspection failed: " + e);
                _done = true;
            }
        }

        private void Dump(RogueLikeUpgradeSO[] upgrades)
        {
            var rows = new List<string>();
            rows.Add("# Upgrade assets detected in the current Stonewards build");
            rows.Add("# Read-only scan. No unlocks, rewards or saves are modified.");
            rows.Add("# A missing asset means it may be loaded only after entering a relevant game scene.");
            rows.Add("# ID | Unity name | Category | IsServer | unlock/default-related fields and properties");

            foreach (var so in upgrades.OrderBy(u => u == null ? "" : ((BaseUpgradeSO)u).ID, StringComparer.Ordinal))
            {
                if (so == null)
                    continue;
                var extra = new List<string>();
                for (var type = so.GetType(); type != null && type != typeof(UnityEngine.Object); type = type.BaseType)
                {
                    foreach (var field in type.GetFields(Flags | BindingFlags.DeclaredOnly))
                    {
                        if (field.Name.IndexOf("unlock", StringComparison.OrdinalIgnoreCase) < 0 &&
                            field.Name.IndexOf("default", StringComparison.OrdinalIgnoreCase) < 0 &&
                            field.Name.IndexOf("toggl", StringComparison.OrdinalIgnoreCase) < 0 &&
                            field.Name.IndexOf("avail", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        try { extra.Add(field.Name + "=" + Format(field.GetValue(so))); }
                        catch { extra.Add(field.Name + "=<unreadable>"); }
                    }
                    foreach (var property in type.GetProperties(Flags | BindingFlags.DeclaredOnly))
                    {
                        if (property.GetIndexParameters().Length > 0 ||
                            (property.Name.IndexOf("unlock", StringComparison.OrdinalIgnoreCase) < 0 &&
                             property.Name.IndexOf("default", StringComparison.OrdinalIgnoreCase) < 0 &&
                             property.Name.IndexOf("toggl", StringComparison.OrdinalIgnoreCase) < 0 &&
                             property.Name.IndexOf("avail", StringComparison.OrdinalIgnoreCase) < 0))
                            continue;
                        try { extra.Add(property.Name + "=" + Format(property.GetValue(so, null))); }
                        catch { extra.Add(property.Name + "=<unreadable>"); }
                    }
                }
                rows.Add(string.Join(" | ",
                    Safe(((BaseUpgradeSO)so).ID),
                    Safe(so.name),
                    so.UpgradeCategory.ToString(),
                    ((BaseUpgradeSO)so).IsServer.ToString(),
                    Safe(string.Join("; ", extra))));
            }

            var path = Path.Combine(Paths.ConfigPath, "StonewardsTogglerDiagnostics.upgrades.txt");
            File.WriteAllLines(path, rows.ToArray(), new UTF8Encoding(false));
            Logger.LogInfo("Detected " + upgrades.Length + " upgrade assets. Wrote " + path);
        }

        private static string Format(object value) => value == null ? "null" : Safe(value.ToString());
        private static string Safe(string s) => (s ?? "").Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
    }
}
