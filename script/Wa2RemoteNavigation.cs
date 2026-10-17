// SPDX-License-Identifier: Apache-2.0
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Wa2RemoteNavigation : Control
{
#if GODOT_TVOS
    // Exported by the patched tvOS engine; resolved through Wa2TvStorage's
    // __Internal resolver, which is installed before the launcher builds UI.
    [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void wa2_set_menu_passthrough(int enabled);
#endif
    private Control _scope;
    private BaseButton _focused;
    // The Siri Remote is the only input on tvOS, so show focus from launch.
    private bool _remoteActive = Wa2TvStorage.IsEnabled;
    private bool? _menuPassthrough;
    private static Wa2RemotePauseMenu _pause;
    private double _searchCooldown;
    private Rect2? _ring;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public static void Ensure(Window root)
    {
        if (root.GetNodeOrNull("RemoteNavigationLayer") != null) return;
        var layer = new CanvasLayer { Name = "RemoteNavigationLayer", Layer = 100 };
        root.AddChild(layer);
        layer.AddChild(new Wa2RemoteNavigation { Name = "RemoteNavigation" });
    }

    private Control ActiveScope()
    {
        var game = Wa2EngineMain.Engine;
        if (game == null) return GetTree().CurrentScene as Control;
        if (game.UiMgr.UiQueue.Count == 0) return null;
        var menu = game.UiMgr.UiQueue.Peek();
        if (menu == game.AdvMain)
            return game.AdvMain.SelectMessageContainer.IsVisibleInTree() ? game.AdvMain.SelectMessageContainer : null;
        return menu;
    }

    private static bool Available(BaseButton button, Control scope)
    {
        if (!GodotObject.IsInstanceValid(button) || button.IsQueuedForDeletion() || !button.IsVisibleInTree() || button.Disabled ||
            button.MouseFilter == MouseFilterEnum.Ignore || !scope.IsAncestorOf(button)) return false;
        for (Node node = button; node != null; node = node.GetParent())
        {
            if (node is CanvasItem item && item.Modulate.A < 0.1f) return false;
            if (node == scope) break;
        }
        return button.GetGlobalRect().Size.X > 0 && button.GetGlobalRect().Size.Y > 0;
    }

    private List<BaseButton> Buttons(Control scope) => scope == null ? new() :
        scope.FindChildren("*", "BaseButton", true, false).OfType<BaseButton>().Where(b => Available(b, scope)).ToList();

    private void Focus(BaseButton button)
    {
        _focused = button;
        button.FocusMode = FocusModeEnum.All;
        button.GrabFocus();
        for (Node node = button.GetParent(); node != null; node = node.GetParent())
            if (node is ScrollContainer scroll) { scroll.EnsureControlVisible(button); break; }
    }

    public override void _Process(double delta)
    {
        var scope = ActiveScope();
        if (_scope != scope || (_focused != null && (scope == null || !Available(_focused, scope))))
        {
            if (GodotObject.IsInstanceValid(_focused)) _focused.ReleaseFocus();
            _focused = null;
            _scope = scope;
            _searchCooldown = 0;
        }
        _searchCooldown -= delta;
        if (_remoteActive && _focused == null && _searchCooldown <= 0)
        {
            // Menus fade in over several frames; a 0.1 s retry avoids walking
            // the whole tree every frame while nothing is focusable yet.
            _searchCooldown = 0.1;
            var buttons = Buttons(scope);
            if (scope is LoadSaveMenu saves)
                // Slot panels fade in after other controls. Wait for the slots
                // rather than letting a transient Back/page button take focus.
                buttons = buttons.Where(b => saves.DataSlots.IsAncestorOf(b)).ToList();
            if (buttons.Count > 0) { Focus(buttons[0]); _searchCooldown = 0; }
        }
        UpdateMenuPassthrough(scope);
        Rect2? ring = _remoteActive && _scope != null && _focused != null && Available(_focused, _scope)
            ? _focused.GetGlobalRect().Grow(5) : null;
        if (ring != _ring)
        {
            _ring = ring;
            QueueRedraw();
        }
    }

    // tvOS HIG: Menu on the app's root screen returns to the Home screen.
    private void UpdateMenuPassthrough(Control scope)
    {
        if (!Wa2TvStorage.IsEnabled) return;
        bool root = scope is Wa2Launcher || scope is Wa2SpecialMenu ||
            (scope is TitleMenu title && title.MenuBttons.IsVisibleInTree());
        if (_menuPassthrough == root) return;
        _menuPassthrough = root;
#if GODOT_TVOS
        wa2_set_menu_passthrough(root ? 1 : 0);
#endif
    }

    public override void _Draw()
    {
        if (_ring is not Rect2 rect) return;
        DrawRect(rect.Grow(2), Colors.Black, false, 7);
        DrawRect(rect, new Color(0.2f, 0.85f, 1), false, 4);
    }

    public override void _Input(InputEvent input)
    {
        if (input is InputEventMouseMotion or InputEventMouseButton or InputEventScreenTouch)
        {
            if (!Wa2TvStorage.IsEnabled) _remoteActive = false;
            return;
        }
        // Popup windows (e.g. the language selector) use Godot's own focus/input.
        var owner = GetViewport().GuiGetFocusOwner();
        if (owner is LineEdit && !_remoteActive) return;
        // Read-only text (licenses) scrolls with the arrows; Menu closes its panel.
        if (owner is TextEdit && owner.IsVisibleInTree()) return;
        if (owner is OptionButton option && option.GetPopup().Visible) return;
        if (owner != null && owner.GetWindow() != GetWindow()) return;
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.P } && ActiveScope() is Wa2DramaPlayer drama)
        {
            drama.TogglePause();
            GetViewport().SetInputAsHandled();
            return;
        }
        // Play/Pause toggles auto-advance while reading; elsewhere it acts as Menu.
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.P } && ActiveScope() == null &&
            Wa2EngineMain.Engine is { State: Wa2EngineMain.GameState.GAME } reading && !reading.VideoPlayer.IsPlaying)
        {
            _remoteActive = true;
            reading.AdvMain.OnAutoButtonDown();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (input.IsActionPressed("ui_cancel") || input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.P } ||
            input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Start })
        {
            _remoteActive = true;
            Back();
            GetViewport().SetInputAsHandled();
            return;
        }
        int dx = input.IsActionPressed("ui_right", true) ? 1 : input.IsActionPressed("ui_left", true) ? -1 : 0;
        int dy = input.IsActionPressed("ui_down", true) ? 1 : input.IsActionPressed("ui_up", true) ? -1 : 0;
        bool accept = input.IsActionPressed("ui_accept");
        if (dx == 0 && dy == 0 && !accept) return;
        _remoteActive = true;
        var game = Wa2EngineMain.Engine;
        var scope = ActiveScope();
        // Up/down scroll the backlog list; left/right still move between buttons.
        if (dy != 0 && scope is BackLogMenu or NovelBackLogMenu)
        {
            var bar = scope is BackLogMenu log ? log.ScrollBar : ((NovelBackLogMenu)scope).ScrollBar;
            bar.Value = Mathf.Clamp(bar.Value + dy, bar.MinValue, bar.MaxValue);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (accept && scope == null && game != null)
        {
            Advance(game);
            GetViewport().SetInputAsHandled();
            return;
        }
        var buttons = Buttons(scope);
        if (buttons.Count == 0) { GetViewport().SetInputAsHandled(); return; }
        if (owner is BaseButton focused && buttons.Contains(focused)) _focused = focused;
        int index = buttons.IndexOf(_focused);
        var bounds = buttons.Select(b => { var r = b.GetGlobalRect(); return new Wa2FocusRect(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y); }).ToArray();
        Focus(buttons[Wa2FocusNavigation.Next(bounds, index, dx, dy)]);
        _scope = scope;
        // Let BaseButton process ui_accept normally, including toggle groups,
        // button_down/pressed timing and release events.
        if (!accept) GetViewport().SetInputAsHandled();
    }

    private static void Advance(Wa2EngineMain game)
    {
        if (game.State is Wa2EngineMain.GameState.LOGO or Wa2EngineMain.GameState.OP)
        {
            if (game.VideoPlayer.IsPlaying) game.HideVideo();
            if (game.State == Wa2EngineMain.GameState.LOGO) game.State = Wa2EngineMain.GameState.OP;
            else game.UiMgr.OpenTitleMenu();
            return;
        }
        if (game.SkipMode) { game.StopSkip(); return; }
        if (game.AdvMain.State == Wa2AdvMain.AdvState.HIDE)
        {
            game.AdvMain.Show();
            game.AdvMain.State = Wa2AdvMain.AdvState.WAIT_CLICK;
            return;
        }
        game.ClickAdv(true);
    }

    private void Back()
    {
        var game = Wa2EngineMain.Engine;
        if (game == null || game.UiMgr.UiQueue.Count == 0) return;
        var menu = game.UiMgr.UiQueue.Peek();
        if (menu is Wa2RemotePauseMenu pause) pause.Close();
        else if (menu is Wa2DramaPlayer drama) drama.Close();
        else if (menu is Wa2LocalSaveMenu localSaves) localSaves.Close();
        else if (menu is UIConfirm confirm) confirm.Close();
        else if (menu is TitleMenu title) title.RemoteBack();
        else if (menu == game.AdvMain && !game.VideoPlayer.IsPlaying) OpenPause();
        else game.Back();
    }

    // Shared by the Menu key, Esc and the on-screen touch menu button.
    public static void OpenPause()
    {
        var game = Wa2EngineMain.Engine;
        if (game == null || game.UiMgr.UiQueue.Count == 0 || game.UiMgr.UiQueue.Peek() != game.AdvMain || game.VideoPlayer.IsPlaying) return;
        if (_pause == null || !GodotObject.IsInstanceValid(_pause))
        {
            _pause = new Wa2RemotePauseMenu();
            game.UiMgr.AddChild(_pause);
        }
        _pause.Open();
    }
}
