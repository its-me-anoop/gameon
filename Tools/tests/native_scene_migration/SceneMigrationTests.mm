#import "UnityAppController.h"
#import "UI/UnityScene.h"
#import <objc/runtime.h>

UISceneSessionRole const UIWindowSceneSessionRoleApplication = @"UIWindowSceneSessionRoleApplication";
NSNotificationName const UISceneWillConnectNotification = @"UISceneWillConnectNotification";
NSNotificationName const UISceneWillEnterForegroundNotification = @"UISceneWillEnterForegroundNotification";
const char *AppControllerClassName = nullptr;
@implementation UISceneConfiguration @end
@implementation UISceneSession @end
@implementation UIScene @end
@implementation UIWindowScene @end
@implementation UIApplication @end
@implementation UnityAppController
- (BOOL)application:(UIApplication *)application didFinishLaunchingWithOptions:(NSDictionary *)options { return YES; }
- (void)initUnityWithScene:(UIWindowScene *)scene {
    if (self.engineLoadState >= kUnityEngineLoadStateCoreInitialized) return;
    self.initializationCount++;
    self.engineLoadState = kUnityEngineLoadStateCoreInitialized;
}
@end
@implementation UnityScene
- (void)sceneWillEnterForeground:(UIScene *)scene { self.foregroundCount++; }
- (void)sceneDidBecomeActive:(UIScene *)scene { self.activeCount++; }
- (void)sceneDidEnterBackground:(UIScene *)scene { self.backgroundCount++; }
@end
@interface OtherSceneDelegate : NSObject <UISceneDelegate> @end
@implementation OtherSceneDelegate @end

