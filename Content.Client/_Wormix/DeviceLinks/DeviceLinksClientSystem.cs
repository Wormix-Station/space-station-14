using Content.Client.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared._Wormix.DeviceLinks;
using Robust.Client.Graphics;

namespace Content.Client._Wormix.DeviceLinks;

/// <summary>
/// Client side of the device link overlay: holds the streamed link list and
/// owns the world overlay that draws it.
/// </summary>
public sealed partial class DeviceLinksClientSystem : EntitySystem
{
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private DeviceLinksOverlay? _overlay;
    private List<DeviceLinkInfo> _links = new();

    public bool Enabled { get; private set; }
    public IReadOnlyList<DeviceLinkInfo> Links => _links;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DeviceLinksStateEvent>(OnState);
        _admin.AdminStatusUpdated += OnAdminStatus;
    }

    public override void Shutdown()
    {
        _admin.AdminStatusUpdated -= OnAdminStatus;
        SetEnabled(false);
        base.Shutdown();
    }

    public bool CanUse()
    {
        return _admin.HasFlag(AdminFlags.Admin)
               || _admin.HasFlag(AdminFlags.Mapping)
               || _admin.HasFlag(AdminFlags.Debug);
    }

    public void Toggle()
    {
        SetEnabled(!Enabled);
    }

    public void SetEnabled(bool value)
    {
        if (Enabled == value)
            return;

        if (value && !CanUse())
            return;

        Enabled = value;

        if (value)
        {
            _overlay = new DeviceLinksOverlay(this, EntityManager);
            _overlays.AddOverlay(_overlay);
        }
        else
        {
            _links = new List<DeviceLinkInfo>();

            if (_overlay != null)
            {
                _overlays.RemoveOverlay(_overlay);
                _overlay = null;
            }
        }

        RaiseNetworkEvent(new DeviceLinksToggleEvent { Enabled = value });
    }

    private void OnState(DeviceLinksStateEvent ev)
    {
        if (!Enabled)
            return;

        _links = ev.Links;
    }

    private void OnAdminStatus()
    {
        // Lost the flags that allow the overlay while it is open - close it.
        if (Enabled && !CanUse())
            SetEnabled(false);
    }
}
