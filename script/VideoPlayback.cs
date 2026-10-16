using System;
using Godot;

[GlobalClass]
public partial class VideoPlayback : Control
{
    private const float DefaultFrameRate = 30.0f;
    private const int StateStopped = 0;
    private const int StateLoading = 1;
    private const int StatePlaying = 2;
    private const int StatePaused = 3;

    [Signal] public delegate void FrameChangedEventHandler(int frameNr);
    [Signal] public delegate void NextFrameCalledEventHandler(int frameNr);
    [Signal] public delegate void VideoLoadedEventHandler();
    [Signal] public delegate void VideoEndedEventHandler();
    [Signal] public delegate void PlaybackStartedEventHandler();
    [Signal] public delegate void PlaybackPausedEventHandler();
    [Signal] public delegate void PlaybackReadyEventHandler();

    private GodotObject _nativePlayer;
    private Control _nativeControl;
    private string _path = string.Empty;
    private bool _loading;
    private bool _loaded;
    private bool _closing;
    private int _requestId;
    private int _currentFrame;
    private float _playbackSpeed = 1.0f;
    private bool _loop;
    private bool _enableAudio = true;

    [Export(PropertyHint.File, "*.wmv,*.pak")]
    public string Path
    {
        get => _path;
        set => SetVideoPath(value);
    }

    [Export]
    public bool EnableAudio
    {
        get => _enableAudio;
        set
        {
            _enableAudio = value;
            ApplyAudioSetting();
        }
    }

    [Export] public bool AudioSpeedToSync { get; set; }
    [Export] public bool EnableAutoPlay { get; set; }
    [Export] public float ForcedFramerate { get; set; }

    [Export(PropertyHint.Range, "0.25,4.0,0.05")]
    public float PlaybackSpeed
    {
        get => _playbackSpeed;
        set => _playbackSpeed = Mathf.Clamp(value, 0.25f, 4.0f);
    }

    [Export] public bool PitchAdjust { get; set; } = true;

    [Export]
    public bool Loop
    {
        get => _loop;
        set
        {
            _loop = value;
            _nativePlayer?.Call("set_loop", value);
        }
    }

    [Export] public bool Debug { get; set; }

    public bool IsPlaying { get; private set; }

    public int CurrentFrame
    {
        get => _currentFrame;
        set => SetCurrentFrame(value);
    }

    public override void _EnterTree()
    {
        // This full-screen renderer must not intercept menus or dialogue buttons.
        MouseFilter = MouseFilterEnum.Ignore;
        if (!ClassDB.ClassExists("WMVPlayer"))
        {
            GD.PrintErr("WMVPlayer native class is unavailable. Check addons/wmv_video and its runtime libraries.");
            return;
        }

        _nativePlayer = (GodotObject)ClassDB.Instantiate("WMVPlayer");
        _nativeControl = _nativePlayer as Control;
        if (_nativeControl == null)
        {
            GD.PrintErr("WMVPlayer could not be instantiated as a Control node.");
            _nativePlayer?.Dispose();
            _nativePlayer = null;
            return;
        }

        _nativeControl.Name = "WMVPlayer";
        _nativeControl.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _nativeControl.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_nativeControl);

