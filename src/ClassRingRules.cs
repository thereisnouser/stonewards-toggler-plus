using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsTogglerPlus
{
    /// <summary>
    /// Class-specific allow/deny overrides for both rings and tomes.
    /// Native global Toggler rules remain in ShopData. A class may override
    /// a global ban, but ONLY within that player's reward-pool generation.
    /// </summary>
    internal static class ClassRingRules
    {
        private const string SelectorName = "stonewards-tp-class-selector";

        private static BaseUnityPlugin _plugin;
        private static ManualLogSource _log;
        private static Type _nativeButtonType;
        private static FieldInfo _buttonUpgrade;
        private static FieldInfo _buttonCanClick;
        private static FieldInfo _buttonManager;
        private static MethodInfo _isGloballyBlacklisted;
        private static FieldInfo _toggleCheck;
        private static MethodInfo _refreshToggle;
        private static MethodInfo _getTogglerLevel;

        private sealed class Rules
        {
            internal ConfigEntry<string> AllowEntry;
            internal ConfigEntry<string> DenyEntry;
            internal HashSet<string> Allowed;
            internal HashSet<string> Denied;
        }

        private static readonly Dictionary<UpgradeCategory, Dictionary<PlayerClass, Rules>> PerClass =
            new Dictionary<UpgradeCategory, Dictionary<PlayerClass, Rules>>();
        private static readonly Dictionary<UpgradeCategory, PlayerClass?> Selected =
            new Dictionary<UpgradeCategory, PlayerClass?>();

        [ThreadStatic] private static UpgradeCategory? _draftScope;
        private static VisualElement _shopRoot;

        internal static void Initialize(BaseUnityPlugin plugin, Harmony harmony, Type nativeButtonType,
            FieldInfo buttonUpgrade, MethodInfo getTogglerLevel)
        {
            _plugin = plugin;
            _log = BepInEx.Logging.Logger.CreateLogSource("StonewardsTogglerPlus.ClassRingRules");
            _nativeButtonType = nativeButtonType;
            _buttonUpgrade = buttonUpgrade;
            _buttonCanClick = AccessTools.Field(nativeButtonType, "canClick");
            _buttonManager = AccessTools.Field(nativeButtonType, "upgradeManager");
            _isGloballyBlacklisted = AccessTools.Method(AccessTools.TypeByName("UpgradeManager"),
                "IsUpgradeBlacklisted", new[] { typeof(string) });
            _getTogglerLevel = getTogglerLevel;
            _toggleCheck = AccessTools.Field(nativeButtonType, "toggleCheck");
            _refreshToggle = AccessTools.Method(nativeButtonType, "RefreshToggle");

            foreach (UpgradeCategory category in new[] { UpgradeCategory.LEVELUP, UpgradeCategory.PICKUP })
            {
                PerClass[category] = new Dictionary<PlayerClass, Rules>();
                Selected[category] = null;
                string noun = category == UpgradeCategory.LEVELUP ? "ring" : "tome";

                foreach (PlayerClass cls in Enum.GetValues(typeof(PlayerClass)))
                {
                    // Preserve existing ring exclusion keys from v0.2.0.
                    var deny = plugin.Config.Bind("Per-class " + noun + " exclusions",
                        cls.ToString(), "", "IDs disabled specifically for this class.");
                    var allow = plugin.Config.Bind("Per-class " + noun + " inclusions",
                        cls.ToString(), "", "IDs explicitly enabled despite the global Toggler.");
                    var rules = new Rules
                    {
                        DenyEntry = deny, AllowEntry = allow,
                        Denied = Deserialize(deny.Value), Allowed = Deserialize(allow.Value)
                    };
                    rules.Denied.ExceptWith(rules.Allowed);
                    PerClass[category][cls] = rules;
                }
            }

            Patch(harmony, AccessTools.Method(nativeButtonType, "OnToggleClick", new[] { typeof(ClickEvent) }),
                nameof(BeforeToggleClick), prefix: true);
            Patch(harmony, _refreshToggle, nameof(AfterRefreshToggle), prefix: false);

            var rogue = AccessTools.TypeByName("RogueLikeUpgradeManager");
            PatchDraft(harmony, AccessTools.Method(rogue, "CreateValidLevelupDraftPool"), nameof(EnterRingDraft));
            PatchDraft(harmony, AccessTools.Method(rogue, "CreateValidPickupDraftPool"), nameof(EnterTomeDraft));
            Patch(harmony, AccessTools.Method(AccessTools.TypeByName("UpgradeManager"),
                "IsUpgradeBlacklisted", new[] { typeof(RogueLikeUpgradeSO) }),
                nameof(AfterBlacklistedCheck), prefix: false);

            _log.LogInfo("Class ring/tome allow/deny overrides initialized.");
        }

        private static void Patch(Harmony harmony, MethodInfo target, string handler, bool prefix)
        {
            if (target == null)
            {
                _log.LogWarning("Class-ring patch target missing: " + handler);
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
                _log.LogError("Could not install " + handler + ": " + ex);
            }
        }

        private static void PatchDraft(Harmony harmony, MethodInfo method, string prefixName)
        {
            if (method == null) { _log.LogWarning("Draft method missing: " + prefixName); return; }
            try
            {
                harmony.Patch(method,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ClassRingRules), prefixName)),
                    finalizer: new HarmonyMethod(AccessTools.Method(typeof(ClassRingRules), nameof(ExitDraft))));
            }
            catch (Exception ex) { _log.LogError("Draft patch failed: " + ex); }
        }

        private static void EnterRingDraft(out UpgradeCategory? __state)
        {
            __state = _draftScope;
            _draftScope = UpgradeCategory.LEVELUP;
        }

        private static void EnterTomeDraft(out UpgradeCategory? __state)
        {
            __state = _draftScope;
            _draftScope = UpgradeCategory.PICKUP;
        }

        private static Exception ExitDraft(Exception __exception, UpgradeCategory? __state)
        {
            _draftScope = __state;
            return __exception;
        }

        // Override the actual blacklist decision while the game constructs the pool.
        // Post-filtering a pool cannot re-enable a globally blacklisted item.
        private static void AfterBlacklistedCheck(RogueLikeUpgradeSO upgradeSO, ref bool __result)
        {
            if (upgradeSO == null || !_draftScope.HasValue ||
                upgradeSO.UpgradeCategory != _draftScope.Value ||
                !TryGetLocalClass(out var cls) ||
                !PerClass.TryGetValue(_draftScope.Value, out var byClass) ||
                !byClass.TryGetValue(cls, out var rules))
                return;

            if (rules.Allowed.Contains(upgradeSO.ID)) __result = false;
            else if (rules.Denied.Contains(upgradeSO.ID)) __result = true;
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

        private static bool FindRules(UpgradeCategory cat, PlayerClass cls, out Rules rules)
        {
            rules = null;
            return PerClass.TryGetValue(cat, out var byClass) && byClass.TryGetValue(cls, out rules);
        }

        private static bool EffectiveAllowed(UpgradeCategory cat, PlayerClass cls,
            string id, bool globalBlacklisted)
        {
            if (FindRules(cat, cls, out var rules))
            {
                if (rules.Allowed.Contains(id)) return true;
                if (rules.Denied.Contains(id)) return false;
            }
            return !globalBlacklisted;
        }

        private static void SetClassRule(UpgradeCategory cat, PlayerClass cls, string id, bool allowed)
        {
            if (!FindRules(cat, cls, out var rules)) return;
            if (allowed) { rules.Denied.Remove(id); rules.Allowed.Add(id); }
            else { rules.Allowed.Remove(id); rules.Denied.Add(id); }
            rules.AllowEntry.Value = Serialize(rules.Allowed);
            rules.DenyEntry.Value = Serialize(rules.Denied);
            _plugin.Config.Save();
        }

        private static string Serialize(HashSet<string> ids)
        {
            return string.Join(",", ids.OrderBy(id => id, StringComparer.Ordinal)
                .Select(Uri.EscapeDataString));
        }

        private static bool IsUpgradeButton(object button, out RogueLikeUpgradeSO upgrade)
        {
            upgrade = _buttonUpgrade?.GetValue(button) as RogueLikeUpgradeSO;
            return upgrade != null && (upgrade.UpgradeCategory == UpgradeCategory.LEVELUP ||
                upgrade.UpgradeCategory == UpgradeCategory.PICKUP);
        }

        private static bool BeforeToggleClick(object __instance)
        {
            if (!IsUpgradeButton(__instance, out var upgrade) || !HasPurchasedToggler())
                return true;

            var category = upgrade.UpgradeCategory;
            if (!Selected.TryGetValue(category, out var selected) || !selected.HasValue)
                return true;

            var id = upgrade.ID;
            if (string.IsNullOrEmpty(id)) return false;
            bool current = EffectiveAllowed(category, selected.Value, id,
                IsGloballyBlacklisted(__instance, id));
            SetClassRule(category, selected.Value, id, !current);
            _refreshToggle?.Invoke(__instance, null);
            return false;
        }

        private static void AfterRefreshToggle(object __instance)
        {
            if (_toggleCheck == null || !IsUpgradeButton(__instance, out var upgrade))
                return;
            var category = upgrade.UpgradeCategory;
            if (!Selected.TryGetValue(category, out var selected) || !selected.HasValue)
                return;
            var check = _toggleCheck.GetValue(__instance) as VisualElement;
            if (check == null) return;

            bool allowed = EffectiveAllowed(category, selected.Value, upgrade.ID,
                IsGloballyBlacklisted(__instance, upgrade.ID));
            check.style.display = allowed ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool IsGloballyBlacklisted(object button, string id)
        {
            if (button == null || string.IsNullOrEmpty(id) ||
                _buttonManager == null || _isGloballyBlacklisted == null)
                return false;
            try
            {
                var manager = _buttonManager.GetValue(button);
                return manager != null &&
                    Convert.ToBoolean(_isGloballyBlacklisted.Invoke(manager, new object[] { id }));
            }
            catch { return false; }
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

        internal static void AttachSelector(object menu, VisualElement root)
        {
            if (root == null || !HasPurchasedToggler())
                return;

            // Discover the existing ring tab from real ring cards. The upgrade
            // category is LEVELUP; IUnlockable.Category is a DIFFERENT enum.
            VisualElement ringGrid = null;
            foreach (var card in root.Query<VisualElement>().ToList())
            {
                if (_nativeButtonType == null || !_nativeButtonType.IsInstanceOfType(card)
                    || card.parent == null || !IsRingButton(card, out _))
                    continue;

                if (_buttonCanClick != null &&
                    _buttonCanClick.GetValue(card) is bool canClick && !canClick)
                    continue; // Ignore the preview card below the shop grid.

                ringGrid = card.parent;
                break;
            }

            if (ringGrid == null || ringGrid.parent == null)
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
            _selector.style.marginTop = 8;
            _selector.style.marginBottom = 12;
            _selector.style.paddingTop = 7;
            _selector.style.paddingBottom = 10;
            _selector.style.paddingLeft = 6;
            _selector.style.paddingRight = 6;
            // Match the native shop: no separate opaque "debug panel".
            _selector.style.backgroundColor = Color.clear;
            _selector.style.borderBottomWidth = 1;
            _selector.style.borderBottomColor = new Color(0.53f, 0.48f, 0.34f, 0.9f);

            var title = new Label("RING TOGGLER - CLASS");
            title.style.color = new Color(0.97f, 0.82f, 0.52f, 1f);
            title.style.fontSize = 19;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            _selector.Add(title);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.alignItems = Align.Center;
            buttons.style.marginBottom = 2;
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

            var hint = new Label("All classes: global bans  |  Class: additional bans");
            hint.style.fontSize = 13;
            hint.style.color = new Color(0.79f, 0.81f, 0.82f, 1f);
            hint.style.marginTop = 5;
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

            // In-game tab proportions and legibility at 1080p and 1440p.
            button.style.fontSize = 16;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.height = 38;
            button.style.minWidth = 70;
            button.style.marginRight = 5;
            button.style.marginBottom = 5;
            button.style.paddingLeft = 11;
            button.style.paddingRight = 11;
            button.style.paddingTop = 3;
            button.style.paddingBottom = 3;
            button.style.borderBottomWidth = 2;
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

                // Native Stonewards shop palette: slate-gray tabs with a dark,
                // warm-highlighted selected state, rather than generic UI buttons.
                pair.Value.style.backgroundColor = active
                    ? new Color(0.09f, 0.11f, 0.15f, 1f)
                    : new Color(0.23f, 0.26f, 0.31f, 1f);
                pair.Value.style.color = active
                    ? new Color(1f, 0.90f, 0.69f, 1f)
                    : new Color(0.96f, 0.96f, 0.94f, 1f);

                var border = active
                    ? new Color(0.90f, 0.72f, 0.42f, 1f)
                    : new Color(0.48f, 0.48f, 0.44f, 1f);
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
