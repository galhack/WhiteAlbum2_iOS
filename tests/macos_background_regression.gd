# SPDX-License-Identifier: Apache-2.0
# macOS rendered regression; use isolated user storage and local purchased data.
# godot --path . --audio-driver Dummy --script tests/macos_background_regression.gd -- --resources <main-folder> --language zh-Hans --play --start-script 1001
# For Special Contents, set WA2_FOCUS_VIDEO to <main-folder>/IC/mv010.pak.
extends SceneTree

var checks: Array[Dictionary] = []
var game: Node
var failures = 0
var original: Dictionary
var negative_deltas: Array[float] = []

func _process(delta: float) -> bool:
    if delta < 0: negative_deltas.append(delta)
    return false

func _initialize():
    run_review.call_deferred()

func frames(count = 4):
    for i in range(count): await process_frame

func check(ok: bool, name: String):
    checks.append({"name": name, "passed": ok})
    if not ok:
        failures += 1
        push_error(name)

func loop_settings() -> Dictionary:
    return {"render": RenderingServer.render_loop_enabled, "low": OS.low_processor_usage_mode,
        "sleep": OS.low_processor_usage_mode_sleep_usec, "fps": Engine.max_fps,
        "physics": Engine.physics_ticks_per_second}

func save_result():
    var f = FileAccess.open("user://macos-background-result.json", FileAccess.WRITE)
    f.store_string(JSON.stringify({"checks": checks, "failures": failures, "platform": OS.get_name(),
        "negative_deltas": negative_deltas}, "  "))
    f.close()

