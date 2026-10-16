# SPDX-License-Identifier: Apache-2.0
extends SceneTree

var frames := 0
var captured := false
var dialogue_frame := -1
const MAX_FRAMES := 18000 # Coda has a long timed opening before its first dialogue.

func _initialize() -> void:
    setup.call_deferred()

func setup() -> void:
    var launcher = load("res://scene/Launcher.tscn").instantiate()
    root.add_child(launcher)
    current_scene = launcher

func _process(_delta: float) -> bool:
    frames += 1
    if frames < 300 or captured:
        return false
    var game = current_scene
    if game == null or game.name != "EngineMain":
        if frames > MAX_FRAMES:
            push_error("Smoke test never loaded the game scene.")
            quit(1)
        return false
    if frames % 600 == 0:
        print("WA2_DIAGNOSTICS ", game.call("GetPortDiagnostics"))
    if dialogue_frame < 0 and frames % 120 == 0 and game.get("VideoPlayer").get("IsPlaying"):
        game.call("OnVideoFinished")
    var adv = game.get("AdvMain")
    var text = adv.get("TextLabel").get("Text")
    if text == null or str(text).is_empty():
        if frames > MAX_FRAMES:
            captured = true
            push_error("Smoke test never reached dialogue.")
            quit(1)
        return false
    if dialogue_frame < 0:
        dialogue_frame = frames
    if frames - dialogue_frame < 180:
        return false
    captured = true
    capture.call_deferred(str(text).length())
    return false

func capture(text_length: int) -> void:
    # An occluded macOS window may stop presenting frames. Draw explicitly so
    # an unattended test cannot wait forever for frame_post_draw.
    await process_frame
    RenderingServer.force_draw(false)
    var image = root.get_texture().get_image()
    var path = "res://build/smoke.png"
    if image == null or image.save_png(path) != OK:
        push_error("Smoke screenshot failed.")
        quit(1)
        return
    print("WA2_SMOKE_CAPTURE dialogue_length=", text_length, " screenshot=", path)
    if not current_scene.call("VerifyPortSaveRoundTrip"):
        push_error("Save/load round trip changed script position, dialogue or local variables.")
        quit(1)
        return
    print("WA2_SAVE_ROUNDTRIP passed")
    quit(0)
