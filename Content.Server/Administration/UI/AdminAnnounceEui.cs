using System.IO;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.UI
{
    // Wormix: reworked announce panel - per-map targets, custom color, sound and sender signature.
    public sealed partial class AdminAnnounceEui : BaseEui
    {
        [Dependency] private IAdminManager _adminManager = default!;
        [Dependency] private IAdminLogManager _adminLogger = default!;
        [Dependency] private IChatManager _chatManager = default!;
        [Dependency] private IEntityManager _entities = default!;
        [Dependency] private IPrototypeManager _prototypes = default!;
        [Dependency] private IResourceManager _resources = default!;

        private readonly ChatSystem _chatSystem;
        private readonly ServerStationSystem _stationSystem;
        private readonly SharedAudioSystem _audio;

        public AdminAnnounceEui()
        {
            IoCManager.InjectDependencies(this);
            var systems = IoCManager.Resolve<IEntitySystemManager>();
            _chatSystem = systems.GetEntitySystem<ChatSystem>();
            _stationSystem = systems.GetEntitySystem<ServerStationSystem>();
            _audio = systems.GetEntitySystem<SharedAudioSystem>();
        }

        public override void Opened()
        {
            StateDirty();
        }

        public override EuiStateBase GetNewState()
        {
            return new AdminAnnounceEuiState(CollectTargets());
        }

        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            if (msg is not AdminAnnounceEuiMsg.DoAnnounce announce)
                return;

            if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
            {
                Close();
                return;
            }

            var color = ParseColor(announce.ColorHex);
            var sound = ParseSound(announce.SoundPath, announce.SoundVolume);
            var sender = string.IsNullOrWhiteSpace(announce.Announcer)
                ? Loc.GetString("chat-manager-sender-announcement")
                : announce.Announcer.Trim();
            var signature = string.IsNullOrWhiteSpace(announce.Sender) ? null : announce.Sender.Trim();
            var text = announce.Announcement;
            var targetName = announce.AnnounceType.ToString();

            switch (announce.AnnounceType)
            {
                case AdminAnnounceType.Server:
                    // Server-wide dispatch has no signature parameter, so it is added inline.
                    var signed = signature == null
                        ? text
                        : $"{text}\n{Loc.GetString("admin-announce-sent-by")} {signature}";
                    _chatManager.DispatchServerAnnouncement($"{sender}: {signed}", color);
                    if (sound != null)
                        _audio.PlayGlobal(sound, Filter.Broadcast(), true);
                    break;

                case AdminAnnounceType.All:
                    _chatSystem.DispatchGlobalAnnouncement(
                        text,
                        sender,
                        colorOverride: color,
                        announcementSound: sound,
                        signature: signature);
                    break;

                case AdminAnnounceType.Map:
                    if (!TryBuildMapFilter(announce.TargetGrid, out var filter, out targetName))
                        break;

                    // Wormix: ${station} becomes the NATO phonetic callsign of the target grid.
                    text = text.Replace("${station}", StationNameFormatter.Format(targetName));
                    _chatSystem.DispatchFilteredAnnouncement(
                        filter,
                        text,
                        sender: sender,
                        colorOverride: color,
                        announcementSound: sound,
                        signature: signature);
                    break;
            }

            _adminLogger.Add(LogType.Chat, LogImpact.Low,
                $"{Player:actor} sent admin announcement " +
                $"[type={announce.AnnounceType}] " +
                $"[target={targetName}] " +
                $"[color=#{announce.ColorHex}] " +
                $"[sound={(sound != null ? announce.SoundPath : "none")}] " +
                $"[volume={announce.SoundVolume}] " +
                $"[announcer=\"{announce.Announcer}\"] " +
                $"[sender=\"{announce.Sender}\"] " +
                $": {announce.Announcement}");

            StateDirty();

            if (announce.CloseAfter)
                Close();
        }

        private List<AnnounceTarget> CollectTargets()
        {
            var targets = new List<AnnounceTarget>();
            var seen = new HashSet<EntityUid>();

            foreach (var station in _stationSystem.GetStations())
            {
                if (_stationSystem.GetLargestGrid(station.AsNullable()) is { } grid)
                    AddTarget(targets, seen, grid);
            }

            // Centcomm grids are attached through the shuttle system, not StationSystem.
            var centcommQuery = _entities.EntityQueryEnumerator<StationCentcommComponent>();
            while (centcommQuery.MoveNext(out _, out var centcomm))
            {
                if (centcomm.Entity is { } grid)
                    AddTarget(targets, seen, grid);
            }

            targets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return targets;
        }

        private void AddTarget(List<AnnounceTarget> targets, HashSet<EntityUid> seen, EntityUid grid)
        {
            if (!seen.Add(grid) ||
                !_entities.TryGetComponent<TransformComponent>(grid, out var xform) ||
                xform.MapID == MapId.Nullspace ||
                !_entities.TryGetComponent<MetaDataComponent>(grid, out var metadata) ||
                string.IsNullOrWhiteSpace(metadata.EntityName))
            {
                return;
            }

            targets.Add(new AnnounceTarget(metadata.EntityName, _entities.GetNetEntity(grid)));
        }

        private bool TryBuildMapFilter(NetEntity? netGrid, out Filter filter, out string targetName)
        {
            filter = Filter.Empty();
            targetName = "invalid";

            if (netGrid is not { } grid ||
                !_entities.TryGetEntity(grid, out var uid) ||
                !_entities.TryGetComponent<TransformComponent>(uid, out var xform) ||
                xform.MapID == MapId.Nullspace)
            {
                return false;
            }

            filter = Filter.Empty().AddInMap(xform.MapID, _entities);
            targetName = _entities.GetComponent<MetaDataComponent>(uid.Value).EntityName;
            return true;
        }

        private static Color ParseColor(string? hex)
        {
            var value = hex?.Trim().TrimStart('#');

            if (string.IsNullOrWhiteSpace(value))
                value = "f0c040";

            try
            {
                return Color.FromHex("#" + value);
            }
            catch (FormatException)
            {
                return Color.FromHex("#f0c040");
            }
        }

        private SoundSpecifier? ParseSound(string? rawPath, float volume)
        {
            var path = rawPath?.Trim();

            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("/Audio/", StringComparison.OrdinalIgnoreCase) ||
                !SoundFileExists(path))
            {
                return null;
            }

            // Announcement audio is already loud, so keep it a bit quieter than requested.
            return new SoundPathSpecifier(path)
            {
                Params = AudioParams.Default.WithVolume(volume - 8f),
            };
        }

        private bool SoundFileExists(string path)
        {
            if (_prototypes.HasIndex<AudioMetadataPrototype>(path))
                return true;

            try
            {
                if (!_resources.TryContentFileRead(path, out var stream))
                    return false;

                stream.Dispose();
                return true;
            }
            catch (Exception e) when (e is ArgumentException or FileNotFoundException)
            {
                return false;
            }
        }
    }
}
