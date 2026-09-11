#import <Foundation/Foundation.h>
#import <GameKit/GameKit.h>
#import "UnityFramework-Swift.h"

// Unity copies the UTF-8 message into its message queue before this call returns.
extern "C" void UnitySendMessage(const char *object, const char *method, const char *message);

static NSString *OrchardString(const char *value) {
    return value == nullptr ? @"" : ([NSString stringWithUTF8String:value] ?: @"");
}

extern "C" {
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
