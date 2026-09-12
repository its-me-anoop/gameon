#pragma once
#import "UnityAppController.h"
@interface UnityScene : NSObject <UISceneDelegate>
@property(nonatomic) NSUInteger foregroundCount;
@property(nonatomic) NSUInteger activeCount;
@property(nonatomic) NSUInteger backgroundCount;
@end
