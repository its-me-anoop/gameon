#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>
#import <UIKit/UIKit.h>
#import <TargetConditionals.h>
#import <UnityFramework/UnityFramework-Swift.h>

// Unity copies the UTF-8 message into its message queue before this call returns.
extern "C" void UnitySendMessage(const char *object, const char *method, const char *message);

static NSString *OrchardString(const char *value) {
    return value == nullptr ? @"" : ([NSString stringWithUTF8String:value] ?: @"");
}

extern "C" {
    __attribute__((visibility("default"))) int OO_ThermalState(void) {
#if TARGET_OS_SIMULATOR
        return -1;
#else
        return (int)NSProcessInfo.processInfo.thermalState;
#endif
    }

    __attribute__((visibility("default"))) float OO_ScreenWidthPoints(void) {
        __block CGFloat width = 0;
        void (^readWidth)(void) = ^{
            for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
                if (![scene isKindOfClass:UIWindowScene.class]) continue;
                UIWindowScene *windowScene = (UIWindowScene *)scene;
                if (scene.activationState != UISceneActivationStateForegroundActive &&
                    scene.activationState != UISceneActivationStateForegroundInactive) continue;
                for (UIWindow *window in windowScene.windows) {
                    if (window.isKeyWindow) { width = window.bounds.size.width; return; }
                }
                width = windowScene.coordinateSpace.bounds.size.width;
            }
            if (width <= 0) width = UIScreen.mainScreen.bounds.size.width;
        };
        if (NSThread.isMainThread) readWidth();
        else dispatch_sync(dispatch_get_main_queue(), readWidth);
        return (float)width;
    }

    __attribute__((visibility("default"))) void OO_Initialize(void) {
        dispatch_async(dispatch_get_main_queue(), ^{
            OrchardAppleBridge.shared.eventHandler = ^(NSString *json) {
                UnitySendMessage("AppleServices", "OnNativeMessage", json.UTF8String);
            };
            [OrchardAppleBridge.shared initialize];
        });
    }

    __attribute__((visibility("default"))) void OO_LoadProducts(void) {
        dispatch_async(dispatch_get_main_queue(), ^{ [OrchardAppleBridge.shared loadProducts]; });
    }

    __attribute__((visibility("default"))) void OO_Purchase(const char *productID) {
        NSString *identifier = OrchardString(productID);
        dispatch_async(dispatch_get_main_queue(), ^{ [OrchardAppleBridge.shared purchase:identifier]; });
    }

    __attribute__((visibility("default"))) void OO_RestorePurchases(void) {
        dispatch_async(dispatch_get_main_queue(), ^{ [OrchardAppleBridge.shared restorePurchases]; });
    }

    __attribute__((visibility("default"))) void OO_AuthenticateGameCenter(void) {
        dispatch_async(dispatch_get_main_queue(), ^{ [OrchardAppleBridge.shared authenticateGameCenter]; });
    }

    __attribute__((visibility("default"))) void OO_ShowLeaderboard(int daily) {
        dispatch_async(dispatch_get_main_queue(), ^{ [OrchardAppleBridge.shared showLeaderboard:daily != 0]; });
    }

    __attribute__((visibility("default"))) void OO_UseLifelineLeaderboards(void) {
        dispatch_async(dispatch_get_main_queue(), ^{ [OrchardAppleBridge.shared useLifelineLeaderboards]; });
    }

    __attribute__((visibility("default"))) void OO_ShowWeeklyLeaderboard(void) {
        dispatch_async(dispatch_get_main_queue(), ^{ [OrchardAppleBridge.shared showWeeklyLeaderboard]; });
    }

    __attribute__((visibility("default"))) void OO_SubmitScore(int score, const char *mode, const char *dayKey) {
        NSString *runMode = OrchardString(mode);
        NSString *day = OrchardString(dayKey);
        dispatch_async(dispatch_get_main_queue(), ^{
            [OrchardAppleBridge.shared submitScore:score mode:runMode dayKey:day];
        });
    }

    __attribute__((visibility("default"))) void OO_RetryScores(void) {
        dispatch_async(dispatch_get_main_queue(), ^{ [OrchardAppleBridge.shared retryScores]; });
    }

    __attribute__((visibility("default"))) void OO_Haptic(int kind) {
        dispatch_async(dispatch_get_main_queue(), ^{ [OrchardAppleBridge.shared playHaptic:kind]; });
    }
}