func run_review():
    if OS.get_name() != "macOS":
        print("WA2_BACKGROUND_REGRESSION skipped: macOS only")
        quit(0)
        return
    var launcher = load("res://scene/Launcher.tscn").instantiate()
    root.add_child(launcher)
    current_scene = launcher
    for i in range(2400):
        if is_instance_valid(current_scene) and current_scene != launcher:
            game = current_scene
            break
        await process_frame
    if not is_instance_valid(game):
        check(false, "Launch actual game scene")
        save_result()
        quit(1)
        return
    await frames(30)
    game.set_process(false)
    var video = game.get("VideoPlayer")
    video.call("Close")
    game.get("SoundMgr").call("StopAll")
    original = loop_settings()
    Engine.max_fps = 120
    Engine.physics_ticks_per_second = 60
    OS.low_processor_usage_mode = false
    OS.low_processor_usage_mode_sleep_usec = 3333
    var expected = loop_settings()
    var sound = AudioStreamWAV.new()
    sound.format = AudioStreamWAV.FORMAT_16_BITS
    sound.mix_rate = 8000
    var pcm = PackedByteArray()
    pcm.resize(8000 * 2 * 60)
    sound.data = pcm
    var audio = AudioStreamPlayer.new()
    audio.stream = sound
    game.add_child(audio)
    audio.play()
    var prepaused: AudioStreamPlayer
    var special = game.get("UiMgr").find_child("SpecialContentsMenu", true, false) != null
    if special:
        var drama = load("res://script/Wa2DramaPlayer.cs").new()
        game.get("UiMgr").add_child(drama)
        drama.call("Open")
        drama.call("PlayChapter", 0)
        drama.call("TogglePause")
        prepaused = drama.get_node("DramaAudio")
    else:
        prepaused = AudioStreamPlayer.new()
        prepaused.stream = sound
        game.add_child(prepaused)
        prepaused.play()
        prepaused.stream_paused = true
    var timer = Timer.new()
    timer.wait_time = 100
    game.add_child(timer)
    timer.start()
    await frames(20)
    check(audio.playing and not audio.stream_paused, "Control audio starts")
    check(prepaused.stream_paused, "Explicitly paused audio starts paused")
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_OUT)
    check(paused, "Application focus loss pauses scene tree")
    check(not RenderingServer.render_loop_enabled and OS.low_processor_usage_mode,
        "Background disables render loop and enables low processor mode")
    check(Engine.max_fps == 4 and Engine.physics_ticks_per_second == expected.physics and OS.low_processor_usage_mode_sleep_usec == 250000,
        "Background throttles render loop without changing physics cadence")
    var position = audio.get_playback_position()
    var remaining = timer.time_left
    await frames(5)
    check(abs(audio.get_playback_position() - position) < 0.06, "Normal audio clock stays paused")
    check(is_equal_approx(timer.time_left, remaining), "Scene timer stays paused")
    game.notification(Node.NOTIFICATION_WM_WINDOW_FOCUS_OUT)
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_OUT)
    game.notification(Node.NOTIFICATION_WM_WINDOW_FOCUS_IN)
    check(not paused, "Window focus resumes application background pause")
    check(loop_settings() == expected, "Duplicate focus-out preserves original engine settings")
    check(not audio.stream_paused, "Previously playing audio resumes")
    check(prepaused.stream_paused, "User-paused audio remains paused after tree resume")
    await frames(15)
    check(audio.get_playback_position() > position + 0.03, "Resumed audio clock advances")
    check(timer.time_left < remaining, "Resumed timer advances")
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_IN)
    check(loop_settings() == expected, "Duplicate focus-in does not mutate engine settings")
    paused = true
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_OUT)
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_IN)
    check(paused, "Pre-existing external tree pause is retained")
    check(loop_settings() == expected, "External tree pause is not claimed or throttled")
    paused = false
    prepaused.stream_paused = true
    game.notification(Node.NOTIFICATION_WM_WINDOW_FOCUS_OUT)
    check(paused, "Window focus-out alone pauses scene")
    game.notification(Node.NOTIFICATION_WM_WINDOW_FOCUS_IN)
    check(not paused and prepaused.stream_paused, "Window focus-in restores audio pause intent")

    # Exercise the real native WMV clock with local purchased opening data.
    var args = OS.get_cmdline_user_args()
    var resource_dir = args[args.find("--resources") + 1]
    var movie = resource_dir.path_join("IC/mv010.pak")
    if not OS.get_environment("WA2_FOCUS_VIDEO").is_empty():
        movie = OS.get_environment("WA2_FOCUS_VIDEO")
    video.call("SetVideoPath", movie)
    for i in range(1200):
        if video.call("IsOpen"): break
        await process_frame
    check(video.call("IsOpen"), "WMV native decoder opens local movie")
    video.call("Play")
    await frames(45)
    check(video.get("IsPlaying"), "WMV starts before focus loss")
    var native = video.get_node("WMVPlayer")
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_OUT)
    check(native.call("is_paused"), "Focus loss explicitly pauses independent WMV clock")
    position = native.call("get_stream_position")
    await frames(5)
    check(abs(float(native.call("get_stream_position")) - position) < 0.06, "WMV clock stays stable in background")
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_IN)
    await frames(20)
    check(video.get("IsPlaying") and not native.call("is_paused"), "Previously playing WMV resumes")
    check(float(native.call("get_stream_position")) > position + 0.03, "WMV clock advances after focus restore")
    video.call("Pause")
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_OUT)
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_IN)
    check(not video.get("IsPlaying") and native.call("is_paused"), "Previously paused WMV is not restarted")
    video.call("Close")

    # A player deleted in the background must not be accessed after free.
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_OUT)
    if not special: prepaused.free()
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_IN)
    if special:
        check(not paused and prepaused.stream_paused, "Real radio-drama audio retains its manual pause")
    else:
        check(not paused and loop_settings() == expected, "Deleted paused audio is skipped safely")

    # Exit the real game scene while it owns the background pause.
    game.notification(MainLoop.NOTIFICATION_APPLICATION_FOCUS_OUT)
    root.remove_child(game)
    game.free()
    check(not paused, "Removing background game scene releases owned pause")
    check(loop_settings() == expected, "Removing background game restores engine loop settings")
    Engine.max_fps = original.fps
    Engine.physics_ticks_per_second = original.physics
    OS.low_processor_usage_mode = original.low
    OS.low_processor_usage_mode_sleep_usec = original.sleep
    RenderingServer.render_loop_enabled = original.render
    check(negative_deltas.is_empty(), "Focus transitions never deliver negative frame deltas")
    save_result()
    print("WA2_BACKGROUND_REGRESSION checks=", checks.size(), " failures=", failures, " special=", special)
    quit(1 if failures else 0)
