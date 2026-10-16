using Godot;
using System;
using System.Collections.Generic;
[GlobalClass]
public partial class Wa2Button : TextureButton
{
	[Export]
	public AudioStream HoverStream;
	[Export]
	public AudioStream ClickStream;
	private Wa2EngineMain _engine;
	// Apple port: touch targets are enlarged on iOS/iPadOS/visionOS without
	// changing artwork. Registered buttons let a margin yield to neighbours.
	private static readonly bool TouchTargets = OS.GetName() == "iOS";
	private static readonly HashSet<Wa2Button> Registered = new();
	public override void _EnterTree()
	{
		if (TouchTargets) Registered.Add(this);
	}
	public override void _ExitTree()
	{
		Registered.Remove(this);
	}
	public override void _Ready()
	{
		_engine=Wa2EngineMain.Engine;
		ButtonDown+=OnClick;

	}
	public override bool _HasPoint(Vector2 point)
	{
		var own = new Wa2FocusRect(0, 0, Size.X, Size.Y);
		bool inside = Wa2TouchInput.Contains(own, point.X, point.Y);
		if (inside || !TouchTargets) return inside;
		if (!Wa2TouchInput.Contains(Wa2TouchInput.Expanded(own), point.X, point.Y)) return false;
		Vector2 global = GetGlobalTransform() * point;
		return Wa2TouchInput.Hit(ToRect(GetGlobalRect()), global.X, global.Y, Neighbours());
	}
	private IEnumerable<Wa2FocusRect> Neighbours()
	{
		foreach (Wa2Button button in Registered)
		{
			if (button == this || !GodotObject.IsInstanceValid(button) || !button.IsVisibleInTree() ||
				button.MouseFilter == MouseFilterEnum.Ignore) continue;
			yield return ToRect(button.GetGlobalRect());
		}
	}
	private static Wa2FocusRect ToRect(Rect2 rect) => new(rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y);
	public void OnClick(){
		_engine?.SoundMgr.PlaySysSe(ClickStream);
	}
	private void OnHover(){
	}
}
