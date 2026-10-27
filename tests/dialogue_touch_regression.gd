# SPDX-License-Identifier: Apache-2.0
extends SceneTree

var game: Control
var adv: Control
var ui: Control

func _initialize() -> void:
    check_touch.call_deferred()

func frames(count: int = 4) -> void:
    for frame in range(count):
        # Synthetic pointer input represents a foreground app. macOS otherwise
        # pauses an unattended window before these gesture checks can run.
        if paused and is_instance_valid(game):
            game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_IN)
        await process_frame

func require(condition: bool, message: String) -> void:
    if not condition:
        push_error(message)
        quit(1)
        # Stop immediately instead of continuing with an invalid scene.
        await process_frame
        assert(false, message)

# ParseInputEvent exercises Godot's real mouse-from-touch ordering too.
func touch(point: Vector2, pressed: bool, index: int = 0, canceled: bool = false) -> void:
    var event = InputEventScreenTouch.new()
    event.position = point
    event.index = index
    event.pressed = pressed
    event.canceled = canceled
    Input.parse_input_event(event)
    await frames(1)

func drag(point: Vector2, delta: Vector2) -> void:
    var event = InputEventScreenDrag.new()
    event.position = point
    event.relative = delta
    Input.parse_input_event(event)
    await frames(1)

func click_button(button: Control) -> void:
    var point = button.get_global_rect().get_center()
    await touch(point, true)
    await touch(point, false)
    await frames(40)

func prepare_dialogue() -> void:
    adv.show()
    adv.set("State", 3) # WAIT_CLICK
    adv.set("WaitKey", true) # Reveal the next text segment without executing a new script command.
    game.set("SkipMode", false)
    game.set("AutoMode", false)

func swipe(start: Vector2) -> void:
    await touch(start, true)
    await require(adv.get("State") == 3, "Touch-down advanced dialogue before recognizing a swipe")
    await drag(start + Vector2(2, 60), Vector2(2, 60))
    await touch(start + Vector2(2, 60), false)

