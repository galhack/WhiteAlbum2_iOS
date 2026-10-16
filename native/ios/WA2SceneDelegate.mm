// SPDX-License-Identifier: Apache-2.0
// Adapt the Godot 4.5 single-window application to UIKit's scene lifecycle.
#import <UIKit/UIKit.h>

@interface GDTApplicationDelegate : NSObject <UIApplicationDelegate>
@end

@interface WA2SceneDelegate : NSObject <UIWindowSceneDelegate>
@property(strong, nonatomic) UIWindow *window;
@end

@implementation WA2SceneDelegate
- (void)scene:(UIScene *)scene willConnectToSession:(UISceneSession *)session options:(UISceneConnectionOptions *)options {
    if (![scene isKindOfClass:[UIWindowScene class]]) return;
    self.window = UIApplication.sharedApplication.delegate.window;
    self.window.windowScene = (UIWindowScene *)scene;
    [self.window makeKeyAndVisible];
    // Revoke the directory bookmark from earlier cloud-enabled test builds.
    [NSUserDefaults.standardUserDefaults removeObjectForKey:@"WA2CloudFolder"];
}
- (void)sceneDidBecomeActive:(UIScene *)scene {
    [UIApplication.sharedApplication.delegate applicationDidBecomeActive:UIApplication.sharedApplication];
}
- (void)sceneWillResignActive:(UIScene *)scene {
    [UIApplication.sharedApplication.delegate applicationWillResignActive:UIApplication.sharedApplication];
}
- (void)sceneDidEnterBackground:(UIScene *)scene {
    [UIApplication.sharedApplication.delegate applicationDidEnterBackground:UIApplication.sharedApplication];
}
- (void)sceneWillEnterForeground:(UIScene *)scene {
    [UIApplication.sharedApplication.delegate applicationWillEnterForeground:UIApplication.sharedApplication];
}
@end

@implementation GDTApplicationDelegate (WA2Scenes)
- (UISceneConfiguration *)application:(UIApplication *)application configurationForConnectingSceneSession:(UISceneSession *)session options:(UISceneConnectionOptions *)options {
    UISceneConfiguration *configuration = [[UISceneConfiguration alloc] initWithName:@"WA2" sessionRole:session.role];
    configuration.delegateClass = WA2SceneDelegate.class;
    return configuration;
}
@end
