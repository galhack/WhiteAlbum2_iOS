// SPDX-License-Identifier: Apache-2.0
#include "../native/ios/WA2RendererArguments.h"

#include <cassert>
#include <cstring>

int main() {
    char app[] = "WA2";
    char separator[] = "--";
    char userOption[] = "--rendering-driver";
    char userValue[] = "user-value";
    char overrideOption[] = "--rendering-method";
    char overrideValue[] = "mobile";
    char *output[WA2GodotArgumentCapacity] = {};

    char *plain[] = {app};
    assert(WA2SelectRendererArguments(true, 1, plain, 0, output) == 0);
    assert(WA2SelectRendererArguments(false, 1, plain, 0, output) == 3);
    assert(output[0] == app);
    assert(std::strcmp(output[1], "--rendering-method") == 0);
    assert(std::strcmp(output[2], "gl_compatibility") == 0);
    assert(output[3] == nullptr);

    char *explicitRenderer[] = {app, overrideOption, overrideValue};
    assert(WA2SelectRendererArguments(false, 3, explicitRenderer, 0, output) == 0);
    char *explicitDriver[] = {app, userOption, userValue};
    assert(WA2SelectRendererArguments(false, 3, explicitDriver, 0, output) == 0);

    char *userArgs[] = {app, separator, userOption, userValue};
    assert(WA2SelectRendererArguments(false, 4, userArgs, 0, output) == 6);
    assert(output[3] == separator && output[4] == userOption && output[5] == userValue);
    assert(output[6] == nullptr);

    assert(WA2SelectRendererArguments(false, 1, plain, WA2GodotArgumentCapacity - 4, output) == 3);
    assert(WA2SelectRendererArguments(false, 1, plain, WA2GodotArgumentCapacity - 3, output) == 0);
}
