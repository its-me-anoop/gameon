#import "UnityAppController.h"
#import "UI/UnityScene.h"

// Native 1.x used SwiftUI scenes. UIKit can reconnect those saved sessions after
// an app update without consulting the Unity scene configuration in Info.plist.
// Replacing the live delegate is public UIKit API; no saved state is removed.
@interface OrchardAppController : UnityAppController
@property(nonatomic, strong) NSHashTable<UIWindowScene *> *adoptedScenes;
@end

@implementation OrchardAppController
- (BOOL)application:(UIApplication *)application didFinishLaunchingWithOptions:(NSDictionary *)options {
    self.adoptedScenes = NSHashTable.weakObjectsHashTable;
    NSNotificationCenter *center = NSNotificationCenter.defaultCenter;
    [center addObserver:self selector:@selector(connectClinicScene:) name:UISceneWillConnectNotification object:nil];
    [center addObserver:self selector:@selector(foregroundClinicScene:) name:UISceneWillEnterForegroundNotification object:nil];
    return [super application:application didFinishLaunchingWithOptions:options];
}

- (void)dealloc {
    [NSNotificationCenter.defaultCenter removeObserver:self];
}

- (BOOL)adoptLegacyScene:(id)object {
    if (![object isKindOfClass:UIWindowScene.class]) return NO;
    UIWindowScene *scene = object;
    if (![scene.session.role isEqualToString:UIWindowSceneSessionRoleApplication]) return NO;
    if ([self.adoptedScenes containsObject:scene]) return YES;
    if ([scene.delegate isKindOfClass:UnityScene.class]) return NO;

    NSString *legacyClass = @"SwiftUI.AppSceneDelegate";
    BOOL legacyDelegate = [NSStringFromClass(scene.delegate.class) isEqualToString:legacyClass];
    if (scene.delegate != nil && !legacyDelegate) return NO;
    BOOL legacyConfiguration = [NSStringFromClass(scene.session.configuration.delegateClass) isEqualToString:legacyClass];
    // The previous framework may no longer be loaded, so its class resolves to nil.
    BOOL legacyRestoration = scene.delegate == nil && scene.session.configuration.delegateClass == Nil &&
        [scene.session.stateRestorationActivity.activityType isEqualToString:@"com.apple.SwiftUI.stateRestoration"];
    if (!legacyDelegate && !legacyConfiguration && !legacyRestoration) return NO;

    scene.delegate = [UnityScene new];
    [self.adoptedScenes addObject:scene];
    NSLog(@"Little Lifeline: migrated legacy scene %@ to UnityScene", scene.session.persistentIdentifier);
    return YES;
}

- (void)connectClinicScene:(NSNotification *)notification {
    [self adoptLegacyScene:notification.object];
}

- (void)foregroundClinicScene:(NSNotification *)notification {
    if ([self adoptLegacyScene:notification.object] && self.engineLoadState < kUnityEngineLoadStateCoreInitialized)
        [self initUnityWithScene:(UIWindowScene *)notification.object];
    // UnityScene handles all subsequent lifecycle callbacks, including pause and
    // resume. The fallback above only covers a delegate changed during foreground.
}
@end

IMPL_APP_CONTROLLER_SUBCLASS(OrchardAppController)
