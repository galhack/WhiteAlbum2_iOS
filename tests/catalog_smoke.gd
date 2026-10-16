# SPDX-License-Identifier: Apache-2.0
extends SceneTree

func _initialize() -> void:
    check_catalog.call_deferred()

func check_catalog() -> void:
    var launcher = load("res://scene/Launcher.tscn").instantiate()
    root.add_child(launcher)
    current_scene = launcher
    for frame in range(120):
        await process_frame
    var menu = root.find_child("SpecialContentsMenu", true, false)
    if menu == null or not menu.is_visible_in_tree():
        push_error("Special Contents catalog is not visible.")
        quit(1)
        return
    if DisplayServer.get_name() != "headless":
        RenderingServer.force_draw(false)
        root.get_texture().get_image().save_png("res://build/sc-catalog.png")
    var buttons = menu.find_children("*", "Button", true, false)
    # Exercise GUI hit testing: a transparent video overlay must not intercept
    # clicks intended for the catalog underneath it.
    var position = buttons[0].get_global_rect().get_center()
    var motion = InputEventMouseMotion.new()
    motion.position = position
    root.push_input(motion, true)
    for pressed in [true, false]:
        var click = InputEventMouseButton.new()
        click.position = position
        click.button_index = MOUSE_BUTTON_LEFT
        click.pressed = pressed
        root.push_input(click, true)
    await process_frame
    var state = current_scene.call("GetPortDiagnostics")
    if state["state"] != "GAME" or state["script"] != "6001":
        var hovered = root.gui_get_hovered_control()
        var hovered_path = str(hovered.get_path()) if hovered != null else "none"
        push_error("Clicking the first catalog entry did not launch Kazusa mini-after; hovered=" + hovered_path)
        quit(1)
        return
    print("WA2_CATALOG_SELECTION passed")
    quit(0)
