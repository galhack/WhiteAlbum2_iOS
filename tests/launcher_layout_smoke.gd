# SPDX-License-Identifier: Apache-2.0
# Renders the launcher (optionally with WA2_UI_PLATFORM=iOS/tvOS sizes) and
# checks that its content fits the 1280x720 canvas.
extends SceneTree

func _initialize() -> void:
    run.call_deferred()

func run() -> void:
    var launcher = load("res://scene/Launcher.tscn").instantiate()
    root.add_child(launcher)
    current_scene = launcher
    for i in range(30):
        await process_frame
    var margin: Control = launcher.get_child(0)
    var needed: float = margin.get_combined_minimum_size().y
    RenderingServer.force_draw(false)
    var platform := OS.get_environment("WA2_UI_PLATFORM")
    var path := "res://build/launcher-%s.png" % (platform if platform != "" else "desktop")
    root.get_texture().get_image().save_png(path)
    if needed > 720.0:
        push_error("Launcher needs %.0f px, more than the 720 px canvas" % needed)
        quit(1)
        return
    var licenses: Button = null
    for button in launcher.find_children("*", "Button", true, false):
        if "Licenses" in button.text: licenses = button
    licenses.emit_signal("pressed")
    await process_frame
    # The language OptionButton's popup also contains PanelContainers; pick ours.
    var panel = null
    for candidate in launcher.find_children("*", "PanelContainer", true, false):
        if candidate.has_method("Open"): panel = candidate
    var text: TextEdit = panel.find_children("*", "TextEdit", true, false)[0]
    if not panel.visible or not ("FFmpeg 7.1.5" in text.text) or not ("LESSER GENERAL PUBLIC LICENSE" in text.text):
        push_error("Licence panel did not show the FFmpeg/LGPL notices")
        quit(1)
        return
    var escape := InputEventKey.new()
    escape.keycode = KEY_ESCAPE
    escape.pressed = true
    root.push_input(escape, true)
    await process_frame
    if panel.visible:
        push_error("Escape did not close the licence panel")
        quit(1)
        return
    print("WA2_LAUNCHER_LAYOUT passed: %.0f px of 720 (%s) -> %s" % [needed, platform, path])
    quit(0)
