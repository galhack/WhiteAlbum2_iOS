# SPDX-License-Identifier: Apache-2.0
extends SceneTree

func _initialize() -> void:
    check_progression.call_deferred()

func check_progression() -> void:
    var launcher = load("res://scene/Launcher.tscn").instantiate()
    root.add_child(launcher)
    current_scene = launcher
    for frame in range(120):
        await process_frame
    var game = current_scene
    var title = game.get("UiMgr").get("TitleMenu")
    var old_ic = game.call("ReadSysFlag", 0)
    var old_cc = game.call("ReadSysFlag", 1)
    var initial_script = game.call("GetPortDiagnostics")["script"]
    var ok = true
    for flags in [[0, 0], [1, 0], [1, 1], [0, 1]]:
        game.call("WirtSysFlag", 0, flags[0])
        game.call("WirtSysFlag", 1, flags[1])
        title.call("OnStartButtonDown")
        ok = ok and not title.get("ICButton").disabled
        ok = ok and title.get("CcButton").disabled == (flags[0] != 1)
        ok = ok and title.get("CodeaButton").disabled == (flags != [1, 1])
        # Guard direct callbacks too: locked chapters must not start an animation.
        if flags == [0, 0]:
            title.call("OnCCButtonDown")
            title.call("OnCodeaButtonDown")
            ok = ok and game.call("GetPortDiagnostics")["script"] == initial_script
    game.call("WirtSysFlag", 0, old_ic)
    game.call("WirtSysFlag", 1, old_cc)
    if not ok:
        push_error("Chapter menu does not match persistent completion flags.")
        quit(1)
        return
    print("WA2_PROGRESSION_MENU passed; original flags restored")
    for child in root.get_children(): child.queue_free()
    for frame in range(4): await process_frame
    quit(0)