static void Require(BOOL condition, NSString *message) {
    if (!condition) { fprintf(stderr, "%s\n", message.UTF8String); exit(1); }
}
static UIWindowScene *Scene(Class delegateClass, id<UISceneDelegate> delegate = nil) {
    UIWindowScene *scene = [UIWindowScene new];
    scene.session = [UISceneSession new];
    scene.session.role = UIWindowSceneSessionRoleApplication;
    scene.session.persistentIdentifier = @"preserved-test-session";
    scene.session.configuration = [UISceneConfiguration new];
    scene.session.configuration.name = @"Default Configuration";
    scene.session.configuration.delegateClass = delegateClass;
    scene.delegate = delegate;
    return scene;
}
static void Post(NSNotificationName name, id object) {
    [NSNotificationCenter.defaultCenter postNotificationName:name object:object];
}
int main(int argc, const char *argv[]) {
    @autoreleasepool {
        Require(argc == 2, @"One named test is required");
        Class legacy = objc_allocateClassPair(NSObject.class, "SwiftUI.AppSceneDelegate", 0);
        class_addProtocol(legacy, @protocol(UISceneDelegate));
        objc_registerClassPair(legacy);
        Require(AppControllerClassName != nullptr, @"Unity must register the custom app controller");
        UnityAppController *controller = [NSClassFromString(@(AppControllerClassName)) new];
        Require(controller != nil, @"Registered app controller is constructible");
        Require([controller application:[UIApplication new] didFinishLaunchingWithOptions:@{}], @"Preserve launch success");
        NSString *test = @(argv[1]);
        if ([test isEqualToString:@"configured-legacy"]) {
            UIWindowScene *scene = Scene(legacy);
            UISceneSession *original = scene.session;
            Post(UISceneWillConnectNotification, scene);
            Require([scene.delegate isKindOfClass:UnityScene.class], @"Restored unavailable legacy delegate must hand off to Unity");
            Require(scene.session == original && scene.session.configuration.delegateClass == legacy,
                    @"Do not rewrite or destroy the persisted session");
            Require(controller.initializationCount == 0, @"Do not start graphics for a merely connected scene");
        } else if ([test isEqualToString:@"live-legacy"]) {
            UIWindowScene *scene = Scene(nil, [legacy new]);
            Post(UISceneWillConnectNotification, scene);
            Require([scene.delegate isKindOfClass:UnityScene.class], @"Recognize the live legacy delegate");
        } else if ([test isEqualToString:@"unresolved-legacy"]) {
            UIWindowScene *scene = Scene(nil);
            scene.session.configuration.name = nil;
            scene.session.stateRestorationActivity = [[NSUserActivity alloc] initWithActivityType:@"com.apple.SwiftUI.stateRestoration"];
            Post(UISceneWillConnectNotification, scene);
            Require([scene.delegate isKindOfClass:UnityScene.class], @"Handle unloaded SwiftUI delegate using public restoration activity");
        } else if ([test isEqualToString:@"current-unity"]) {
            UnityScene *delegate = [UnityScene new];
            UIWindowScene *scene = Scene(UnityScene.class, delegate);
            Post(UISceneWillConnectNotification, scene);
            Post(UISceneWillEnterForegroundNotification, scene);
            Require(scene.delegate == delegate && controller.initializationCount == 0, @"Leave normal Unity lifecycle untouched");
        } else if ([test isEqualToString:@"other-delegate"]) {
            OtherSceneDelegate *delegate = [OtherSceneDelegate new];
            UIWindowScene *scene = Scene(OtherSceneDelegate.class, delegate);
            Post(UISceneWillConnectNotification, scene);
            Require(scene.delegate == delegate, @"Do not replace unrelated scene delegates");
        } else if ([test isEqualToString:@"already-overridden"]) {
            OtherSceneDelegate *delegate = [OtherSceneDelegate new];
            UIWindowScene *scene = Scene(legacy, delegate);
            Post(UISceneWillConnectNotification, scene);
            Require(scene.delegate == delegate, @"Respect an explicit replacement of a legacy delegate");
        } else if ([test isEqualToString:@"other-role"]) {
            UIWindowScene *scene = Scene(legacy);
            scene.session.role = @"UIWindowSceneSessionRoleExternalDisplay";
            Post(UISceneWillConnectNotification, scene);
            Require(scene.delegate == nil, @"Do not take external display scenes");
        } else if ([test isEqualToString:@"nonwindow"]) {
            UIScene *scene = [UIScene new];
            scene.session = Scene(legacy).session;
            Post(UISceneWillConnectNotification, scene);
            Post(UISceneWillConnectNotification, nil);
            Require(scene.delegate == nil, @"Do not take non-window scenes");
        } else if ([test isEqualToString:@"unknown-missing"]) {
            UIWindowScene *scene = Scene(nil);
            Post(UISceneWillConnectNotification, scene);
            Require(scene.delegate == nil, @"Missing delegate alone is not proof of a legacy scene");
        } else if ([test isEqualToString:@"foreground-fallback"]) {
            UIWindowScene *scene = Scene(legacy);
            Post(UISceneWillEnterForegroundNotification, scene);
            id<UISceneDelegate> adopted = scene.delegate;
            Post(UISceneWillEnterForegroundNotification, scene);
            Post(UISceneWillConnectNotification, scene);
            Require([adopted isKindOfClass:UnityScene.class] && scene.delegate == adopted, @"Migration must be idempotent");
            Require(controller.initializationCount == 1, @"Foreground fallback must initialize exactly once");
        } else if ([test isEqualToString:@"lifecycle"]) {
            UIWindowScene *scene = Scene(legacy);
            Post(UISceneWillConnectNotification, scene);
            UnityScene *delegate = (UnityScene *)scene.delegate;
            [delegate sceneWillEnterForeground:scene];
            Post(UISceneWillEnterForegroundNotification, scene);
            [delegate sceneDidBecomeActive:scene];
            [delegate sceneDidEnterBackground:scene];
            [delegate sceneWillEnterForeground:scene];
            Post(UISceneWillEnterForegroundNotification, scene);
            [delegate sceneDidBecomeActive:scene];
            Require(delegate.foregroundCount == 2 && delegate.activeCount == 2 && delegate.backgroundCount == 1,
                    @"Observer must not duplicate Unity delegate lifecycle callbacks");
        } else { Require(NO, @"Unknown test case"); }
        printf("PASS %s\n", argv[1]);
    }
}
