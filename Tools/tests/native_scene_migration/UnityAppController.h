#pragma once
#import <Foundation/Foundation.h>

// Small public-API doubles for executing the actual adapter on macOS.
// The adapter is also compiled separately against UIKit and Unity's real headers.
typedef NSString *UISceneSessionRole;
extern UISceneSessionRole const UIWindowSceneSessionRoleApplication;
extern NSNotificationName const UISceneWillConnectNotification;
extern NSNotificationName const UISceneWillEnterForegroundNotification;

@interface UISceneConfiguration : NSObject
@property(nonatomic) Class delegateClass;
@property(nonatomic, copy) NSString *name;
@end
@interface UISceneSession : NSObject
@property(nonatomic, copy) UISceneSessionRole role;
@property(nonatomic, copy) NSString *persistentIdentifier;
@property(nonatomic, strong) UISceneConfiguration *configuration;
@property(nonatomic, strong) NSUserActivity *stateRestorationActivity;
@end
@protocol UISceneDelegate <NSObject>
@optional
- (void)sceneWillEnterForeground:(id)scene;
- (void)sceneDidBecomeActive:(id)scene;
- (void)sceneDidEnterBackground:(id)scene;
@end
@interface UIScene : NSObject
@property(nonatomic, strong) UISceneSession *session;
@property(nonatomic, strong) id<UISceneDelegate> delegate;
@end
@interface UIWindowScene : UIScene
@end
@interface UIApplication : NSObject
@end
typedef NS_ENUM(NSInteger, UnityEngineLoadState) {
    kUnityEngineLoadStateNotStarted = 0,
    kUnityEngineLoadStateCoreInitialized = 2
};
@interface UnityAppController : NSObject
@property(nonatomic) UnityEngineLoadState engineLoadState;
@property(nonatomic) NSUInteger initializationCount;
- (BOOL)application:(UIApplication *)application didFinishLaunchingWithOptions:(NSDictionary *)options;
- (void)initUnityWithScene:(UIWindowScene *)scene;
@end
extern const char *AppControllerClassName;
#define IMPL_APP_CONTROLLER_SUBCLASS(ClassName) \
    @implementation ClassName (TestRegistration) \
    + (void)load { AppControllerClassName = #ClassName; } \
    @end
