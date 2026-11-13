using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Orion.Chat.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DirectionalEmoteTargetComponent : Component
{
    /// <summary>
    /// Whether the entity can send directional emotes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanSendEmotes = true;

    /// <summary>
    /// Whether the entity can receive directional emotes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanReceiveEmotes = true;

    /// <summary>
    /// Whether the entity can hide their name in directional emotes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanHideName;

    public TimeSpan LastSend = TimeSpan.MinValue;
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);
}

[Serializable, NetSerializable]
public sealed class SendDirectionalEmoteEvent : EntityEventArgs
{
    public readonly NetEntity Source;
    public readonly NetEntity Target;
    public readonly string Text;
    public readonly bool HideName;

    public SendDirectionalEmoteEvent(NetEntity source, NetEntity target, string text, bool hideName = false)
    {
        Source = source;
        Target = target;
        Text = text;
        HideName = hideName;
    }
}
