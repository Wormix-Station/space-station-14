using System.Linq;
using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Client.Eui;
using Content.Client.Stylesheets;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;
using static Content.Shared.Administration.PermissionsEuiMsg;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Administration.UI
{
    // Wormix: the admin and rank lists are searchable tables with aligned columns,
    // and the flag editor highlights the currently chosen inherit/deny/grant state.
    [UsedImplicitly]
    public sealed partial class PermissionsEui : BaseEui
    {
        private const int NoRank = -1;

        [Dependency] private IClientAdminManager _adminManager = default!;

        private readonly Menu _menu;
        private readonly List<BaseWindow> _subWindows = new();

        private PermissionsEuiState? _state;

        private Dictionary<int, PermissionsEuiState.AdminRankData> _ranks =
            new();

        public PermissionsEui()
        {
            IoCManager.InjectDependencies(this);

            _menu = new Menu(this);
            _menu.AddAdminButton.OnPressed += AddAdminPressed;
            _menu.AddAdminRankButton.OnPressed += AddAdminRankPressed;
            _menu.AdminSearchInput.OnTextChanged += _ => RefreshLists();
            _menu.RankSearchInput.OnTextChanged += _ => RefreshLists();
            _menu.OnClose += CloseEverything;
        }

        public override void Closed()
        {
            base.Closed();

            SendMessage(new CloseEuiMessage());
            CloseEverything();
        }

        private void CloseEverything()
        {
            foreach (var subWindow in _subWindows.ToArray())
            {
                subWindow.Close();
            }

            _menu.Close();
        }

        private void AddAdminPressed(BaseButton.ButtonEventArgs obj)
        {
            OpenEditWindow(null);
        }

        private void AddAdminRankPressed(BaseButton.ButtonEventArgs obj)
        {
            OpenRankEditWindow(null);
        }

        private void OnEditPressed(PermissionsEuiState.AdminData admin)
        {
            OpenEditWindow(admin);
        }

        private void OpenEditWindow(PermissionsEuiState.AdminData? data)
        {
            var window = new EditAdminWindow(this, data);
            window.SaveButton.OnPressed += _ => SaveAdminPressed(window);
            window.OpenCentered();
            window.OnClose += () => _subWindows.Remove(window);
            if (data != null)
            {
                window.RemoveButton!.OnPressed += _ => RemoveButtonPressed(window);
            }

            _subWindows.Add(window);
        }

        private void OpenRankEditWindow(KeyValuePair<int, PermissionsEuiState.AdminRankData>? rank)
        {
            var window = new EditAdminRankWindow(this, rank);
            window.SaveButton.OnPressed += _ => SaveAdminRankPressed(window);
            window.OpenCentered();
            window.OnClose += () => _subWindows.Remove(window);
            if (rank != null)
            {
                window.RemoveButton!.OnPressed += _ => RemoveRankButtonPressed(window);
            }

            _subWindows.Add(window);
        }

        private void RemoveButtonPressed(EditAdminWindow window)
        {
            SendMessage(new RemoveAdmin { UserId = window.SourceData!.Value.UserId });

            window.Close();
        }

        private void RemoveRankButtonPressed(EditAdminRankWindow window)
        {
            SendMessage(new RemoveAdminRank { Id = window.SourceId!.Value });

            window.Close();
        }

        private void SaveAdminPressed(EditAdminWindow popup)
        {
            popup.CollectSetFlags(out var pos, out var neg);

            int? rank = popup.RankButton.SelectedId;
            if (rank == NoRank)
            {
                rank = null;
            }

            var title = string.IsNullOrWhiteSpace(popup.TitleEdit.Text) ? null : popup.TitleEdit.Text;
            var suspended = popup.SuspendedCheckbox.Pressed;

            if (popup.SourceData is { } src)
            {
                SendMessage(new UpdateAdmin
                {
                    UserId = src.UserId,
                    Title = title,
                    PosFlags = pos,
                    NegFlags = neg,
                    RankId = rank,
                    Suspended = suspended,
                });
            }
            else
            {
                DebugTools.AssertNotNull(popup.NameEdit);

                SendMessage(new AddAdmin
                {
                    UserNameOrId = popup.NameEdit!.Text,
                    Title = title,
                    PosFlags = pos,
                    NegFlags = neg,
                    RankId = rank,
                    Suspended = suspended,
                });
            }

            popup.Close();
        }

        private void SaveAdminRankPressed(EditAdminRankWindow popup)
        {
            var flags = popup.CollectSetFlags();
            var name = popup.NameEdit.Text;

            if (popup.SourceId is { } src)
            {
                SendMessage(new UpdateAdminRank
                {
                    Id = src,
                    Flags = flags,
                    Name = name,
                });
            }
            else
            {
                SendMessage(new AddAdminRank
                {
                    Flags = flags,
                    Name = name
                });
            }

            popup.Close();
        }

        public override void Opened()
        {
            _menu.OpenCentered();
        }

        public override void HandleState(EuiStateBase state)
        {
            var s = (PermissionsEuiState) state;

            if (s.IsLoading)
            {
                return;
            }

            _ranks = s.AdminRanks;
            _state = s;

            RefreshLists();
        }

        // Wormix-start
        private void RefreshLists()
        {
            if (_state is not { } state)
                return;

            FillAdmins(state);
            FillRanks(state);
        }

        private void FillAdmins(PermissionsEuiState state)
        {
            var list = _menu.AdminsList;
            list.RemoveAllChildren();

            TableHeader(list,
                ("permissions-eui-header-action", RowActionWidth),
                ("permissions-eui-header-admin", RowNameWidth),
                ("permissions-eui-header-rank", RowRankWidth),
                ("permissions-eui-header-title", RowTitleWidth),
                ("permissions-eui-header-flags", RowFlagsWidth));

            var filter = _menu.AdminSearchInput.Text.Trim();
            var shown = 0;

            foreach (var admin in state.Admins.OrderBy(a => a.UserName ?? a.UserId.ToString()))
            {
                var name = admin.UserName ?? admin.UserId.ToString();
                var (rankLabel, rankInherited) = RankLabel(state, admin.RankId);
                var title = admin.Title ?? Loc.GetString("permissions-eui-edit-admin-title-control-text").ToLowerInvariant();

                var combined = admin.PosFlags;
                if (admin.RankId is { } rankId && state.AdminRanks.TryGetValue(rankId, out var rankData))
                    combined |= rankData.Flags;

                var flagsText = AdminFlagsHelper.PosNegFlagsText(admin.PosFlags, admin.NegFlags);

                if (!FilterMatches(filter, name, admin.UserId.ToString(), rankLabel, title, flagsText))
                    continue;

                var edit = RowButton(Loc.GetString("permissions-eui-edit-title-button"));
                edit.OnPressed += _ => OnEditPressed(admin);
                if (!_adminManager.HasFlag(combined))
                {
                    edit.Disabled = true;
                    edit.ToolTip = Loc.GetString("permissions-eui-do-not-have-required-flags-to-edit-admin-tooltip");
                }

                TableRow(list, shown++,
                    Cell(edit, RowActionWidth),
                    TextCell(name, RowNameWidth, style: StyleClass.LabelHeading),
                    TextCell(rankLabel, RowRankWidth, italic: rankInherited),
                    TextCell(title, RowTitleWidth, italic: admin.Title == null),
                    TextCell(flagsText, RowFlagsWidth, style: StyleClass.LabelSubText));
            }

            EmptyRow(list, shown);
        }

        private void FillRanks(PermissionsEuiState state)
        {
            var list = _menu.AdminRanksList;
            list.RemoveAllChildren();

            TableHeader(list,
                ("permissions-eui-header-action", RowActionWidth),
                ("permissions-eui-header-rank", RowRankWidth),
                ("permissions-eui-header-flags", RowFlagsWidth));

            var filter = _menu.RankSearchInput.Text.Trim();
            var shown = 0;

            foreach (var (id, data) in state.AdminRanks.OrderBy(kv => kv.Value.Name))
            {
                var flagsText = string.Join(' ', AdminFlagsHelper.FlagsToNames(data.Flags).Select(flag => $"+{flag}"));

                if (!FilterMatches(filter, data.Name, flagsText))
                    continue;

                var edit = RowButton(Loc.GetString("permissions-eui-edit-admin-rank-button"));
                edit.OnPressed += _ => OnEditRankPressed(new(id, data));
                if (!_adminManager.HasFlag(data.Flags))
                {
                    edit.Disabled = true;
                    edit.ToolTip = Loc.GetString("permissions-eui-do-not-have-required-flags-to-edit-rank-tooltip");
                }

                TableRow(list, shown++,
                    Cell(edit, RowActionWidth),
                    TextCell(data.Name, RowRankWidth, style: StyleClass.LabelHeading),
                    TextCell(flagsText, RowFlagsWidth, style: StyleClass.LabelSubText));
            }

            EmptyRow(list, shown);
        }

        private (string Label, bool Inherited) RankLabel(PermissionsEuiState state, int? rankId)
        {
            if (rankId is { } id)
            {
                if (state.AdminRanks.TryGetValue(id, out var data))
                    return (data.Name, false);

                // Rank was deleted while the window is open.
                return (Loc.GetString("permissions-eui-edit-no-rank-text").ToLowerInvariant(), true);
            }

            return (Loc.GetString("permissions-eui-edit-no-rank-text").ToLowerInvariant(), true);
        }

        private const int RowActionWidth = 140;
        private const int RowNameWidth = 220;
        private const int RowRankWidth = 150;
        private const int RowTitleWidth = 150;
        private const int RowFlagsWidth = 240;
        private const int RowPadding = 8;

        private static void TableHeader(BoxContainer list, params (string Key, int Width)[] columns)
        {
            var row = HeaderRow(columns);

            list.AddChild(new PanelContainer
            {
                HorizontalExpand = true,
                StyleClasses = { StyleClass.BackgroundPanelDark },
                Children = { row },
            });
        }

        private static void TableRow(BoxContainer list, int index, params Control[] cells)
        {
            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SeparationOverride = RowPadding,
            };

            foreach (var cell in cells)
            {
                row.AddChild(cell);
            }

            list.AddChild(new PanelContainer
            {
                HorizontalExpand = true,
                StyleClasses = { index % 2 == 0 ? StyleClass.Panel : StyleClass.PanelDark },
                Children = { row },
            });
        }

        private static BoxContainer HeaderRow((string Key, int Width)[] columns)
        {
            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SeparationOverride = RowPadding,
            };

            foreach (var (key, width) in columns)
            {
                var label = new Label
                {
                    Text = Loc.GetString(key),
                    ClipText = true,
                    ToolTip = Loc.GetString(key),
                    VerticalAlignment = Control.VAlignment.Center,
                    StyleClasses = { StyleClass.LabelSubHeading },
                };
                label.SetWidth = width;
                row.AddChild(label);
            }

            return row;
        }

        private static void EmptyRow(BoxContainer list, int shown)
        {
            if (shown > 0)
                return;

            list.AddChild(new PanelContainer
            {
                HorizontalExpand = true,
                StyleClasses = { StyleClass.Panel },
                Children =
                {
                    new Label
                    {
                        Text = Loc.GetString("permissions-eui-search-empty"),
                        HorizontalExpand = true,
                        Align = Label.AlignMode.Center,
                        StyleClasses = { StyleClass.LabelSubText },
                    },
                },
            });
        }

        private static Control Cell(Control control, int width)
        {
            control.SetWidth = width;
            return control;
        }

        private static Label TextCell(string text, int width, bool italic = false, string? style = null)
        {
            var label = new Label
            {
                Text = text,
                ClipText = true,
                ToolTip = text,
                VerticalAlignment = Control.VAlignment.Center,
            };
            label.SetWidth = width;

            if (style != null)
                label.StyleClasses.Add(style);

            if (italic)
                label.StyleClasses.Add(StyleClass.Italic);

            return label;
        }

        private static Button RowButton(string text)
        {
            return new Button
            {
                Text = text,
                TextAlign = Label.AlignMode.Center,
                VerticalAlignment = Control.VAlignment.Center,
            };
        }

        private static bool FilterMatches(string filter, params string?[] values)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return values.Any(value =>
                !string.IsNullOrWhiteSpace(value) &&
                value.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
        // Wormix-end

        private void OnEditRankPressed(KeyValuePair<int, PermissionsEuiState.AdminRankData> rank)
        {
            OpenRankEditWindow(rank);
        }

        private sealed partial class Menu : DefaultWindow
        {
            private readonly PermissionsEui _ui;

            public readonly BoxContainer AdminsList;
            public readonly BoxContainer AdminRanksList;
            public readonly LineEdit AdminSearchInput;
            public readonly LineEdit RankSearchInput;
            public readonly Button AddAdminButton;
            public readonly Button AddAdminRankButton;

            public Menu(PermissionsEui ui)
            {
                _ui = ui;
                MinSize = new Vector2(760, 460); // Wormix
                SetSize = new Vector2(900, 560); // Wormix
                Title = Loc.GetString("permissions-eui-menu-title");

                AdminSearchInput = SearchField(Loc.GetString("permissions-eui-admins-search"));
                AdminsList = ListColumn();
                AddAdminButton = ActionButton(Loc.GetString("permissions-eui-menu-add-admin-button"));

                var adminsTab = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    SeparationOverride = 8,
                    Children =
                    {
                        AdminSearchInput,
                        ListScroll(AdminsList),
                        AddAdminButton,
                    },
                };
                TabContainer.SetTabTitle(adminsTab, Loc.GetString("permissions-eui-menu-admins-tab-title"));

                RankSearchInput = SearchField(Loc.GetString("permissions-eui-ranks-search"));
                AdminRanksList = ListColumn();
                AddAdminRankButton = ActionButton(Loc.GetString("permissions-eui-menu-add-admin-rank-button"));

                var ranksTab = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    SeparationOverride = 8,
                    Children =
                    {
                        RankSearchInput,
                        ListScroll(AdminRanksList),
                        AddAdminRankButton,
                    },
                };
                TabContainer.SetTabTitle(ranksTab, Loc.GetString("permissions-eui-menu-admin-ranks-tab-title"));

                var tabs = new TabContainer
                {
                    HorizontalExpand = true,
                    VerticalExpand = true,
                };
                tabs.AddChild(adminsTab);
                tabs.AddChild(ranksTab);

                ContentsContainer.AddChild(tabs);
            }

            private static LineEdit SearchField(string placeholder)
            {
                return new LineEdit
                {
                    PlaceHolder = placeholder,
                    SelectAllOnFocus = true,
                    HorizontalExpand = true,
                };
            }

            private static BoxContainer ListColumn()
            {
                return new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    SeparationOverride = 4,
                };
            }

            private static ScrollContainer ListScroll(BoxContainer list)
            {
                return new ScrollContainer
                {
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    Children = { list },
                };
            }

            private static Button ActionButton(string text)
            {
                return new Button
                {
                    Text = text,
                    HorizontalAlignment = HAlignment.Right,
                    StyleClasses = { StyleClass.Positive },
                };
            }
        }

        private sealed partial class EditAdminWindow : DefaultWindow
        {
            public readonly PermissionsEuiState.AdminData? SourceData;
            public readonly LineEdit? NameEdit;
            public readonly LineEdit TitleEdit;
            public readonly OptionButton RankButton;
            public readonly Button SaveButton;
            public readonly Button? RemoveButton;
            public readonly CheckBox SuspendedCheckbox;

            public readonly Dictionary<AdminFlags, (Button inherit, Button sub, Button plus)> FlagButtons
                = new();

            public EditAdminWindow(PermissionsEui ui, PermissionsEuiState.AdminData? data)
            {
                MinSize = new Vector2(700, 460); // Wormix
                SetSize = new Vector2(760, 520); // Wormix
                SourceData = data;

                Control nameControl;

                if (data is { } dat)
                {
                    var name = dat.UserName ?? dat.UserId.ToString();
                    Title = Loc.GetString("permissions-eui-edit-admin-window-edit-admin-label",
                                          ("admin", name));

                    nameControl = new Label
                    {
                        Text = name,
                        ClipText = true,
                        ToolTip = name,
                        StyleClasses = { StyleClass.LabelHeading },
                    };
                }
                else
                {
                    Title = Loc.GetString("permissions-eui-menu-add-admin-button");

                    nameControl = NameEdit = new LineEdit
                    {
                        PlaceHolder = Loc.GetString("permissions-eui-edit-admin-window-name-edit-placeholder"),
                        SelectAllOnFocus = true,
                    };
                }

                TitleEdit = new LineEdit
                {
                    PlaceHolder = Loc.GetString("permissions-eui-edit-admin-window-title-edit-placeholder"),
                    SelectAllOnFocus = true,
                };
                TitleEdit.Text = data?.Title ?? string.Empty; // Wormix

                RankButton = new OptionButton
                {
                    HorizontalExpand = true,
                };
                SaveButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-edit-admin-window-save-button"),
                    HorizontalAlignment = HAlignment.Right,
                    StyleClasses = { StyleClass.Positive },
                };

                SuspendedCheckbox = new CheckBox
                {
                    Text = Loc.GetString("permissions-eui-edit-admin-window-suspended"),
                    Pressed = data?.Suspended ?? false,
                };

                RankButton.AddItem(Loc.GetString("permissions-eui-edit-admin-window-no-rank-button"), NoRank);
                foreach (var (rId, rank) in ui._ranks)
                {
                    RankButton.AddItem(rank.Name, rId);
                }

                RankButton.SelectId(data?.RankId ?? NoRank);
                RankButton.OnItemSelected += RankSelected;

                var permGrid = new GridContainer
                {
                    Columns = 5, // Wormix
                    HSeparationOverride = 0,
                    VSeparationOverride = 0,
                };

                permGrid.AddChild(FlagColumnHeader("permissions-eui-flag-column-inherit"));
                permGrid.AddChild(FlagColumnHeader("permissions-eui-flag-column-deny"));
                permGrid.AddChild(FlagColumnHeader("permissions-eui-flag-column-grant"));
                permGrid.AddChild(new Control());
                permGrid.AddChild(FlagColumnHeader("permissions-eui-flag-column-right"));

                foreach (var flag in AdminFlagsHelper.AllFlags)
                {
                    // Can only grant out perms you also have yourself.
                    // Primarily intended to prevent people giving themselves +HOST with +PERMISSIONS but generalized.
                    var disable = !ui._adminManager.HasFlag(flag);
                    var flagName = flag.ToString().ToUpper();

                    var group = new ButtonGroup();

                    var inherit = new Button
                    {
                        Text = "I",
                        TextAlign = Label.AlignMode.Center,
                        StyleClasses = { StyleClass.ButtonOpenRight },
                        Disabled = disable,
                        Group = group,
                    };
                    var sub = new Button
                    {
                        Text = "-",
                        TextAlign = Label.AlignMode.Center,
                        StyleClasses = { StyleClass.ButtonOpenBoth },
                        Disabled = disable,
                        Group = group
                    };
                    var plus = new Button
                    {
                        Text = "+",
                        TextAlign = Label.AlignMode.Center,
                        StyleClasses = { StyleClass.ButtonOpenLeft },
                        Disabled = disable,
                        Group = group
                    };

                    // Wormix: the chosen button of each trio gets highlighted.
                    inherit.OnPressed += _ => HighlightChoice(inherit, sub, plus);
                    sub.OnPressed += _ => HighlightChoice(inherit, sub, plus);
                    plus.OnPressed += _ => HighlightChoice(inherit, sub, plus);

                    if (data is { } d)
                    {
                        if ((d.NegFlags & flag) != 0)
                        {
                            sub.Pressed = true;
                        }
                        else if ((d.PosFlags & flag) != 0)
                        {
                            plus.Pressed = true;
                        }
                        else
                        {
                            inherit.Pressed = true;
                        }
                    }
                    else
                    {
                        inherit.Pressed = true;
                    }

                    HighlightChoice(inherit, sub, plus);
                    permGrid.AddChild(inherit);
                    permGrid.AddChild(sub);
                    permGrid.AddChild(plus);
                    permGrid.AddChild(new Control());
                    permGrid.AddChild(new Label
                    {
                        Text = flagName,
                        ClipText = true,
                        ToolTip = flagName,
                        VerticalAlignment = Control.VAlignment.Center,
                        HorizontalExpand = true,
                    });

                    FlagButtons.Add(flag, (inherit, sub, plus));
                }

                var bottomButtons = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    HorizontalExpand = true,
                    SeparationOverride = 8,
                };
                if (data != null)
                {
                    RemoveButton = new Button
                    {
                        Text = Loc.GetString("permissions-eui-edit-admin-window-remove-flag-button"),
                        StyleClasses = { StyleClass.Negative },
                    };
                    bottomButtons.AddChild(RemoveButton);
                }

                bottomButtons.AddChild(new Control { HorizontalExpand = true });
                bottomButtons.AddChild(SaveButton);

                ContentsContainer.AddChild(new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    SeparationOverride = 8,
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            SeparationOverride = 8,
                            HorizontalExpand = true,
                            VerticalExpand = true,
                            Children =
                            {
                                new PanelContainer
                                {
                                    SetWidth = 260,
                                    StyleClasses = { StyleClass.BackgroundPanelDark },
                                    Children =
                                    {
                                        new BoxContainer
                                        {
                                            Orientation = LayoutOrientation.Vertical,
                                            SeparationOverride = 6,
                                            Children =
                                            {
                                                nameControl,
                                                TitleEdit,
                                                RankButton,
                                                SuspendedCheckbox,
                                            }
                                        }
                                    }
                                },
                                new PanelContainer
                                {
                                    HorizontalExpand = true,
                                    VerticalExpand = true,
                                    StyleClasses = { StyleClass.BackgroundPanelDark },
                                    Children =
                                    {
                                        new ScrollContainer
                                        {
                                            HorizontalExpand = true,
                                            VerticalExpand = true,
                                            Children = { permGrid },
                                        }
                                    }
                                }
                            }
                        },
                        bottomButtons
                    }
                });
            }

            // Wormix-start
            private static Label FlagColumnHeader(string key)
            {
                return new Label
                {
                    Text = Loc.GetString(key),
                    ToolTip = Loc.GetString(key),
                    Align = Label.AlignMode.Center,
                    StyleClasses = { StyleClass.LabelSubHeading },
                };
            }

            private static void HighlightChoice(Button inherit, Button deny, Button grant)
            {
                inherit.RemoveStyleClass(StyleClass.Positive);
                deny.RemoveStyleClass(StyleClass.Positive);
                grant.RemoveStyleClass(StyleClass.Positive);

                if (inherit.Pressed)
                    inherit.AddStyleClass(StyleClass.Positive);
                else if (deny.Pressed)
                    deny.AddStyleClass(StyleClass.Positive);
                else if (grant.Pressed)
                    grant.AddStyleClass(StyleClass.Positive);
            }
            // Wormix-end

            private void RankSelected(OptionButton.ItemSelectedEventArgs obj)
            {
                RankButton.SelectId(obj.Id);
            }

            public void CollectSetFlags(out AdminFlags pos, out AdminFlags neg)
            {
                pos = default;
                neg = default;

                foreach (var (flag, (_, s, p)) in FlagButtons)
                {
                    if (s.Pressed)
                    {
                        neg |= flag;
                    }
                    else if (p.Pressed)
                    {
                        pos |= flag;
                    }
                }
            }
        }

        private sealed partial class EditAdminRankWindow : DefaultWindow
        {
            public readonly int? SourceId;
            public readonly LineEdit NameEdit;
            public readonly Button SaveButton;
            public readonly Button? RemoveButton;
            public readonly Button ClearAllButton; // Wormix
            public readonly Button GrantAllButton; // Wormix
            public readonly Dictionary<AdminFlags, CheckBox> FlagCheckBoxes = new();

            public EditAdminRankWindow(PermissionsEui ui, KeyValuePair<int, PermissionsEuiState.AdminRankData>? data)
            {
                MinSize = new Vector2(500, 460); // Wormix
                SetSize = new Vector2(560, 520); // Wormix
                Title = Loc.GetString("permissions-eui-edit-admin-rank-window-title");
                SourceId = data?.Key;

                NameEdit = new LineEdit
                {
                    PlaceHolder = Loc.GetString("permissions-eui-edit-admin-rank-window-name-edit-placeholder"),
                    SelectAllOnFocus = true,
                };

                if (data != null)
                {
                    NameEdit.Text = data.Value.Value.Name;
                }

                SaveButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-menu-save-admin-rank-button"),
                    HorizontalAlignment = HAlignment.Right,
                    StyleClasses = { StyleClass.Positive },
                };
                ClearAllButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-rank-flags-clear"),
                };
                ClearAllButton.OnPressed += _ => SetAllFlags(false);
                GrantAllButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-rank-flags-grant"),
                    StyleClasses = { StyleClass.Positive },
                };
                GrantAllButton.OnPressed += _ => SetAllFlags(true);

                var flagsBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    SeparationOverride = 4,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                };

                foreach (var flag in AdminFlagsHelper.AllFlags)
                {
                    // Can only grant out perms you also have yourself.
                    // Primarily intended to prevent people giving themselves +HOST with +PERMISSIONS but generalized.
                    var disable = !ui._adminManager.HasFlag(flag);
                    var flagName = flag.ToString().ToUpper();

                    var checkBox = new CheckBox
                    {
                        Disabled = disable,
                        Text = flagName
                    };

                    if (data != null && (data.Value.Value.Flags & flag) != 0)
                    {
                        checkBox.Pressed = true;
                    }

                    FlagCheckBoxes.Add(flag, checkBox);
                    flagsBox.AddChild(checkBox);
                }

                var bottomButtons = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    HorizontalExpand = true,
                    SeparationOverride = 8,
                };
                if (data != null)
                {
                    RemoveButton = new Button
                    {
                        Text = Loc.GetString("permissions-eui-menu-remove-admin-rank-button"),
                        StyleClasses = { StyleClass.Negative },
                    };
                    bottomButtons.AddChild(RemoveButton);
                }

                bottomButtons.AddChild(new Control { HorizontalExpand = true });
                bottomButtons.AddChild(ClearAllButton);
                bottomButtons.AddChild(GrantAllButton);
                bottomButtons.AddChild(SaveButton);

                ContentsContainer.AddChild(new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    SeparationOverride = 8,
                    Children =
                    {
                        NameEdit,
                        new PanelContainer
                        {
                            HorizontalExpand = true,
                            VerticalExpand = true,
                            StyleClasses = { StyleClass.BackgroundPanelDark },
                            Children =
                            {
                                new ScrollContainer
                                {
                                    HorizontalExpand = true,
                                    VerticalExpand = true,
                                    Children = { flagsBox },
                                }
                            }
                        },
                        bottomButtons
                    }
                });
            }

            public AdminFlags CollectSetFlags()
            {
                AdminFlags flags = default;
                foreach (var (flag, chk) in FlagCheckBoxes)
                {
                    if (chk.Pressed)
                    {
                        flags |= flag;
                    }
                }

                return flags;
            }

            // Wormix-start
            private void SetAllFlags(bool pressed)
            {
                foreach (var checkBox in FlagCheckBoxes.Values)
                {
                    if (!checkBox.Disabled)
                        checkBox.Pressed = pressed;
                }
            }
            // Wormix-end
        }
    }
}
