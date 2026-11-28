using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration
{
    // Wormix-start: announcement can be delivered everywhere, to one map, or server-wide.
    public enum AdminAnnounceType
    {
        All,
        Map,
        Server,
    }
    // Wormix-end

    [Serializable, NetSerializable]
    public sealed class AdminAnnounceEuiState : EuiStateBase
    {
        // Wormix-start: the panel receives the list of mappable grids it can target.
        public readonly List<AnnounceTarget> Targets;

        public AdminAnnounceEuiState(List<AnnounceTarget> targets)
        {
            Targets = targets;
        }
        // Wormix-end
    }

    // Wormix-start
    [Serializable, NetSerializable]
    public readonly record struct AnnounceTarget(string Name, NetEntity Grid);

    /// <summary>
    /// Where an announcement should be delivered.
    /// </summary>
    public readonly record struct AnnounceDestination(AdminAnnounceType Type, NetEntity? Grid)
    {
        public static readonly AnnounceDestination All = new(AdminAnnounceType.All, null);
        public static readonly AnnounceDestination Server = new(AdminAnnounceType.Server, null);
    }
    // Wormix-end

    public static class AdminAnnounceEuiMsg
    {
        [Serializable, NetSerializable]
        public sealed class DoAnnounce : EuiMessageBase
        {
            public bool CloseAfter;
            public string Announcer = default!;
            public string Announcement = default!;
            public AdminAnnounceType AnnounceType;
            // Wormix-start: optional map target, custom color, sound and sender signature.
            public NetEntity? TargetGrid;
            public string ColorHex = "f0c040";
            public string SoundPath = "";
            public float SoundVolume = 5f;
            public string Sender = "";
            // Wormix-end
        }
    }
}
