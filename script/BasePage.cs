
using System.Collections.Generic;
using Godot;
public partial class BasePage : Control
{
  [Export]
  public BgmPlayer BgmPlayer;
  [Export]
  public Wa2Button ExitBtn;
  [Export]
  public AnimationPlayer AnimationPlayer;
  public Wa2EngineMain _engine;
  private List<Wa2Button> _buttons;

  public virtual void Open()
  {
    DisabledAllButton();
    AnimationPlayer.Play("open");
    BgmPlayer.Show();

  }
  public override void _Ready()
  {
    _buttons = GetAllButtons(this);
    _engine = Wa2EngineMain.Engine;
    ExitBtn.ButtonDown += OnExitBtnDown;
    AnimationPlayer.AnimationFinished += OnAnimationFinished;
  }
  public List<Wa2Button> GetAllButtons(Node node, List<Wa2Button> list = null)
  {
    list ??= new List<Wa2Button>();
    foreach (Node child in node.GetChildren())
    {
      if (child is Wa2Button btn)
        list.Add(btn);
      GetAllButtons(child, list);
    }
    return list;
  }
  public virtual void Close()
  {
    DisabledAllButton();
    AnimationPlayer.Play("close");
    BgmPlayer.Hide();
    // _engine.UiMgr.UiQueue.Pop();
  }
  public virtual void FaseClose()
  {

    _engine.UiMgr.ReturnScene();
  }
  // Apple port: list menus scroll by touch drag, mouse wheel and trackpad pan.
  private float _dragAccumulated, _panAccumulated;
  protected int ScrollStepsFor(InputEvent input)
  {
    if (_engine == null || !Visible || _engine.UiMgr.UiQueue.Count == 0 || _engine.UiMgr.UiQueue.Peek() != this) return 0;
    switch (input)
    {
      case InputEventScreenTouch { Pressed: true }:
        _dragAccumulated = 0;
        return 0;
      case InputEventScreenDrag drag:
        return Wa2TouchInput.DragSteps(ref _dragAccumulated, drag.Relative.Y);
      case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
        return -1;
      case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
        return 1;
      case InputEventPanGesture pan:
        return Wa2TouchInput.DragSteps(ref _panAccumulated, -pan.Delta.Y, 2f);
      default:
        return 0;
    }
  }
  protected void ScrollBy(ScrollBar bar, InputEvent input)
  {
    int steps = ScrollStepsFor(input);
    if (steps == 0) return;
    bar.Value = Mathf.Clamp(bar.Value + steps, bar.MinValue, bar.MaxValue);
    GetViewport().SetInputAsHandled();
  }
  public void OnExitBtnDown()
  {
    Close();

  }
  public virtual void OnAnimationFinished(StringName anime)
  {
    if (anime == "close")
    {
      OnCloseAnimationFinished();
    }
    if (anime == "open")
    {
      OnOpenAnimationFinished();
    }
  }
  public virtual void OnCloseAnimationFinished()
  {
    _engine.UiMgr.ReturnScene();
  }
  public virtual void OnOpenAnimationFinished()
  {
    EnabledAllButton();
  }
  public void DisabledAllButton()
  {
    foreach (Wa2Button btn in _buttons)
    {
      btn.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

  }
  public void EnabledAllButton()
  {
    foreach (Wa2Button btn in _buttons)
    {
      btn.MouseFilter = Control.MouseFilterEnum.Stop;
    }
  }
}