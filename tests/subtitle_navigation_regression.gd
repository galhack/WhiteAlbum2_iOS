# SPDX-License-Identifier: Apache-2.0
extends SceneTree

func _initialize() -> void:
    check_game.call_deferred()

func frames(count: int = 8) -> void:
    for frame in range(count): await process_frame

func click(control: Control) -> void:
    var point = control.get_global_rect().get_center()
    for pressed in [true, false]:
        var event = InputEventMouseButton.new()
        event.button_index = MOUSE_BUTTON_LEFT
        event.pressed = pressed
        event.position = point
        event.global_position = point
        root.push_input(event, true)
        await process_frame
    await create_timer(0.5).timeout

func check_game() -> void:
    var launcher = load("res://scene/Launcher.tscn").instantiate()
    root.add_child(launcher)
    current_scene = launcher
    await frames(120)
    var game = current_scene
    var ui = game.get("UiMgr")
    var special = ui.find_child("SpecialContentsMenu", true, false)
    if special != null:
        for button in special.find_children("*", "Button", true, false):
            if button.text.begins_with("Options"):
                await click(button)
                break
        await create_timer(0.5).timeout
        var options = ui.get("OptionsMenu")
        if not options.visible:
            push_error("Special Contents settings button did not open settings")
            quit(1)
            return
        # Confirmations must also remain above a reordered settings page.
        await click(options.get("DefaultBtn"))
        var confirm = ui.get("UIConfirm")
        if not confirm.visible:
            push_error("Reset settings did not open confirmation")
            quit(1)
            return
        await click(confirm.get("CancelBtn"))
        if confirm.visible:
            push_error("Pointer cannot cancel confirmation above settings")
            quit(1)
            return
        await click(options.get("ExitBtn"))
        if options.visible:
            push_error("Pointer cannot return from Special Contents settings")
            quit(1)
            return
        print("WA2_SETTINGS_POINTER_RETURN passed")
    else:
        var manager = game.get("SubtitleMgr")
        var label = manager.get("TextLabel")
        var seen_station = false
        var seen_voice = false
        for frame in range(12000):
            if frame % 120 == 0:
                print("WA2_CC_DIAGNOSTIC ", game.call("GetPortDiagnostics"), " label=", game.get("Label"))
                if game.get("VideoPlayer").get("IsPlaying"): game.call("OnVideoFinished")
                game.call("ClickAdv", true)
            if label.text.length() > 0 and label.text != "峰城广播站":
                seen_voice = true
                print("WA2_CC_CAPTION ", label.text)
                RenderingServer.force_draw(false)
                root.get_texture().get_image().save_png("res://build/cc-caption-regression.png")
                break
            if label.text == "峰城广播站": seen_station = true
            await process_frame
        if not seen_station or not seen_voice:
            push_error("CC opening missed station or voice subtitle through script playback")
            quit(1)
            return
        print("WA2_CC_SCRIPT_CAPTION passed")
    for child in root.get_children(): child.queue_free()
    await frames()
    quit(0)