        _nativePlayer.Connect("playback_started", Callable.From(OnNativePlaybackStarted));
        _nativePlayer.Connect("playback_paused", Callable.From(OnNativePlaybackPaused));
        _nativePlayer.Connect("playback_stopped", Callable.From(OnNativePlaybackStopped));
        _nativePlayer.Connect("playback_finished", Callable.From(OnNativePlaybackFinished));
        _nativePlayer.Connect("playback_error", Callable.From<string>(OnNativePlaybackError));
        _nativePlayer.Call("set_loop", _loop);
        ApplyAudioSetting();
    }

    public override void _Ready()
    {
        EmitSignal(SignalName.PlaybackReady);
        if (EnableAutoPlay && !string.IsNullOrEmpty(_path))
        {
            SetVideoPath(_path);
        }
    }

    public override void _Process(double delta)
    {
        if (!_loaded || _nativePlayer == null)
        {
            return;
        }

        int frame = Mathf.Max(0, Mathf.FloorToInt(GetStreamPosition() * GetVideoFramerate()));
        if (frame != _currentFrame)
        {
            SetCurrentFrame(frame);
            EmitSignal(SignalName.NextFrameCalled, frame);
        }

        int state = GetNativeState();
        IsPlaying = state == StatePlaying;
    }

    public override void _ExitTree()
    {
        Close();
    }

    public void SetVideoPath(string newPath)
    {
        Close();
        _path = ResolvePath(newPath);
        int requestId = ++_requestId;

        if (_nativePlayer == null)
        {
            EmitLoadFailureDeferred(requestId, "WMVPlayer native class is unavailable.");
            return;
        }
        if (string.IsNullOrEmpty(_path) || !FileAccess.FileExists(_path))
        {
            EmitLoadFailureDeferred(requestId, $"Video file does not exist: {_path}");
            return;
        }

        _loading = true;
        _loaded = false;
        _currentFrame = 0;
        _nativePlayer.Call("set_source", _path);
        _nativePlayer.Call("play");
        _nativePlayer.Call("set_paused", true);
    }

    public void Play()
    {
        if (_nativePlayer == null || (!_loaded && !_loading))
        {
            return;
        }

        int state = GetNativeState();
        if (state == StatePaused)
        {
            _nativePlayer.Call("set_paused", false);
            IsPlaying = true;
            EmitSignal(SignalName.PlaybackStarted);
        }
        else if (state == StateStopped)
        {
            _nativePlayer.Call("play");
        }
    }

    public void Pause()
    {
        if (_nativePlayer == null || !IsPlaying)
        {
            return;
        }
        _nativePlayer.Call("pause");
    }

    public void Close()
    {
        _requestId++;
        _loading = false;
        _loaded = false;
        IsPlaying = false;
        _currentFrame = 0;

        if (_nativePlayer == null)
        {
            return;
        }

        _closing = true;
        _nativePlayer.Call("stop");
        _nativePlayer.Call("set_source", string.Empty);
        _closing = false;
    }

    public bool IsOpen() => _loaded && _nativePlayer != null;

    public float GetVideoLength()
    {
        return _nativePlayer == null ? 0.0f : (float)_nativePlayer.Call("get_stream_length").AsDouble();
    }

    public float GetVideoFramerate()
    {
        return ForcedFramerate > 0.0f ? ForcedFramerate : DefaultFrameRate;
    }

    public int GetVideoFrameCount()
    {
        return Mathf.CeilToInt(GetVideoLength() * GetVideoFramerate());
    }

    public int GetVideoRotation() => 0;

    public void SeekFrame(int frame)
    {
        if (_nativePlayer == null || !_loaded)
        {
            return;
        }
        float frameRate = GetVideoFramerate();
        _nativePlayer.Call("set_stream_position", Mathf.Max(0, frame) / frameRate);
        SetCurrentFrame(Mathf.Max(0, frame));
    }

    public void SetCurrentFrame(int frame)
    {
        _currentFrame = frame;
        EmitSignal(SignalName.FrameChanged, frame);
    }

    public void SetPlaybackSpeed(float speed)
    {
        PlaybackSpeed = speed;
        if (!Mathf.IsEqualApprox(PlaybackSpeed, 1.0f) && Debug)
        {
            GD.Print("WMVPlayer currently plays at normal speed; PlaybackSpeed is retained for API compatibility.");
        }
    }

    public void SetPitchAdjust(bool enabled)
    {
        PitchAdjust = enabled;
    }

    private static string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("uid://", StringComparison.Ordinal))
        {
            return path ?? string.Empty;
        }
        return ResourceUid.GetIdPath(ResourceUid.TextToId(path));
    }

    private double GetStreamPosition()
    {
        return _nativePlayer == null ? 0.0 : _nativePlayer.Call("get_stream_position").AsDouble();
    }

    private int GetNativeState()
    {
        return _nativePlayer == null ? StateStopped : _nativePlayer.Call("get_playback_state").AsInt32();
    }

    private void ApplyAudioSetting()
    {
        _nativePlayer?.Call("set_volume_db", _enableAudio ? 0.0f : -80.0f);
    }

    private void OnNativePlaybackStarted()
    {
        if (_loading)
        {
            _loading = false;
            _loaded = true;
            IsPlaying = false;
            _currentFrame = 0;
            EmitSignal(SignalName.VideoLoaded);
            return;
        }

        _loaded = true;
        IsPlaying = true;
        EmitSignal(SignalName.PlaybackStarted);
    }

    private void OnNativePlaybackPaused()
    {
        IsPlaying = false;
        if (!_loading)
        {
            EmitSignal(SignalName.PlaybackPaused);
        }
    }

    private void OnNativePlaybackStopped()
    {
        if (!_closing)
        {
            IsPlaying = false;
        }
    }

    private void OnNativePlaybackFinished()
    {
        IsPlaying = false;
        SetCurrentFrame(GetVideoFrameCount());
        EmitSignal(SignalName.VideoEnded);
    }

    private void OnNativePlaybackError(string message)
    {
        int requestId = _requestId;
        EmitLoadFailureDeferred(requestId, message);
    }

    private async void EmitLoadFailureDeferred(int requestId, string message)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (requestId != _requestId)
        {
            return;
        }

        _loading = false;
        _loaded = false;
        IsPlaying = false;
        GD.PrintErr($"VideoPlayback: {message}");
        EmitSignal(SignalName.VideoLoaded);
    }
}
