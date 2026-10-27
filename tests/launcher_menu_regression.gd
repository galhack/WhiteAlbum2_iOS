# SPDX-License-Identifier: Apache-2.0
# Run with --resources and an isolated user data directory, without --play.
extends SceneTree

func _initialize() -> void:
    run.call_deferred()

func frames(count: int = 4) -> void:
    for frame in range(count): await process_frame

func require(condition: bool, message: String) -> void:
    if not condition:
        push_error(message)
        quit(1)
        await process_frame
        assert(false, message)

func launcher() -> Control:
    var scene = load("res://scene/Launcher.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    return scene

func run() -> void:
    # A pre-existing config without the new key must retain the visible menu.
    var legacy := ConfigFile.new()
    legacy.set_value("game", "language", "ja")
    await require(legacy.save("user://launcher.cfg") == OK, "Cannot write isolated launcher config")
    var scene := launcher()
    await frames()
    var toggle: CheckButton = scene.find_child("ShowTouchMenu", true, false)
    await require(toggle != null and toggle.button_pressed, "Legacy config must default to a visible corner menu")

    # The launcher applies and saves both directions of the preference.
    for show_menu in [false, true]:
        toggle.button_pressed = show_menu
        RenderingServer.force_draw(false)
        root.get_texture().get_image().save_png("res://build/launcher-menu-%s.png" % str(show_menu))
        scene.call("Play")
        await frames(120)
        var game = current_scene
        await require(game != scene, "Launcher failed to start the game")
        await frames()
        var button = game.get("AdvMain").get_node_or_null("TouchMenuButton")
        await require((button != null) == show_menu, "Corner menu visibility differs from launcher selection")
        var saved := ConfigFile.new()
        await require(saved.load("user://launcher.cfg") == OK, "Launcher config was not saved")
        await require(saved.get_value("game", "show_touch_menu") == show_menu, "Menu preference was not persisted")

        # A new launcher reads the saved value and lets the user enable it again.
        var reopened := launcher()
        await frames()
        var restored: CheckButton = reopened.find_child("ShowTouchMenu", true, false)
        await require(restored.button_pressed == show_menu, "New launcher did not restore the menu preference")
        reopened.queue_free()
        game.queue_free()
        await frames()
        current_scene = scene
        scene.show()
    print("WA2_LAUNCHER_MENU passed: legacy default, hidden/visible game button, saved preference and launcher reload")
    for child in root.get_children(): child.queue_free()
    await frames()
    quit(0)
