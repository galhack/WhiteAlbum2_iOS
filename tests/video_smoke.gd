# SPDX-License-Identifier: Apache-2.0
# Plays the first opening video without skipping and checks frames advance.
extends SceneTree

var frames := 0
var first_frame := -1
var first_seen := -1

func _initialize() -> void:
    setup.call_deferred()

func setup() -> void:
    var launcher = load("res://scene/Launcher.tscn").instantiate()
    root.add_child(launcher)
    current_scene = launcher

func _process(_delta: float) -> bool:
    frames += 1
    var game = current_scene
    if frames > 6000:
        push_error("Video never advanced.")
        quit(1)
        return false
    if game == null or game.name != "EngineMain":
        return false
    var player = game.get("VideoPlayer")
    if player == null or not player.get("IsPlaying"):
        return false
    var current: int = player.get("CurrentFrame")
    if first_frame < 0:
        first_frame = current
        first_seen = frames
        return false
    if frames - first_seen < 240:
        return false
    if current <= first_frame + 60:
        push_error("Video frames did not advance: %d -> %d" % [first_frame, current])
        quit(1)
        return false
    RenderingServer.force_draw(false)
    root.get_texture().get_image().save_png("res://build/video_smoke.png")
    print("WA2_VIDEO_SMOKE passed: frame %d -> %d over %d engine frames" % [first_frame, current, frames - first_seen])
    quit(0)
    return false
