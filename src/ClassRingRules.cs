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
        private static MethodInfo _isClassValid;
        private static bool _warnedClassValidity;

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

        // Thread-local draft context: determined ONCE per draft, not once per
        // UpgradeManager.IsUpgradeBlacklisted call (which runs for every item).
        private struct DraftState
        {
            internal UpgradeCategory? PreviousScope;
            internal PlayerClass? PreviousPlayerClass;
        }

        [ThreadStatic] private static UpgradeCategory? _draftScope;
        [ThreadStatic] private static PlayerClass? _draftPlayerClass;

        private static FieldInfo _gameManagerInstance;
        private static PropertyInfo _localPlayerProperty;
        private static FieldInfo _localPlayerClassField;
        private static VisualElement _shopRoot;

        private sealed class PixelTab
        {
            internal Button Control;
            internal readonly List<VisualElement> Frame = new List<VisualElement>();
            internal readonly List<VisualElement> Surface = new List<VisualElement>();
            internal Label Caption;
        }

        private sealed class SelectorPanel
        {
            internal VisualElement Root;
            internal VisualElement Grid;
            internal readonly Dictionary<string, PixelTab> Tabs =
                new Dictionary<string, PixelTab>(StringComparer.Ordinal);
        }

        private static readonly Dictionary<UpgradeCategory, SelectorPanel> Panels =
            new Dictionary<UpgradeCategory, SelectorPanel>();

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
            // Use the same class eligibility check as CreateValidLevelupDraftPool.
            _isClassValid = AccessTools.Method(typeof(RogueLikeUpgradeSO),
                "IsClassValid", new[] { typeof(PlayerClass) });

            // Resolve reflection metadata only at initialization. The player
            // instance itself is read once at the start of each reward draft.
            var gameManagerType = AccessTools.TypeByName("GameManager");
            if (gameManagerType != null)
            {
                _gameManagerInstance = AccessTools.Field(gameManagerType, "Instance");
                _localPlayerProperty = AccessTools.Property(gameManagerType, "LocalPlayer");
            }
            var playerType = AccessTools.TypeByName("FirstPersonController");
            if (playerType != null)
                _localPlayerClassField = AccessTools.Field(playerType, "playerClass");

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

        private static void EnterRingDraft(out DraftState __state)
        {
            EnterDraft(UpgradeCategory.LEVELUP, out __state);
        }

        private static void EnterTomeDraft(out DraftState __state)
        {
            EnterDraft(UpgradeCategory.PICKUP, out __state);
        }

        private static void EnterDraft(UpgradeCategory category, out DraftState state)
        {
            state = new DraftState
            {
                PreviousScope = _draftScope,
                PreviousPlayerClass = _draftPlayerClass
            };

            _draftScope = category;
            _draftPlayerClass = TryGetLocalClass(out var cls) ? cls : (PlayerClass?)null;
        }

        // Harmony finalizer restores the previous context even on exceptions.
        private static Exception ExitDraft(Exception __exception, DraftState __state)
        {
            _draftScope = __state.PreviousScope;
            _draftPlayerClass = __state.PreviousPlayerClass;
            return __exception;
        }

        // Override the actual blacklist decision while the game constructs the pool.
        // Do NOT perform GameManager or PlayerClass reflection in this hot path.
        private static void AfterBlacklistedCheck(RogueLikeUpgradeSO upgradeSO, ref bool __result)
        {
            if (upgradeSO == null || !_draftScope.HasValue ||
                !_draftPlayerClass.HasValue ||
                upgradeSO.UpgradeCategory != _draftScope.Value ||
                !PerClass.TryGetValue(_draftScope.Value, out var byClass) ||
                !byClass.TryGetValue(_draftPlayerClass.Value, out var rules))
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
            if (_gameManagerInstance == null || _localPlayerProperty == null ||
                _localPlayerClassField == null)
                return false;

            try
            {
                var manager = _gameManagerInstance.GetValue(null);
                if (manager == null)
                    return false;

                var player = _localPlayerProperty.GetValue(manager, null);
                if (player == null)
                    return false;

                var value = _localPlayerClassField.GetValue(player);
                if (!(value is PlayerClass playerClass))
                    return false;

                cls = playerClass;
                return true;
            }
            catch
            {
                return false; // Missing local player: preserve the game's original rules.
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

        // Two independent selectors: one belongs to the Rings tab, one to Tomes.
        internal static void AttachSelector(object menu, VisualElement root)
        {
            if (root == null || !HasPurchasedToggler())
                return;

            if (_shopRoot != root)
            {
                _shopRoot = root;
                Panels.Clear();
                Selected[UpgradeCategory.LEVELUP] = null;
                Selected[UpgradeCategory.PICKUP] = null;
            }

            foreach (var category in new[] { UpgradeCategory.LEVELUP, UpgradeCategory.PICKUP })
            {
                VisualElement grid = FindCategoryGrid(root, category);
                if (grid == null || grid.parent == null)
                    continue;

                if (Panels.TryGetValue(category, out var oldPanel) &&
                    oldPanel.Root.parent == grid.parent &&
                    oldPanel.Grid == grid)
                {
                    RefreshCategoryChecks(root, category);
                    continue;
                }

                if (oldPanel != null && oldPanel.Root.parent != null)
                    oldPanel.Root.RemoveFromHierarchy();

                var stale = root.Q<VisualElement>(SelectorName + "-" + category);
                if (stale != null) stale.RemoveFromHierarchy();

                var panel = new SelectorPanel { Grid = grid };
                var frame = new VisualElement { name = SelectorName + "-" + category };
                panel.Root = frame;
                frame.style.flexDirection = FlexDirection.Column;
                frame.style.marginTop = 9;
                frame.style.marginBottom = 13;
                frame.style.paddingTop = 8;
                frame.style.paddingBottom = 11;
                frame.style.paddingLeft = 3;
                frame.style.paddingRight = 3;
                frame.style.backgroundColor = Color.clear;
                frame.style.borderBottomWidth = 2;
                frame.style.borderBottomColor = new Color(0.55f, 0.48f, 0.34f, 1f);

                var title = new Label(category == UpgradeCategory.LEVELUP
                    ? "RINGS - CLASS FILTER" : "TOMES - CLASS FILTER");
                title.style.color = new Color(0.99f, 0.84f, 0.55f, 1f);
                title.style.fontSize = 23;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.marginBottom = 10;
                frame.Add(title);

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.flexWrap = Wrap.Wrap;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4;
                frame.Add(row);

                AddModeButton(panel, row, category, "ALL", null);

                var archetypeField = AccessTools.Field(menu.GetType(), "archetypeDatabaseSo");
                var archetypes = archetypeField?.GetValue(menu) as PlayerArchetypeDatabaseSO;
                foreach (PlayerClass cls in Enum.GetValues(typeof(PlayerClass)))
                {
                    // Use the SAME character sprite used by the game's ring
                    // exclusivity icons (PlayerArchetypeSO.Icon).
                    Sprite classIcon = null;
                    if (archetypes != null)
                    {
                        try
                        {
                            var archetype = archetypes.GetPlayerArchetype(cls);
                            if (archetype == null)
                                continue;
                            classIcon = archetype.Icon;
                        }
                        catch { continue; }
                    }
                    AddModeButton(panel, row, category, cls.ToString(), cls, classIcon);
                }

                var hint = new Label("ALL: default  |  Class: override");
                hint.style.fontSize = 15;
                hint.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                hint.style.marginTop = 6;
                frame.Add(hint);

                var container = grid.parent;
                container.Insert(container.IndexOf(grid), frame);
                Panels[category] = panel;
                RefreshModeButtons(category);
                RefreshCategoryChecks(root, category);
            }
        }

        private static VisualElement FindCategoryGrid(VisualElement root, UpgradeCategory category)
        {
            foreach (var card in root.Query<VisualElement>().ToList())
            {
                if (_nativeButtonType == null || !_nativeButtonType.IsInstanceOfType(card) ||
                    card.parent == null || !IsUpgradeButton(card, out var upgrade) ||
                    upgrade.UpgradeCategory != category)
                    continue;

                if (_buttonCanClick != null &&
                    _buttonCanClick.GetValue(card) is bool clickable && !clickable)
                    continue; // Details preview, not the main item grid.

                return card.parent;
            }
            return null;
        }

        private static void AddModeButton(SelectorPanel panel, VisualElement row,
            UpgradeCategory category, string text, PlayerClass? cls, Sprite icon = null)
        {
            // Draw a REAL stepped pixel border. UI Toolkit borderRadius produces
            // smooth curves, so construct five horizontal strips at stepped insets
            // (plus five inner strips) instead of square or anti-aliased corners.
            var button = new Button(() =>
            {
                Selected[category] = cls;
                RefreshModeButtons(category);
                RefreshCategoryChecks(_shopRoot, category);
            });
            button.text = string.Empty;
            button.RemoveFromClassList("unity-button");
            button.style.position = Position.Relative;
            button.style.height = 46;
            // Budget for the sprite + label; wrapping remains enabled for small screens.
            button.style.width = cls.HasValue
                ? Math.Max(104, text.Length * 11 + (icon != null ? 49 : 26))
                : 86;
            button.style.marginRight = 6;
            button.style.marginBottom = 6;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.backgroundColor = Color.clear;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;

            var tab = new PixelTab { Control = button };
            // Border silhouette: top 12px inset, shoulder 6px inset, flat sides.
            tab.Frame.Add(Slice(button, 12, 12, 0, -1, 4));
            tab.Frame.Add(Slice(button, 6, 6, 4, -1, 5));
            tab.Frame.Add(Slice(button, 0, 0, 9, 9, -1));
            tab.Frame.Add(Slice(button, 6, 6, -1, 4, 5));
            tab.Frame.Add(Slice(button, 12, 12, -1, 0, 4));
            // The inset silhouette leaves a crisp 2px border throughout.
            tab.Surface.Add(Slice(button, 13, 13, 2, -1, 4));
            tab.Surface.Add(Slice(button, 8, 8, 6, -1, 5));
            tab.Surface.Add(Slice(button, 2, 2, 11, 11, -1));
            tab.Surface.Add(Slice(button, 8, 8, -1, 6, 5));
            tab.Surface.Add(Slice(button, 13, 13, -1, 2, 4));

            // Center icon and text together, not independently. Child elements
            // ignore picking so the full stepped tab remains clickable.
            var content = new VisualElement { pickingMode = PickingMode.Ignore };
            content.style.position = Position.Absolute;
            content.style.left = 7;
            content.style.right = 7;
            content.style.top = 0;
            content.style.bottom = 0;
            content.style.flexDirection = FlexDirection.Row;
            content.style.alignItems = Align.Center;
            content.style.justifyContent = Justify.Center;
            button.Add(content);

            if (cls.HasValue && icon != null)
            {
                var classIcon = new VisualElement { pickingMode = PickingMode.Ignore };
                classIcon.style.width = 26;
                classIcon.style.height = 26;
                classIcon.style.flexShrink = 0;
                classIcon.style.marginRight = 5;
                classIcon.style.backgroundImage = new StyleBackground(icon);
                content.Add(classIcon);
            }

            var caption = new Label(text);
            caption.pickingMode = PickingMode.Ignore;
            caption.style.fontSize = 19;
            caption.style.unityFontStyleAndWeight = FontStyle.Bold;
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            caption.style.flexShrink = 0;
            content.Add(caption);
            tab.Caption = caption;

            row.Add(button);
            panel.Tabs[text] = tab;
        }

        // -1 means the edge is unconstrained. A fixed height is used only for
        // horizontal edge strips; middle strips stretch with the button.
        private static VisualElement Slice(VisualElement parent, float left, float right,
            float top, float bottom, float height)
        {
            var slice = new VisualElement { pickingMode = PickingMode.Ignore };
            slice.style.position = Position.Absolute;
            slice.style.left = left;
            slice.style.right = right;
            if (top >= 0) slice.style.top = top;
            if (bottom >= 0) slice.style.bottom = bottom;
            if (height >= 0) slice.style.height = height;
            parent.Add(slice);
            return slice;
        }

        private static void RefreshModeButtons(UpgradeCategory category)
        {
            if (!Panels.TryGetValue(category, out var panel))
                return;

            Selected.TryGetValue(category, out var selected);
            foreach (var item in panel.Tabs)
            {
                bool active = selected.HasValue
                    ? item.Key == selected.Value.ToString()
                    : item.Key == "ALL";

                var tab = item.Value;
                Color border = active
                    ? new Color(0.95f, 0.73f, 0.36f, 1f)
                    : new Color(0.54f, 0.47f, 0.34f, 1f);
                Color fill = active
                    ? new Color(0.13f, 0.17f, 0.22f, 1f)
                    : new Color(0.28f, 0.32f, 0.37f, 1f);
                foreach (var part in tab.Frame) part.style.backgroundColor = border;
                foreach (var part in tab.Surface) part.style.backgroundColor = fill;
                tab.Caption.style.color = active
                    ? new Color(1f, 0.87f, 0.59f, 1f)
                    : new Color(0.96f, 0.96f, 0.93f, 1f);
            }
        }

        private static bool IsAvailableToClass(RogueLikeUpgradeSO upgrade, PlayerClass cls)
        {
            // If a game update removes the API, keep the full list visible
            // instead of incorrectly hiding rings or deleting any settings.
            if (_isClassValid == null)
            {
                if (!_warnedClassValidity)
                {
                    _log.LogWarning("RogueLikeUpgradeSO.IsClassValid missing; class ring visibility unchanged.");
                    _warnedClassValidity = true;
                }
                return true;
            }

            try
            {
                return Convert.ToBoolean(_isClassValid.Invoke(upgrade, new object[] { cls }));
            }
            catch (Exception ex)
            {
                if (!_warnedClassValidity)
                {
                    _log.LogWarning("Could not check class ring eligibility: " + ex);
                    _warnedClassValidity = true;
                }
                return true;
            }
        }

        private static void RefreshCategoryChecks(VisualElement root, UpgradeCategory category)
        {
            if (root == null || _nativeButtonType == null || _refreshToggle == null)
                return;

            // Hide incompatible rings ONLY in class-specific tabs. The existing
            // ring grid contains item cards; the details preview lives elsewhere.
            // Never delete cards or alter upgrade data, blacklist or save state.
            if (category == UpgradeCategory.LEVELUP &&
                Panels.TryGetValue(category, out var panel) &&
                panel.Grid != null)
            {
                Selected.TryGetValue(category, out var selected);
                foreach (var element in panel.Grid.Children())
                {
                    if (_nativeButtonType.IsInstanceOfType(element) &&
                        IsUpgradeButton(element, out var ring) &&
                        ring.UpgradeCategory == UpgradeCategory.LEVELUP)
                    {
                        bool visible = !selected.HasValue || IsAvailableToClass(ring, selected.Value);
                        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                }
            }

            foreach (var card in root.Query<VisualElement>().ToList())
            {
                if (_nativeButtonType.IsInstanceOfType(card) &&
                    IsUpgradeButton(card, out var upgrade) &&
                    upgrade.UpgradeCategory == category)
                {
                    try { _refreshToggle.Invoke(card, null); }
                    catch { /* Preview cards may not be initialized yet. */ }
                }
            }
        }
    }
}
