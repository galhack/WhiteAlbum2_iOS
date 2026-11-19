// SPDX-License-Identifier: Apache-2.0
#pragma once

#include <algorithm>
#include <cstddef>
#include <cstring>

// Godot 4.5.1's iOS entry point copies argv and Info.plist options into
// a 64-element array, including the terminating null.
constexpr int WA2GodotArgumentCapacity = 64;

inline int WA2SelectRendererArguments(bool supportsApple4, int argc, char *const *argv,
                                      std::size_t bundleArgumentCount, char **output) {
    if (argc < 1 || argc > WA2GodotArgumentCapacity - 3 ||
        bundleArgumentCount > static_cast<std::size_t>(WA2GodotArgumentCapacity - argc - 3)) {
        return 0;
    }

    int insertion = 1;
    for (; insertion < argc; ++insertion) {
        const char *argument = argv[insertion];
        if (std::strcmp(argument, "--") == 0 || std::strcmp(argument, "++") == 0) {
            break;
        }
        if (std::strcmp(argument, "--rendering-method") == 0 ||
            std::strcmp(argument, "--rendering-driver") == 0) {
            return 0;
        }
    }

    static char renderingMethod[] = "--rendering-method";
    static char compatibility[] = "gl_compatibility";
    static char mobile[] = "mobile";
    std::copy(argv, argv + insertion, output);
    output[insertion] = renderingMethod;
    output[insertion + 1] = supportsApple4 ? mobile : compatibility;
    std::copy(argv + insertion, argv + argc, output + insertion + 2);
    output[argc + 2] = nullptr;
    return argc + 2;
}
