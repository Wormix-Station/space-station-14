namespace Content.Server._TP.Falling.FallMonitor;

/// <summary>
/// Machine that listens for <see cref="FallDetectedEvent"/> and radios out the last known position.
/// </summary>
[RegisterComponent]
public sealed partial class FallMonitorComponent : Component;
