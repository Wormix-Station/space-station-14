namespace Content.Shared._TP.Movement.Components;

/// <summary>
/// Tracks an entity that is mid-jump, so the fall system
/// (<see cref="Content.Server._TP.Falling.Systems.FallSystem"/>) can make it
/// fall once it lands in open air off a grid.
/// </summary>
[RegisterComponent]
public sealed partial class JumpingComponent : Component
{
    [DataField]
    public bool IsJumping = false;

    [DataField]
    public bool WasJumping = false;

    [DataField]
    public TimeSpan LastJumped = TimeSpan.Zero;
}
