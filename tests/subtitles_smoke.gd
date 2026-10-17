# SPDX-License-Identifier: Apache-2.0
extends SceneTree

func _initialize() -> void:
    check_subtitles.call_deferred()

func frames(count: int = 8) -> void:
    for frame in range(count): await process_frame

func key(code: Key) -> void:
    for pressed in [true, false]:
        var event = InputEventKey.new()
        event.keycode = code
        event.pressed = pressed
        root.push_input(event, true)
        await process_frame
    await frames()

func require(ok: bool, message: String) -> bool:
    if ok: return true
    push_error(message)
    quit(1)
    return false

func check_subtitles() -> void:
    var launcher = load("res://scene/Launcher.tscn").instantiate()
    root.add_child(launcher)
    current_scene = launcher
    await frames(120)
    var game = current_scene
    if not require(game != launcher, "Subtitle test must launch with --play and original resource path"): return
    var manager = game.get("SubtitleMgr")
    var japanese = "ja" in OS.get_cmdline_user_args()
    # Detect the edition using its actual UI, avoiding custom types over interop.
    var special = game.get("UiMgr").find_child("SpecialContentsMenu", true, false) != null
    if special:
        var player = load("res://script/Wa2DramaPlayer.cs").new()
        game.get("UiMgr").add_child(player)
        player.call("Open")
        if not require(player.find_child("Drama80Chapter0", true, false) != null, "Drama chapter selector missing"): return
        await key(KEY_DOWN)
        if not require(root.gui_get_focus_owner().name == "Drama80Chapter0", "Remote focus did not reach drama chapter"): return
        await key(KEY_ENTER)
        await frames()
        var audio = player.get_node("DramaAudio")
        audio.seek(3.0)
        await frames()
        var caption = player.find_child("DramaCaption", true, false)
        if not require(audio.playing and (caption.text.length() == 0 if japanese else caption.text.length() > 0), "Drama caption or language isolation failed"): return
        await key(KEY_P)
        if not require(audio.stream_paused, "Drama pause failed"): return
        await key(KEY_P)
        player.call("PlayChapter", 2)
        audio.seek(457.1)
        await frames()
        var song = player.find_child("SongCaption", true, false)
        if not require(song.text.length() == 0 if japanese else song.text.length() > 0, "Song captions failed"): return
        if not require(caption.text.length() == 0 if japanese else caption.text.length() > 0, "Concurrent drama dialogue failed"): return
        await key(KEY_ESCAPE)
        await frames()
        if not require(not is_instance_valid(player), "Closing drama player did not release playback"): return
    else:
        var audio = load("res://script/Wa2Audio.cs").new()
        var silence = AudioStreamWAV.new()
        silence.format = AudioStreamWAV.FORMAT_16_BITS
        silence.mix_rate = 8000
        var data = PackedByteArray()
        data.resize(8000 * 2 * 8)
        silence.data = data
        game.add_child(audio)
        audio.stream = silence
        audio.play()
        manager.call("ListenSe", 8365, audio)
        await frames()
        var label = manager.get("TextLabel")
        if not require(label.text.length() == 0 if japanese else label.text.length() > 0, "Main subtitle or Japanese isolation failed"): return
        if "Override test" == label.text: print("WA2_SUBTITLE_EXTERNAL_OVERRIDE passed")
        audio.seek(5.5)
        await frames()
        if not require(label.text == "", "Subtitle remained after interval end"): return
        audio.seek(0.3)
        await frames()
        if not require(label.text.length() == 0 if japanese else label.text.length() > 0, "Seek back failed"): return
        audio.stop()
        await frames()
        if not require(label.text == "", "Subtitle remained after audio stop"): return
    print("WA2_SUBTITLE_PLAYBACK passed special=", special, " japanese=", japanese)
    for child in root.get_children(): child.queue_free()
    await frames()
    quit(0)