func check_touch() -> void:
    Input.emulate_mouse_from_touch = true
    var launcher = load("res://scene/Launcher.tscn").instantiate()
    root.add_child(launcher)
    current_scene = launcher
    await frames(120)
    game = current_scene
    await require(game != launcher, "Launch with --resources, --play and --start-script")
    ui = game.get("UiMgr")
    adv = game.get("AdvMain")
    var ready = false
    for frame in range(2400):
        if paused: game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_IN)
        var state = game.call("GetPortDiagnostics")
        if state["ui"] == "AdvMain" and state["dialogue_state"] == "WAIT_CLICK" and not state["wait"] and not state["animation_wait"]:
            ready = true
            break
        if frame % 120 == 0 and game.get("VideoPlayer").get("IsPlaying"):
            game.call("OnVideoFinished")
        await process_frame
    await require(ready, "Story did not reach stable dialogue: " + str(game.call("GetPortDiagnostics")) + " paused=" + str(paused))
    game.set_process(false) # Freeze script progression while checking each input.
    var start = adv.get("MessageBox").get_global_rect().get_center()
    var position = game.call("GetPortDiagnostics")["position"]
    await swipe(start)
    var backlog = ui.get("BackLogMenu")
    await require(backlog.visible and not adv.visible, "Downward dialogue swipe did not open history")
    await require(adv.get("State") == 3 and game.call("GetPortDiagnostics")["position"] == position, "Swipe advanced the story")
    await require(not game.get("IsPressed") and game.get("PressedTime") == 0.0, "Swipe left long-press skip active")
    await click_button(backlog.get("ExitBtn"))
    await require(adv.visible and not backlog.visible, "Original history close button failed")
    await require(adv.get("State") == 3, "Closing history let the same touch advance the dialogue underneath")

    # A normal tap waits for release, then advances exactly once.
    prepare_dialogue()
    await touch(start, true)
    await require(adv.get("State") == 3, "Tap advanced on press")
    await touch(start, false)
    await require(adv.get("State") == 0, "Tap did not advance on release")

    # Background above the dialogue box participates in the same gesture.
    prepare_dialogue()
    await swipe(Vector2(500, 300))
    await require(backlog.visible and adv.get("State") == 3, "Downward background swipe failed or advanced dialogue")
    await click_button(backlog.get("ExitBtn"))
    prepare_dialogue()
    await touch(Vector2(500, 300), true)
    await require(adv.get("State") == 3, "Background tap advanced before release")
    await touch(Vector2(500, 300), false)
    await require(adv.get("State") == 0, "Background tap did not advance on release")

    # Some devices only report the final position on release.
    prepare_dialogue()
    await touch(Vector2(500, 300), true)
    await touch(Vector2(500, 360), false)
    await require(backlog.visible and adv.get("State") == 3, "Release-only downward swipe failed or advanced dialogue")
    await click_button(backlog.get("ExitBtn"))

    for delta in [Vector2(70, 0), Vector2(0, -60)]:
        prepare_dialogue()
        await touch(start, true)
        await drag(start + delta, delta)
        await touch(start + delta, false)
        await require(adv.visible and adv.get("State") == 3, "Non-downward drag triggered history or advance")

    prepare_dialogue()
    await touch(start, true)
    await touch(start, false, 0, true)
    await require(adv.get("State") == 3, "Canceled touch advanced dialogue")
    await touch(start, true)
    await touch(Vector2(20, 20), true, 1)
    await drag(start + Vector2(0, 60), Vector2(0, 60))
    await touch(start + Vector2(0, 60), false)
    await touch(Vector2(20, 20), false, 1)
    await require(adv.visible and adv.get("State") == 3, "Multitouch triggered history or advance")

    # Losing focus cancels the pending tap and hold timer.
    await touch(start, true)
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_OUT)
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_IN)
    await touch(start, false)
    await require(adv.get("State") == 3 and not game.get("IsPressed"), "Focus loss retained a tap or hold")

    # Existing original buttons must remain visible and directly tappable.
    for field in ["SaveButton", "LoadButton", "OptionButton", "BackLogButton", "AutoButton", "SkipButton", "OffButton"]:
        await require(adv.get(field).is_visible_in_tree(), "Original shortcut missing: " + field)
    await click_button(adv.get("BackLogButton"))
    await require(backlog.visible, "Original history shortcut failed")
    await click_button(backlog.get("ExitBtn"))
    await click_button(adv.get("SaveButton"))
    var saves = ui.get("LoadSaveMenu")
    await require(saves.visible, "Original Save shortcut failed")
    await click_button(saves.get("ExitBtn"))
    await click_button(adv.get("LoadButton"))
    await require(saves.visible, "Original Load shortcut failed")
    await click_button(saves.get("ExitBtn"))
    await click_button(adv.get("OptionButton"))
    var options = ui.get("OptionsMenu")
    await require(options.visible, "Original Options shortcut failed")
    await click_button(options.get("ExitBtn"))

    var corner = adv.get_node_or_null("TouchMenuButton")
    if corner != null:
        prepare_dialogue()
        await click_button(corner)
        var pause = root.find_child("RemotePauseMenu", true, false)
        await require(pause != null and pause.visible, "Corner menu button failed")
        await require(adv.get("State") == 3, "Opening corner menu advanced dialogue")
        await touch(Vector2(20, 20), true)
        await touch(Vector2(20, 20), false)
        await require(not pause.visible and adv.get("State") == 3, "Closing pause menu leaked its touch to dialogue")

    # A menu taking over mid-press discards the pending dialogue tap.
    prepare_dialogue()
    await touch(start, true)
    ui.call("OpenOptionsMenu")
    await touch(start, false)
    await frames(40)
    await require(options.visible and adv.get("State") == 3, "Menu takeover retained the pending tap")
    await click_button(options.get("ExitBtn"))

    options.get("LongPressSkipYesBtn").emit_signal("button_down")
    await touch(start, true)
    await require(game.get("IsPressed"), "Dialogue hold did not start")
    game.set("PressedTime", 0.61)
    game.call("InputKeyHandling")
    await require(game.get("Skipping"), "Enabled hold-to-skip stopped working")
    await touch(start, false)
    await require(adv.get("State") == 3 and not game.get("IsPressed"), "Long-press release advanced text or retained the hold")
    game.call("InputKeyHandling")
    await require(not game.get("Skipping"), "Skip remained active after release")
    options.get("LongPressSkipNoBtn").emit_signal("button_down")

    game.set("SkipMode", true)
    await touch(start, true)
    await touch(start, false)
    await require(not game.get("SkipMode") and adv.get("State") == 3, "Stopping skip also advanced dialogue")

    game.call("BeginPortRemoteChoiceProbe")
    await touch(start, true)
    await drag(start + Vector2(0, 60), Vector2(0, 60))
    await touch(start + Vector2(0, 60), false)
    await require(adv.get("SelectMessageContainer").visible and not backlog.visible, "Swipe interfered with an active choice")
    game.call("EndPortRemoteChoiceProbe")
    game.set_process(false)

    prepare_dialogue()
    game.set("AutoMode", true)
    await swipe(start)
    await require(backlog.visible and not game.get("AutoMode"), "History swipe did not pause auto playback")
    await click_button(backlog.get("ExitBtn"))

    # The full-page novel uses its own history menu.
    game.call("InputKeyHandling")
    adv.call("SetNovelMode", true)
    prepare_dialogue()
    await swipe(Vector2(500, 300))
    var novel_log = ui.get("NovelBackLogMenu")
    await require(novel_log.visible, "Novel swipe opened the wrong history menu")
    await click_button(novel_log.get("ExitBtn"))
    adv.call("SetNovelMode", false)
    print("WA2_DIALOGUE_TOUCH passed: downward dialogue/background/novel swipes, release taps, direction rejection, cancel, multitouch, focus, menu takeover, hold/skip/auto, choices, original controls")
    for child in root.get_children(): child.queue_free()
    await frames()
    quit(0)
