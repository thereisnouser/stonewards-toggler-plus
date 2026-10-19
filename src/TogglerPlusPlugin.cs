using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace StonewardsTogglerPlus
{
    [BepInPlugin(PluginGuid, "Stonewards Toggler Plus", "0.2.1")]
    public sealed class TogglerPlusPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.thereisnouser.stonewards.togglerplus";
        private const string AddedClass = "stonewards-tp-default";

        private static TogglerPlusPlugin _plugin;
        private Harmony _harmony;

        private static Type _menuType;
        private static Type _buttonType;
        private static Type _upgradeCategoryType;

        private static FieldInfo _menuManager;
        private static FieldInfo _menuTemplate;
        private static FieldInfo _menuArchetypes;
        private static FieldInfo _buttonUnlockable;
        private static FieldInfo _buttonCanClick;

        private static MethodInfo _getAvailable;
        private static MethodInfo _buttonInit;
        private static MethodInfo _buttonSetUpgrade;
        private static MethodInfo _buttonEnableToggle;
        private static MethodInfo _showUnlockable;
        private static MethodInfo _getTogglerLevel;

        private static readonly HashSet<string> Warnings = new HashSet<string>();

        private void Awake()
        {
            _plugin = this;
            _menuType = AccessTools.TypeByName("UnlockableMenu");
            _buttonType = AccessTools.TypeByName("UnlockUpgradeButton");
            _upgradeCategoryType = AccessTools.TypeByName("UpgradeCategory");

            Type managerType = AccessTools.TypeByName("UpgradeManager");
            Type unlockableManagerType = AccessTools.TypeByName("UnlockableManager");

            if (_menuType == null || _buttonType == null || _upgradeCategoryType == null ||
                managerType == null || unlockableManagerType == null)
            {
                Logger.LogError("Stonewards upgrade types not found. Toggler Plus disabled for this game build.");
                return;
            }

            _menuManager = AccessTools.Field(_menuType, "upgradeManager");
            _menuTemplate = AccessTools.Field(_menuType, "unlockableButtonTemplate");
            _menuArchetypes = AccessTools.Field(_menuType, "archetypeDatabaseSo");
            _buttonUnlockable = AccessTools.Field(_buttonType, "unlockable");
            _buttonCanClick = AccessTools.Field(_buttonType, "canClick");

            _getAvailable = AccessTools.Method(managerType, "GetAvailableUpgrades");
            _buttonInit = AccessTools.Method(_buttonType, "Init");
            _buttonSetUpgrade = AccessTools.Method(_buttonType, "SetUpgrade");
            _buttonEnableToggle = AccessTools.Method(_buttonType, "EnableToggle");
            _showUnlockable = AccessTools.Method(_menuType, "ShowUnlockable");
            _getTogglerLevel = AccessTools.Method(unlockableManagerType, "GetUpgradableTogglerLevel");

            if (_menuManager == null || _menuTemplate == null || _menuArchetypes == null ||
                _buttonUnlockable == null || _buttonCanClick == null || _getAvailable == null ||
                _buttonInit == null || _buttonSetUpgrade == null || _buttonEnableToggle == null ||
                _showUnlockable == null || _getTogglerLevel == null)
            {
                Logger.LogError("Upgrade UI API has changed; no UI patches installed.");
                return;
            }

            _harmony = new Harmony(PluginGuid);
            Patch("InitUI", nameof(AfterMenuBuilt), null);
            Patch("RebuildShopButtons", nameof(AfterMenuBuilt), null);
            Patch("Show", nameof(AfterShow), new[] { typeof(bool) });

            ClassRingRules.Initialize(this, _harmony, _buttonType, _buttonUnlockable, _getTogglerLevel);

            Logger.LogInfo("Toggler Plus v0.2.0 initialized. Per-class ring/tome overrides are enabled.");
        }

        private void Patch(string methodName, string postfixName, Type[] arguments)
        {
            try
            {
                var method = arguments == null
                    ? AccessTools.Method(_menuType, methodName)
                    : AccessTools.Method(_menuType, methodName, arguments);

                if (method == null)
                {
                    WarnOnce("patch:" + methodName, "Method not found: UnlockableMenu." + methodName);
                    return;
                }

                var postfix = AccessTools.Method(typeof(TogglerPlusPlugin), postfixName);
                _harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                Logger.LogInfo("Patched UnlockableMenu." + methodName);
            }
            catch (Exception ex)
            {
                WarnOnce("patch:" + methodName, "Could not patch UnlockableMenu." + methodName + ": " + ex);
            }
        }

        private static void AfterMenuBuilt(object __instance)
        {
            TryAppendDefaults(__instance);
        }

        private static void AfterShow(object __instance, bool _Show)
        {
            if (_Show)
                TryAppendDefaults(__instance);
        }

        private static void TryAppendDefaults(object menu)
        {
            if (_plugin == null || menu == null)
                return;

            try
            {
                var component = menu as Component;
                var document = component != null ? component.GetComponent<UIDocument>() : null;
                var root = document != null ? document.rootVisualElement : null;
                var manager = _menuManager.GetValue(menu);
                var template = _menuTemplate.GetValue(menu) as VisualTreeAsset;

                if (root == null || manager == null || template == null)
                    return;

                // Find the actual vanilla ring/tome grid. Never guess the parent
                // container by name, because UXML layouts may vary between versions.
                var categoryParents = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
                var alreadyPresent = new HashSet<string>(StringComparer.Ordinal);
                var nativeButtons = root.Query<VisualElement>().ToList();

                foreach (var element in nativeButtons)
                {
                    if (!_buttonType.IsInstanceOfType(element) || element.parent == null)
                        continue;

                    var attachedUpgrade = _buttonUnlockable.GetValue(element);
                    var id = ReadString(attachedUpgrade, "ID");
                    if (!string.IsNullOrEmpty(id))
                        alreadyPresent.Add(id);

                    if (element.ClassListContains(AddedClass))
                    {
                        RefreshAddedToggle(element);
                        continue;
                    }

                    if (!(_buttonCanClick.GetValue(element) is bool canClick) || !canClick)
                        continue; // Selected upgrade preview is not a shop-list button.

                    // Handle game versions that already show defaults but hide their
                    // native toggle. This also avoids adding a duplicate card.
                    if (attachedUpgrade != null &&
                        ReadString(attachedUpgrade, "UpgradeCategory") != null &&
                        !ReadBool(attachedUpgrade, "Unlockable", true) &&
                        !ReadBool(attachedUpgrade, "HiddenUpgrade", false))
                        RefreshAddedToggle(element);

                    // UpgradeCategory is LEVELUP/PICKUP. IUnlockable.Category
                    // is a different enum and cannot identify the ring/tome tab.
                    var category = ReadString(attachedUpgrade, "UpgradeCategory");
                    if ((category == "LEVELUP" || category == "PICKUP") &&
                        !categoryParents.ContainsKey(category))
                        categoryParents.Add(category, element.parent);
                }

                AppendCategory(menu, manager, template, categoryParents, alreadyPresent, 0, "LEVELUP");
                AppendCategory(menu, manager, template, categoryParents, alreadyPresent, 1, "PICKUP");

                ClassRingRules.AttachSelector(menu, root);
            }
            catch (Exception ex)
            {
                WarnOnce("inject", "Failed to add default upgrade buttons: " + ex);
            }
        }

        private static void AppendCategory(
            object menu, object manager, VisualTreeAsset template,
            Dictionary<string, VisualElement> categoryParents,
            HashSet<string> alreadyPresent, int categoryValue, string categoryName)
        {
            if (!categoryParents.TryGetValue(categoryName, out var parent))
            {
                WarnOnce("missing:" + categoryName,
                    "No existing " + categoryName + " button to locate its vanilla grid. " +
                    "Try opening the unlock shop with some upgrades in this category.");
                return;
            }

            var category = Enum.ToObject(_upgradeCategoryType, categoryValue);
            var upgrades = _getAvailable.Invoke(manager, new[] { category }) as IEnumerable;
            if (upgrades == null)
                return;

            var archetypes = _menuArchetypes.GetValue(menu);
            int count = 0;

            foreach (var upgrade in upgrades)
            {
                if (upgrade == null || ReadBool(upgrade, "Unlockable", true) ||
                    ReadBool(upgrade, "HiddenUpgrade", false))
                    continue; // Only default, visible upgrades.

                var id = ReadString(upgrade, "ID");
                if (string.IsNullOrEmpty(id) || alreadyPresent.Contains(id))
                    continue;

                // Construct the *actual* game's own card and clone the game's UXML.
                // Its OnToggleClick invokes UpgradeManager.ToggleUpgradeAvailability,
                // using ShopData's normal persistent blacklist.
                var card = Activator.CreateInstance(_buttonType, true) as VisualElement;
                if (card == null)
                    continue;

                template.CloneTree(card);
                _buttonInit.Invoke(card, new object[] { manager, archetypes, true });
                _buttonSetUpgrade.Invoke(card, new[] { upgrade });
                card.AddToClassList(AddedClass);

                // Vanilla SetUpgrade hides the checkbox for Unlockable=false.
                // We make it available only after purchasing the real Toggler.
                RefreshAddedToggle(card);

                // The native shop normally subscribes to OnClicked/OnSubmitted.
                // Our dynamically added cards select the upgrade through the
                // same native ShowUnlockable method.
                var currentCard = card;
                var currentUpgrade = upgrade;
                card.RegisterCallback<ClickEvent>(_ => ShowDetails(menu, currentUpgrade, currentCard));
                card.RegisterCallback<NavigationSubmitEvent>(_ => ShowDetails(menu, currentUpgrade, currentCard));

                parent.Add(card);
                alreadyPresent.Add(id);
                count++;
            }

            if (count > 0)
                _plugin.Logger.LogInfo("Added " + count + " default " + categoryName + " upgrades.");
        }

        private static void ShowDetails(object menu, object upgrade, VisualElement card)
        {
            try
            {
                _showUnlockable.Invoke(menu, new[] { upgrade, (object)card });
            }
            catch (Exception ex)
            {
                WarnOnce("details", "Could not select default upgrade: " + ex);
            }
        }

        private static void RefreshAddedToggle(VisualElement card)
        {
            try
            {
                var unlockableManagerType = AccessTools.TypeByName("UnlockableManager");
                var singleton = AccessTools.Property(unlockableManagerType, "Instance")?.GetValue(null, null);
                bool canClick = _buttonCanClick.GetValue(card) is bool value && value;
                bool enabled = canClick && singleton != null &&
                               Convert.ToInt32(_getTogglerLevel.Invoke(singleton, null)) > 0;
                _buttonEnableToggle.Invoke(card, new object[] { enabled });
            }
            catch (Exception ex)
            {
                WarnOnce("toggle", "Could not refresh default toggle: " + ex);
            }
        }

        private static string ReadString(object obj, string propertyName)
        {
            if (obj == null)
                return null;

            var property = AccessTools.Property(obj.GetType(), propertyName);
            return property?.GetValue(obj, null)?.ToString();
        }

        private static bool ReadBool(object obj, string propertyName, bool fallback)
        {
            if (obj == null)
                return fallback;

            var property = AccessTools.Property(obj.GetType(), propertyName);
            var value = property?.GetValue(obj, null);
            return value is bool result ? result : fallback;
        }

        private static void WarnOnce(string key, string message)
        {
            if (_plugin != null && Warnings.Add(key))
                _plugin.Logger.LogWarning(message);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _plugin = null;
        }
    }
}
