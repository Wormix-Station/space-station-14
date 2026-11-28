using Content.Shared.DeviceNetwork;
using Content.Shared.Medical.SuitSensors;
using Robust.Shared.Audio;

namespace Content.Server.Medical.CrewMonitoring;

/// <summary>
/// Stores suit sensor status received by a crew monitoring console.
/// </summary>
[RegisterComponent]
[Access(typeof(CrewMonitoringConsoleSystem))]
public sealed partial class CrewMonitoringConsoleComponent : Component
{
    /// <summary>
    /// List of all currently connected sensors to this console.
    /// </summary>
    public Dictionary<DeviceAddress, SuitSensorStatus> ConnectedSensors = new();

    /// <summary>
    /// How long without an update before a sensor is considered lost, in seconds.
    /// </summary>
    [DataField]
    public float SensorTimeout = 10f;

    // Orion-Edit-Start
    /// <summary>
    ///     Enable or disable alerts.
    /// </summary>
    [DataField]
    public bool DoAlert = true;

    /// <summary>
    ///     Time interval between alerts in seconds.
    /// </summary>
    [DataField]
    public float AlertTime = 30f;

    /// <summary>
    ///     Sound to play when alert is triggered.
    /// </summary>
    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Machines/crew_monitoring_alert.ogg");

    [DataField]
    public AudioParams AlertAudioParams = AudioParams.Default.WithVolume(-8f).WithVariation(0.25f);

    /// <summary>
    ///     Timestamp of the next allowed alert.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextAlertTime = TimeSpan.Zero;

    [DataField(serverOnly: true)]
    public Color? NormalLightColor { get; set; }

    [DataField(serverOnly: true)]
    public float? NormalLightEnergy { get; set; }

    [DataField(serverOnly: true)]
    public float? NormalLightRadius { get; set; }
    // Orion-Edit-End
}
