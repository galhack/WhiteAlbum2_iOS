# SPDX-License-Identifier: Apache-2.0
extends SceneTree

func _initialize() -> void:
    check_remote.call_deferred()

func frames(count: int = 4) -> void:
    for frame in range(count):
        await process_frame

func key(code: Key) -> void:
    for pressed in [true, false]:
        var event = InputEventKey.new()
        event.keycode = code
        event.pressed = pressed
        root.push_input(event, true)
        await process_frame
    await frames()

func require(condition: bool, message: String) -> bool:
    if condition: return true
    push_error(message)
    quit(1)
    return false

func check_remote() -> void:
    var launcher = load("res://scene/Launcher.tscn").instantiate()
    root.add_child(launcher)
    current_scene = launcher
    await frames(120)
    var game = current_scene
    var ui = game.get("UiMgr")
    for frame in range(1200):
        var state = game.call("GetPortDiagnostics")
        if state["ui"] == "AdvMain" and state["dialogue_state"] == "WAIT_CLICK": break
        if frame % 120 == 0 and game.get("VideoPlayer").get("IsPlaying"):
            game.call("OnVideoFinished")
        await process_frame
    print("WA2_REMOTE_START ", game.call("GetPortDiagnostics"))
    # Menu opens a pause overlay; directional/select input goes through GUI dispatch.
    await key(KEY_ESCAPE)
    var pause = root.find_child("RemotePauseMenu", true, false)
    if not require(pause != null and pause.visible, "Remote Menu did not open pause"): return
    if not require("Resume" in root.gui_get_focus_owner().text, "Pause must initially focus Resume"): return
    await key(KEY_DOWN)
    if not require("Save" in root.gui_get_focus_owner().text, "Down did not focus Save"): return
    await key(KEY_ENTER)
    await frames(120)
    var saves = ui.get("LoadSaveMenu")
    if not require(saves.visible, "Select did not open original save menu"): return
    var first_slot = saves.get("DataSlots").get_child(0)
    if not require(root.gui_get_focus_owner() == first_slot, "Save menu did not focus first slot"): return
    await key(KEY_RIGHT)
    if not require(root.gui_get_focus_owner() != first_slot, "Save slot directional navigation failed"): return
    await key(KEY_ESCAPE)
    await frames(120)
    if not require(not saves.visible, "Back did not close save menu"): return
    await key(KEY_ESCAPE)
    # Two-column pause grid: Load sits to the right of Save.
    await key(KEY_DOWN)
    await key(KEY_RIGHT)
    if not require("Load" in root.gui_get_focus_owner().text, "Right from Save did not focus Load"): return
    await key(KEY_DOWN)
    if not require("Skip" in root.gui_get_focus_owner().text, "Pause menu lacks Skip below Load"): return
    await key(KEY_LEFT)
    if not require("Auto" in root.gui_get_focus_owner().text, "Pause menu lacks Auto"): return
    await key(KEY_UP)
    await key(KEY_RIGHT)
    await key(KEY_ENTER)
    await frames(120)
    if not require(saves.visible, "Remote Load did not open load menu"): return
    await key(KEY_ESCAPE)
    await frames(120)
    await key(KEY_ESCAPE)
    for step in range(3): await key(KEY_DOWN)
    await key(KEY_ENTER)
    await frames(120)
    var options = ui.get("OptionsMenu")
    if not require(options.visible, "Remote Options did not open settings"): return
    await key(KEY_RIGHT)
    if not require(root.gui_get_focus_owner() != null, "Settings lack a focus target"): return
    await key(KEY_ESCAPE)
    await frames(120)
    if not require(not options.visible, "Back did not close settings"): return
    # Touch users close the pause overlay by tapping outside its buttons.
    await key(KEY_ESCAPE)
    if not require(pause.visible, "Pause did not reopen"): return
    for pressed in [true, false]:
        var click = InputEventMouseButton.new()
        click.button_index = MOUSE_BUTTON_LEFT
        click.pressed = pressed
        click.position = Vector2(20, 20)
        click.global_position = Vector2(20, 20)
        root.push_input(click, true)
        await process_frame
    await frames()
    if not require(not pause.visible, "Tapping outside did not resume"): return
    # The tap switched to pointer mode; any remote key switches back.
    await key(KEY_UP)
    game.call("BeginPortRemoteChoiceProbe")
    await frames()
    var choices = game.get("AdvMain").get("SelectMessageContainer")
    var choices_ok = root.gui_get_focus_owner() == choices.get_child(0)
    await key(KEY_DOWN)
    choices_ok = choices_ok and root.gui_get_focus_owner() == choices.get_child(2)
    await key(KEY_ENTER)
    choices_ok = choices_ok and not choices.visible
    var selected = game.call("EndPortRemoteChoiceProbe")
    if not require(choices_ok and selected == 2, "Remote choices must skip disabled entries and commit the selected option"): return
    print("WA2_REMOTE_MENUS passed: pause grid, auto/skip entries, save slots, load, settings, back, tap-outside resume, dialogue choices")
    for child in root.get_children(): child.queue_free()
    await frames()
    quit(0)
