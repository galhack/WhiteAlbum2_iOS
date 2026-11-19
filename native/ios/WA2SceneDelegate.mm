// SPDX-License-Identifier: Apache-2.0
// Adapt the Godot 4.5 single-window application to UIKit's scene lifecycle.
#import <UIKit/UIKit.h>
#import <Metal/Metal.h>

#include "WA2RendererArguments.h"

extern int gargc;
extern char **gargv;

@interface GDTApplicationDelegate : NSObject <UIApplicationDelegate>
+ (void)addService:(NSObject<UIApplicationDelegate> *)service;
@end

@interface WA2RendererLaunchService : NSObject <UIApplicationDelegate>
@end

@implementation WA2RendererLaunchService
- (BOOL)application:(UIApplication *)application willFinishLaunchingWithOptions:(NSDictionary *)launchOptions {
    // This callback runs before Godot's didFinishLaunching service reads argv.
    id<MTLDevice> device = MTLCreateSystemDefaultDevice();
    BOOL supportsApple4 = [device supportsFamily:MTLGPUFamilyApple4];
    NSBundle *bundle = NSBundle.mainBundle;
    NSArray *bundleArguments = [bundle objectForInfoDictionaryKey:@"godot_cmdline"];
    NSUInteger bundleArgumentCount = bundleArguments.count;
    if ([bundle objectForInfoDictionaryKey:@"godot_path"]) {
        bundleArgumentCount += 2;
    }
    static char *arguments[WA2GodotArgumentCapacity];
    int count = WA2SelectRendererArguments(supportsApple4,
                                          gargc, gargv, bundleArgumentCount, arguments);
    if (count != 0) {
        gargc = count;
        gargv = arguments;
        NSLog(@"WA2 selected %@ for %@.", supportsApple4 ? @"Mobile" : @"Compatibility",
              device.name ?: @"an unavailable Metal device");
    }
    return YES;
}
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
+ (void)load {
    [self addService:[WA2RendererLaunchService new]];
}

- (UISceneConfiguration *)application:(UIApplication *)application configurationForConnectingSceneSession:(UISceneSession *)session options:(UISceneConnectionOptions *)options {
    UISceneConfiguration *configuration = [[UISceneConfiguration alloc] initWithName:@"WA2" sessionRole:session.role];
    configuration.delegateClass = WA2SceneDelegate.class;
    return configuration;
}
@end
