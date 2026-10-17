using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsTogglerPlus
{
    /// <summary>
    /// Per-class ring exclusions. Vanilla global ring/tome blacklist is untouched.
    /// Every class gets a separate BepInEx config entry; only the local player's
    /// level-up draft is filtered by these additional rules.
    /// </summary>
    internal static class ClassRingRules
    {
        private const string SelectorName = "stonewards-tp-class-selector";

        private static BaseUnityPlugin _plugin;
        private static Type _nativeButtonType;
        private static FieldInfo _buttonUpgrade;
        private static FieldInfo _toggleCheck;
        private static MethodInfo _refreshToggle;
        private static MethodInfo _getTogglerLevel;

        private static readonly Dictionary<PlayerClass, ConfigEntry<string>> Entries =
            new Dictionary<PlayerClass, ConfigEntry<string>>();
        private static readonly Dictionary<PlayerClass, HashSet<string>> Disabled =
            new Dictionary<PlayerClass, HashSet<string>>();
        private static readonly Dictionary<string, Button> SelectorButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);

        private static PlayerClass? _selectedClass;
        private static VisualElement _shopRoot;
        private static VisualElement _selector;

        internal static void Initialize(BaseUnityPlugin plugin, Harmony harmony, Type nativeButtonType,
            FieldInfo buttonUpgrade, MethodInfo getTogglerLevel)
        {
            _plugin = plugin;
            _nativeButtonType = nativeButtonType;
            _buttonUpgrade = buttonUpgrade;
            _getTogglerLevel = getTogglerLevel;
            _toggleCheck = AccessTools.Field(nativeButtonType, "toggleCheck");
            _refreshToggle = AccessTools.Method(nativeButtonType, "RefreshToggle");

            foreach (PlayerClass cls in Enum.GetValues(typeof(PlayerClass)))
            {
                string name = cls.ToString();
                if (Entries.ContainsKey(cls))
                    continue;

                var entry = plugin.Config.Bind("Per-class ring exclusions", name, "",
                    "Additional banned ring IDs for " + name +
                    ". Global in-game Toggler bans still take precedence. Managed by the mod UI.");
                Entries.Add(cls, entry);
                Disabled.Add(cls, Deserialize(entry.Value));
            }

            Patch(harmony, AccessTools.Method(nativeButtonType, "OnToggleClick"),
                nameof(BeforeToggleClick), prefix: true);
            Patch(harmony, _refreshToggle, nameof(AfterRefreshToggle), prefix: false);

            Type rogueManager = AccessTools.TypeByName("RogueLikeUpgradeManager");
            Patch(harmony, AccessTools.Method(rogueManager, "CreateValidLevelupDraftPool"),
                nameof(AfterCreateRingDraftPool), prefix: false);

            plugin.Logger.LogInfo("Per-class ring rules initialized for " + Entries.Count + " classes.");
        }

        private static void Patch(Harmony harmony, MethodInfo target, string handler, bool prefix)
        {
            if (target == null)
            {
                _plugin.Logger.LogWarning("Class-ring patch target missing: " + handler);
                return;
            }

            try
            {
                var patchMethod = new HarmonyMethod(AccessTools.Method(typeof(ClassRingRules), handler));
                harmony.Patch(target,
                    prefix: prefix ? patchMethod : null,
                    postfix: prefix ? null : patchMethod);
            }
            catch (Exception ex)
            {
                _plugin.Logger.LogError("Could not install " + handler + ": " + ex);
            }
        }

        private static HashSet<string> Deserialize(string serialized)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(serialized))
                return result;

            foreach (var part in serialized.Split(','))
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;
                try
                {
                    var id = Uri.UnescapeDataString(part.Trim());
                    if (!string.IsNullOrEmpty(id))
                        result.Add(id);
                }
                catch (UriFormatException) { /* Skip malformed manual config edits. */ }
            }
            return result;
        }

        private static void SetClassRule(PlayerClass cls, string id)
        {
            if (!Disabled.TryGetValue(cls, out var ids))
                return;

            if (!ids.Add(id))
                ids.Remove(id);

            // Store separately from the game's ShopData (and Steam Cloud saves).
            Entries[cls].Value = string.Join(",",
                ids.OrderBy(x => x, StringComparer.Ordinal).Select(Uri.EscapeDataString));
            _plugin.Config.Save();
        }

        private static bool IsRingButton(object button, out RogueLikeUpgradeSO ring)
        {
            ring = _buttonUpgrade?.GetValue(button) as RogueLikeUpgradeSO;
            return ring != null && ring.UpgradeCategory == UpgradeCategory.LEVELUP;
        }

        // The native Toggler click normally changes the GLOBAL ShopData blacklist.
        // In a class tab, intercept ring clicks and change only that class's config.
        private static bool BeforeToggleClick(object __instance)
        {
            if (!_selectedClass.HasValue ||
                !IsRingButton(__instance, out var ring) ||
                !HasPurchasedToggler())
                return true; // Keep original behavior for global mode and tomes.

            var id = ring.ID;
            if (!string.IsNullOrEmpty(id))
            {
                SetClassRule(_selectedClass.Value, id);
                _refreshToggle?.Invoke(__instance, null);
            }
            return false;
        }

        // Native UI renders the global check; override it for the selected class.
        private static void AfterRefreshToggle(object __instance)
        {
            if (!_selectedClass.HasValue ||
                _toggleCheck == null ||
                !IsRingButton(__instance, out var ring))
                return;

            var check = _toggleCheck.GetValue(__instance) as VisualElement;
            if (check == null)
                return;

            bool banned = Disabled.TryGetValue(_selectedClass.Value, out var ids) && ids.Contains(ring.ID);
            // Game's RefreshToggle displays its check when enabled (not banned).
            check.style.display = banned ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // The level-up draft is generated for GameManager.Instance.LocalPlayer.
        // Filtering here affects the local class only, not another player's rings.
        private static void AfterCreateRingDraftPool(ref List<RogueLikeUpgradeSO> __result)
        {
            if (__result == null || __result.Count == 0 ||
                !TryGetLocalClass(out var cls) || !Disabled.TryGetValue(cls, out var ids) ||
                ids.Count == 0)
                return;

            __result.RemoveAll(ring => ring != null && ids.Contains(ring.ID));
            // Vanilla GenerateThreeChoices already handles an empty draft pool.
        }

        private static bool TryGetLocalClass(out PlayerClass cls)
        {
            cls = default(PlayerClass);
            try
            {
                var gmType = AccessTools.TypeByName("GameManager");
                var gm = AccessTools.Field(gmType, "Instance")?.GetValue(null);
                if (gm == null)
                    return false;

                var player = AccessTools.Property(gmType, "LocalPlayer")?.GetValue(gm, null);
                if (player == null)
                    return false;

                var value = AccessTools.Field(player.GetType(), "playerClass")?.GetValue(player);
                if (!(value is PlayerClass localClass))
                    return false;

                cls = localClass;
                return true;
            }
            catch
            {
                return false; // If the class is unknown, preserve vanilla rewards.
            }
        }

        private static bool HasPurchasedToggler()
        {
            try
            {
                var unlockableType = AccessTools.TypeByName("UnlockableManager");
                var singleton = AccessTools.Property(unlockableType, "Instance")?.GetValue(null, null);
                return singleton != null &&
                       Convert.ToInt32(_getTogglerLevel.Invoke(singleton, null)) > 0;
            }
            catch { return false; }
        }

        internal static void AttachSelector(object menu, VisualElement root,
            Dictionary<string, VisualElement> categoryParents)
        {
            if (root == null || !HasPurchasedToggler() ||
                !categoryParents.TryGetValue("LEVELUP", out var ringGrid) ||
                ringGrid.parent == null)
                return;

            if (_shopRoot != root)
            {
                _shopRoot = root;
                _selector = null;
                SelectorButtons.Clear();
            }

            if (_selector != null && _selector.parent != null)
            {
                RefreshAllRingChecks(root);
                return;
            }

            var container = ringGrid.parent;
            var existing = root.Q<VisualElement>(SelectorName);
            if (existing != null)
                existing.RemoveFromHierarchy();

            _selector = new VisualElement { name = SelectorName };
            _selector.style.flexDirection = FlexDirection.Column;
            _selector.style.marginTop = 5;
            _selector.style.marginBottom = 7;
            _selector.style.paddingTop = 5;
            _selector.style.paddingBottom = 5;
            _selector.style.paddingLeft = 7;
            _selector.style.paddingRight = 7;
            _selector.style.backgroundColor = new Color(0.08f, 0.11f, 0.14f, 0.68f);

            var title = new Label("RING TOGGLER - CLASS");
            title.style.color = new Color(0.93f, 0.88f, 0.70f);
            title.style.fontSize = 12;
            title.style.marginBottom = 3;
            _selector.Add(title);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.alignItems = Align.Center;
            _selector.Add(buttons);

            AddModeButton(buttons, "All classes", null);

            // Limit the list to actual playable archetypes, not sentinel enum values.
            var archetypeField = AccessTools.Field(menu.GetType(), "archetypeDatabaseSo");
            var archetypes = archetypeField?.GetValue(menu) as PlayerArchetypeDatabaseSO;
            foreach (PlayerClass cls in Enum.GetValues(typeof(PlayerClass)))
            {
                if (archetypes != null)
                {
                    try
                    {
                        if (archetypes.GetPlayerArchetype(cls) == null)
                            continue;
                    }
                    catch { continue; }
                }
                AddModeButton(buttons, cls.ToString(), cls);
            }

            var hint = new Label("All classes = global bans. A class tab = additional bans for that class.");
            hint.style.fontSize = 10;
            hint.style.color = new Color(0.74f, 0.76f, 0.78f);
            hint.style.marginTop = 3;
            _selector.Add(hint);

            container.Insert(container.IndexOf(ringGrid), _selector);
            RefreshModeButtons();
            RefreshAllRingChecks(root);
        }

        private static void AddModeButton(VisualElement container, string label, PlayerClass? cls)
        {
            var button = new Button(() =>
            {
                _selectedClass = cls;
                RefreshModeButtons();
                RefreshAllRingChecks(_shopRoot);
            }) { text = label };

            button.style.fontSize = 11;
            button.style.height = 24;
            button.style.marginRight = 3;
            button.style.marginBottom = 3;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.borderBottomWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            container.Add(button);
            SelectorButtons[label] = button;
        }

        private static void RefreshModeButtons()
        {
            foreach (var pair in SelectorButtons)
            {
                bool active = _selectedClass.HasValue
                    ? pair.Key == _selectedClass.Value.ToString()
                    : pair.Key == "All classes";

                pair.Value.style.backgroundColor = active
                    ? new Color(0.45f, 0.34f, 0.18f, 0.96f)
                    : new Color(0.15f, 0.19f, 0.23f, 0.85f);
                pair.Value.style.color = active
                    ? new Color(1f, 0.91f, 0.71f)
                    : new Color(0.85f, 0.88f, 0.89f);

                var border = active
                    ? new Color(0.77f, 0.59f, 0.31f)
                    : new Color(0.31f, 0.34f, 0.37f);
                pair.Value.style.borderTopColor = border;
                pair.Value.style.borderBottomColor = border;
                pair.Value.style.borderLeftColor = border;
                pair.Value.style.borderRightColor = border;
            }
        }

        private static void RefreshAllRingChecks(VisualElement root)
        {
            if (root == null || _nativeButtonType == null || _refreshToggle == null)
                return;

            foreach (var card in root.Query<VisualElement>().ToList())
            {
                if (_nativeButtonType.IsInstanceOfType(card) &&
                    IsRingButton(card, out _))
                {
                    try { _refreshToggle.Invoke(card, null); }
                    catch { /* An uninitialized card can be left to the native UI. */ }
                }
            }
        }
    }
}
