// SPDX-License-Identifier: Apache-2.0
#import <Foundation/Foundation.h>
#include <stdlib.h>
#include <string.h>

static NSString *const WA2LocalKey = @"WA2LocalProgressV1";
static const int WA2LocalLimit = 448 * 1024;

extern "C" __attribute__((visibility("default"), used)) void *wa2_tv_read(int *length) {
    @autoreleasepool {
        NSData *data = [NSUserDefaults.standardUserDefaults dataForKey:WA2LocalKey];
        *length = (int)data.length;
        if (!data) return nullptr;
        if (data.length > WA2LocalLimit) { *length = -1; return nullptr; }
        void *result = malloc(data.length);
        if (!result) { *length = -1; return nullptr; }
        memcpy(result, data.bytes, data.length);
        return result;
    }
}

extern "C" __attribute__((visibility("default"), used)) int wa2_tv_write(const void *bytes, int length) {
    @autoreleasepool {
        if (!bytes || length < 1 || length > WA2LocalLimit) return 0;
        NSUserDefaults *defaults = NSUserDefaults.standardUserDefaults;
        NSString *domain = NSBundle.mainBundle.bundleIdentifier;
        NSMutableDictionary *candidate = [[defaults persistentDomainForName:domain] mutableCopy] ?: [NSMutableDictionary new];
        NSData *data = [NSData dataWithBytes:bytes length:length];
        candidate[WA2LocalKey] = data;
        NSError *error = nil;
        NSData *serialized = [NSPropertyListSerialization dataWithPropertyList:candidate format:NSPropertyListBinaryFormat_v1_0 options:0 error:&error];
        if (!serialized || serialized.length > 480 * 1024) return 0;
        [defaults setObject:data forKey:WA2LocalKey];
        return 1;
    }
}

extern "C" __attribute__((visibility("default"), used)) void wa2_tv_free(void *bytes) { free(bytes); }
