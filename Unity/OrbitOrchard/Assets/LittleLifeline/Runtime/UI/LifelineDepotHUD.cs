using System;
using System.Linq;
using OrbitOrchard.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace LittleLifeline.App
{
    public sealed partial class LifelineApp
    {
        private bool hudTipsExpanded;
        private bool hudStoreTouched;
        private string hudObservedStoreState = "";
        private double hudStoreMessageUntil;
        private static readonly string[] HudLiveryLabels = { "Willow", "Sunrise", "Coastal", "Heritage" };

        private void BuildWardrobeHUD()
        {
            var dock = Dock(pageBody);
            dock.AddToClassList("wardrobe-hud");
            dock.style.paddingTop = 8; dock.style.paddingBottom = 8;
            if (Array.IndexOf(LiveryIds, previewLivery) < 0) previewLivery = ActiveLivery;
            var swatches = Row(dock, "hud-swatches");
            for (var i = 0; i < LiveryIds.Length; i++)
            {
                var id = LiveryIds[i]; var index = i;
                var choice = IconAction(swatches, GameGlyph.Train, "Preview " + LiveryNames[i] + (i == 0 ? " · included" : " · collection finish"), () =>
                {
                    previewLivery = id;
                    world.Focus(-1, ReducedMotion);
                    BuildScreen();
                }, HudLiveryLabels[i]);
                choice.style.flexGrow = 1; choice.style.flexBasis = 0; choice.style.minWidth = 0;
                choice.style.height = 52; choice.style.flexDirection = FlexDirection.Column;
                choice.style.paddingLeft = 2; choice.style.paddingRight = 2;
                choice.EnableInClassList("active", previewLivery == id);
                var mark = choice.Q<LifelineIcon>();
                if (mark != null) mark.Tint = LiverySwatch(index);
            }

            var explanation = Text(dock, "Cosmetic collection · permanent", "hud-note");
            explanation.style.fontSize = 14; explanation.style.marginTop = 3; explanation.style.marginBottom = 5;
            explanation.tooltip = "The Founder’s Carriage Collection includes Sunrise, Coastal and Heritage. It never changes care, income or rankings. Previous Plus purchases retain access.";
            var actions = Row(dock, "hud-store-actions");
            var ownsPreview = previewLivery == "base" || apple.IsPassOwned;
            if (ownsPreview && (apple.IsPassOwned || ActiveLivery != previewLivery))
            {
                var use = IconAction(actions, GameGlyph.Check, "Apply the previewed finish to your train", () =>
                {
                    // Entitlements may change between drawing this button and its click.
                    if (previewLivery != "base" && !apple.IsPassOwned) { BuildScreen(); return; }
                    profile.preferences.livery = previewLivery;
                    SaveNow(); SuccessFeedback(); BuildScreen();
                }, ActiveLivery == previewLivery ? "In use" : "Use finish", "primary");
                use.SetEnabled(ActiveLivery != previewLivery);
                use.style.flexGrow = 1;
            }
            if (!apple.IsPassOwned)
            {
                var product = apple.Products.FirstOrDefault(value => value.id == AppleServices.PassProductId);
                var purchase = IconAction(actions, GameGlyph.Palette, "Buy the permanent Founder’s Carriage Collection", () =>
                {
                    if (apple.IsPassOwned || apple.IsPurchasing || apple.IsRestoring || apple.PurchaseState == "pending") return;
                    MarkStoreInteraction();
                    apple.Purchase(AppleServices.PassProductId);
                    BuildScreen();
                }, product == null ? apple.IsStoreLoading ? "Loading…" : "Unavailable" : "Founder pack · " + product.price, "primary");
                purchase.style.flexGrow = 1;
                purchase.SetEnabled(product != null && !apple.IsPurchasing && !apple.IsRestoring && apple.PurchaseState != "pending");
            }
            var restore = IconAction(actions, GameGlyph.Restore, "Restore collection or previous Plus purchases", RestoreFromHUD);
            readouts.Add(() => restore.SetEnabled(!apple.IsPurchasing && !apple.IsRestoring));
            if (apple.ProductState == "failed" || apple.ProductState == "unavailable")
                IconAction(actions, GameGlyph.Arrange, "Refresh App Store products", RefreshStoreFromHUD);
            var tips = IconAction(actions, GameGlyph.Star, "Show optional tips that support the game", () =>
            {
                hudTipsExpanded = !hudTipsExpanded;
                BuildScreen();
            });
            tips.EnableInClassList("active", hudTipsExpanded);
            if (hudTipsExpanded) BuildTipsHUD(dock);
            AddStoreStatusHUD(dock, true);
        }

        private void BuildDepotHUD()
        {
            var dock = Dock(pageBody);
            dock.AddToClassList("depot-hud");
            var preferences = Row(dock, "hud-preferences");
            AddHUDPreference(preferences, GameGlyph.Sound, "Sound", () => profile.preferences.sound,
                () => profile.preferences.sound = !profile.preferences.sound);
            AddHUDPreference(preferences, GameGlyph.Haptic, "Haptics", () => profile.preferences.haptics,
                () => profile.preferences.haptics = !profile.preferences.haptics);
            AddHUDPreference(preferences, GameGlyph.Motion, "Less motion", () => profile.preferences.reducedMotion || apple.IsReduceMotionEnabled,
                () => profile.preferences.reducedMotion = !profile.preferences.reducedMotion,
                () => apple.IsReduceMotionEnabled);
            var actions = Row(dock, "hud-depot-actions");
            IconAction(actions, GameGlyph.Palette, "Preview train finishes and the cosmetic collection", () => Open(Page.Wardrobe), "Finishes");
            IconAction(actions, GameGlyph.Help, "Read the hospital guide", () => Open(Page.Guide), "Guide");
            var restore = IconAction(actions, GameGlyph.Restore, "Restore collection or previous Plus purchases", RestoreFromHUD, "Restore");
            readouts.Add(() => restore.SetEnabled(!apple.IsPurchasing && !apple.IsRestoring));
            AddStoreStatusHUD(dock, false);
        }

        private void AddHUDPreference(VisualElement parent, GameGlyph glyph, string label,
            Func<bool> isOn, Action toggle, Func<bool> systemControlled = null)
        {
            var action = IconAction(parent, glyph, label, () =>
            {
                if (systemControlled != null && systemControlled()) return;
                toggle(); SaveNow(); BuildScreen();
            }, label);
            action.style.flexGrow = 1; action.style.flexBasis = 0; action.style.minWidth = 0;
            readouts.Add(() =>
            {
                var on = isOn();
                var controlled = systemControlled != null && systemControlled();
                action.EnableInClassList("active", on);
                action.tooltip = label + (controlled ? " · enabled by iPhone accessibility settings" : on ? " · on; tap to turn off" : " · off; tap to turn on");
                action.SetEnabled(!controlled);
                var mark = action.Q<LifelineIcon>();
                if (mark != null) mark.Tint = on ? LifelinePalette.Green : LifelinePalette.Ink;
            });
        }

        private void BuildTipsHUD(VisualElement parent)
        {
            var row = Row(parent, "hud-tip-actions");
            row.style.marginTop = 5;
            var hint = Text(row, "Optional tip", "hud-note");
            hint.style.fontSize = 14; hint.style.alignSelf = Align.Center;
            hint.tooltip = "Tips support game development. They grant no items or gameplay benefit.";
            var any = false;
            foreach (var id in new[] { AppleServices.SmallTipProductId, AppleServices.MediumTipProductId, AppleServices.LargeTipProductId })
            {
                var product = apple.Products.FirstOrDefault(value => value.id == id);
                if (product == null) continue;
                any = true;
                var button = Button(row, product.price, () =>
                {
                    if (apple.IsPurchasing || apple.IsRestoring || apple.PurchaseState == "pending") return;
                    MarkStoreInteraction(); apple.Purchase(product.id); BuildScreen();
                }, "quiet small");
                button.tooltip = "Optional tip of " + product.price + " · supports the game; no items or advantages";
                button.style.flexGrow = 1;
                button.SetEnabled(!apple.IsPurchasing && !apple.IsRestoring && apple.PurchaseState != "pending");
            }
            if (!any)
            {
                Text(row, apple.IsStoreLoading ? "Loading…" : "Unavailable", "hud-note");
                IconAction(row, GameGlyph.Restore, "Refresh optional tip availability", RefreshStoreFromHUD);
            }
        }

        private void RestoreFromHUD()
        {
            if (apple.IsPurchasing || apple.IsRestoring) return;
            MarkStoreInteraction();
            apple.RestorePurchases(); BuildScreen();
        }

        private void RefreshStoreFromHUD()
        {
            if (apple.IsStoreLoading) return;
            MarkStoreInteraction();
            apple.LoadProducts(); BuildScreen();
        }

        private void MarkStoreInteraction()
        {
            hudStoreTouched = true;
            hudStoreMessageUntil = Time.realtimeSinceStartupAsDouble + 12;
        }

        private void AddStoreStatusHUD(VisualElement parent, bool shop)
        {
            var label = Text(parent, "", "hud-store-status");
            label.style.fontSize = 14;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 4;
            readouts.Add(() =>
            {
                var stateKey = apple.PurchaseState + "|" + apple.IsRestoring + "|" + apple.StoreStatus;
                if (stateKey != hudObservedStoreState)
                {
                    hudObservedStoreState = stateKey;
                    if (hudStoreTouched) hudStoreMessageUntil = Time.realtimeSinceStartupAsDouble + 12;
                }
                var recent = hudStoreTouched && Time.realtimeSinceStartupAsDouble < hudStoreMessageUntil;
                string message;
                if (apple.IsPurchasing) message = "Confirm your purchase with Apple.";
                else if (apple.IsRestoring) message = "Restoring purchases…";
                else if (apple.PurchaseState == "pending") message = "Awaiting Apple’s approval.";
                else if (apple.PurchaseState == "failed") message = apple.StoreStatus;
                else if (recent && apple.PurchaseState == "cancelled" && string.IsNullOrEmpty(apple.StoreStatus)) message = "Purchase cancelled.";
                else if (recent) message = apple.StoreStatus;
                else if (shop && (apple.ProductState == "failed" || apple.ProductState == "unavailable")) message = apple.StoreStatus;
                else message = "";
                label.text = message ?? "";
                label.tooltip = apple.StoreStatus;
                label.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
            });
        }

        private static Color LiverySwatch(int index)
        {
            switch (index)
            {
                case 1: return new Color(.71f, .39f, .19f);
                case 2: return new Color(.22f, .42f, .54f);
                case 3: return new Color(.17f, .28f, .20f);
                default: return LifelinePalette.Green;
            }
        }
    }
}
