using CRM.winforms.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    [DesignerCategory("Code")]
    public class SubscriptionsControl : UserControl
    {
        private readonly ApiClient _api = new ApiClient();
        private List<SubscriptionDto> _all = new();
        private List<SubscriptionDto> _filtered = new();
        private string _activeFilter = "All";

        // ── Controls ──
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private SaasButton btnAdd = null!;
        private SaasButton btnRefresh = null!;

        // ── Stat Tiles ──
        private StatTile tileTotal = null!;
        private StatTile tileActive = null!;
        private StatTile tileMultiBranch = null!;
        private StatTile tileSubscribers = null!;

        // ── Workbench Card ──
        private SurfaceCard card = null!;
        private WorkbenchSearch searchBox = null!;
        private SegmentedFilter filterBar = null!;
        private Label lblCount = null!;
        private DataGridView dgv = null!;
        private SaasEmptyState emptyState = null!;

        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;

        public SubscriptionsControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            BuildActionsMenu();
            BuildUi();

            this.Load += async (s, e) => await ReloadAsync();
        }

        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip { ShowImageMargin = false };
            _actionsMenu.Renderer = new QuietMenuRenderer();

            var miEdit = new ToolStripMenuItem("Edit Plan") { Height = 32 };
            miEdit.Click += (s, e) => EditCurrentPlan();

            var miToggle = new ToolStripMenuItem("Toggle Active / Inactive") { Height = 32 };
            miToggle.Click += async (s, e) => await TogglePlanStatusAsync();

            var miArchive = new ToolStripMenuItem("Archive Plan") { Height = 32 };
            miArchive.Click += async (s, e) => await ArchivePlanAsync();

            var miRestore = new ToolStripMenuItem("Restore Plan") { Height = 32 };
            miRestore.Click += async (s, e) => await RestorePlanAsync();

            _actionsMenu.Items.AddRange(new ToolStripItem[] { miEdit, miToggle, new ToolStripSeparator(), miArchive, miRestore });

            _actionsMenu.Opening += (s, e) =>
            {
                var item = CurrentItem;
                if (item != null)
                {
                    miArchive.Visible = !item.IsArchived;
                    miRestore.Visible = item.IsArchived;
                    miToggle.Enabled = !item.IsArchived;
                }
            };
        }

        private void BuildUi()
        {
            // Header
            lblTitle = new Label
            {
                Text = "Subscription Plans",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Manage subscription plans, pricing, and quotas.",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnAdd = new SaasButton("Add Plan", SaasButtonVariant.Primary, "\uE710");
            btnAdd.Size = new Size(130, UiKit.T.ButtonHeight);
            btnAdd.Click += async (s, e) => await AddPlanAsync();

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Size = new Size(100, UiKit.T.ButtonHeight);
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnAdd);
            Controls.Add(btnRefresh);

            // KPI Tiles
            tileTotal = new StatTile("TOTAL PLANS", "0", "\uE9D5");
            tileActive = new StatTile("ACTIVE TIERS", "0", "\uE73E");
            tileMultiBranch = new StatTile("MULTI-BRANCH", "0", "\uE716");
            tileSubscribers = new StatTile("SUBSCRIBED TENANTS", "0", "\uE716");

            Controls.Add(tileTotal);
            Controls.Add(tileActive);
            Controls.Add(tileMultiBranch);
            Controls.Add(tileSubscribers);

            // Workbench Card
            card = new SurfaceCard();

            searchBox = new WorkbenchSearch
            {
                Placeholder = "Search by plan name, features, or billing cycle..."
            };
            searchBox.QueryChanged += (s, e) => ApplyFilter();

            filterBar = new SegmentedFilter(new (string, int?)[]
            {
                ("All", null),
                ("Active", null),
                ("Inactive", null),
                ("Archived", null)
            });
            filterBar.SelectionChanged += (s, key) =>
            {
                _activeFilter = key;
                ApplyFilter();
            };

            lblCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = UiKit.T.InkFaint,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            dgv = new DataGridView();
            StyleGrid(dgv);
            dgv.CellClick += Dgv_CellClick;
            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _filtered.Count)
                    EditCurrentPlan();
            };

            emptyState = new SaasEmptyState
            {
                Icon = "\uE9D5",
                Text = "No subscription plans found",
                Subtitle = "Create your first SaaS subscription tier with pricing in PHP and user limits."
            };
            emptyState.SetAction("Add Subscription Plan", async (s, e) => await AddPlanAsync());

            card.Controls.Add(searchBox);
            card.Controls.Add(filterBar);
            card.Controls.Add(lblCount);
            card.Controls.Add(dgv);
            card.Controls.Add(emptyState);

            Controls.Add(card);

            Resize += (s, e) => LayoutUi();
            LayoutUi();
        }

        private void LayoutUi()
        {
            int pad = UiKit.T.S6;
            int right = Width - pad;

            lblTitle.Location = new Point(pad, pad);
            lblSubtitle.Location = new Point(pad, lblTitle.Bottom + 4);

            btnRefresh.Location = new Point(right - btnRefresh.Width, pad);
            btnAdd.Location = new Point(btnRefresh.Left - btnAdd.Width - UiKit.T.S3, pad);

            // Stats
            int tileY = lblSubtitle.Bottom + UiKit.T.S5;
            int gap = UiKit.T.S4;
            int tileW = Math.Max(180, (Width - pad * 2 - gap * 3) / 4);
            int tileH = 88;

            tileTotal.SetBounds(pad, tileY, tileW, tileH);
            tileActive.SetBounds(pad + (tileW + gap), tileY, tileW, tileH);
            tileMultiBranch.SetBounds(pad + (tileW + gap) * 2, tileY, tileW, tileH);
            tileSubscribers.SetBounds(pad + (tileW + gap) * 3, tileY, tileW, tileH);

            // Card
            int cardY = tileY + tileH + UiKit.T.S5;
            int cardH = Height - cardY - pad;
            card.SetBounds(pad, cardY, Width - pad * 2, Math.Max(260, cardH));

            // Card content
            int innerW = card.Width - pad * 2;
            searchBox.SetBounds(pad, pad, Math.Min(420, innerW - 280), 38);
            filterBar.SetBounds(card.Width - pad - filterBar.PreferredWidth, pad + 1, filterBar.PreferredWidth, 36);

            int gridY = searchBox.Bottom + UiKit.T.S4;
            int gridH = card.Height - gridY - pad - 26;

            dgv.SetBounds(pad, gridY, innerW, Math.Max(120, gridH));
            emptyState.SetBounds(pad, gridY, innerW, Math.Max(120, gridH));
            lblCount.Location = new Point(pad, dgv.Bottom + 6);
        }

        private void StyleGrid(DataGridView g)
        {
            g.AutoGenerateColumns = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.BackgroundColor = UiKit.T.Surface;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.GridColor = UiKit.T.LineSoft;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ScrollBars = ScrollBars.Both;
            g.RowTemplate.Height = 52;
            g.ColumnHeadersHeight = 42;

            g.ColumnHeadersDefaultCellStyle.BackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 0, 12, 0);

            g.DefaultCellStyle.BackColor = UiKit.T.Surface;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(12, 6, 12, 6);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            g.Columns.Clear();

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "PLAN NAME",
                DataPropertyName = "SubscriptionName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 180,
                MinimumWidth = 220
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPrice",
                HeaderText = "PRICE (PHP)",
                DataPropertyName = "PriceDisplay",
                Width = 160,
                MinimumWidth = 140
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDuration",
                HeaderText = "DURATION",
                DataPropertyName = "DurationDisplay",
                Width = 140,
                MinimumWidth = 120
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colUsers",
                HeaderText = "QUOTAS (USERS / DEVICES)",
                Width = 240,
                MinimumWidth = 200
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colMultiBranch",
                HeaderText = "MULTI-BRANCH",
                Width = 140,
                MinimumWidth = 125
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colSubscribers",
                HeaderText = "SUBSCRIBED",
                Width = 120,
                MinimumWidth = 105
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                HeaderText = "STATUS",
                Width = 120,
                MinimumWidth = 105
            });

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "colActions",
                HeaderText = "",
                Text = "\u22EF",
                UseColumnTextForButtonValue = true,
                Width = 48,
                FlatStyle = FlatStyle.Flat
            };
            btnCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            btnCol.DefaultCellStyle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            btnCol.DefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.Columns.Add(btnCol);

            g.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;
                var item = _filtered[e.RowIndex];

                // Quotas column
                if (e.ColumnIndex == g.Columns["colUsers"]!.Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    string quotaText = $"Max {item.MaxUsers} Users  ·  {item.MaxDevices} Units";
                    UiKit.Text(e.Graphics, quotaText, UiKit.T.Body, UiKit.T.Ink, e.CellBounds, UiKit.LeftWrap);
                    e.Handled = true;
                }
                // Multi-branch badge
                else if (e.ColumnIndex == g.Columns["colMultiBranch"]!.Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    var gState = e.Graphics;
                    UiKit.Quality(gState);

                    Color bg = item.EnableMultiBranching ? UiKit.Wash(AppTheme.Primary) : UiKit.LineSoft;
                    Color fg = item.EnableMultiBranching ? AppTheme.Primary : UiKit.T.InkMuted;
                    string txt = item.EnableMultiBranching ? "ENABLED" : "SINGLE";

                    var pill = new Rectangle(e.CellBounds.Left + 8, e.CellBounds.Top + (e.CellBounds.Height - 22) / 2, 74, 22);
                    UiKit.FillRounded(gState, pill, 11, bg);
                    UiKit.Text(gState, txt, UiKit.MicroStrong, fg, pill, UiKit.Center);
                    e.Handled = true;
                }
                // Subscribers count
                else if (e.ColumnIndex == g.Columns["colSubscribers"]!.Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    string subsText = $"{item.SubscribedCompaniesCount} tenant(s)";
                    UiKit.Text(e.Graphics, subsText, UiKit.T.Small, UiKit.T.InkMuted, e.CellBounds, UiKit.Left);
                    e.Handled = true;
                }
                // Status Pill Cell
                else if (e.ColumnIndex == g.Columns["colStatus"]!.Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    var stateG = e.Graphics;
                    UiKit.Quality(stateG);

                    Color bg = item.IsArchived
                        ? UiKit.LineSoft
                        : (item.IsActive ? UiKit.Wash(AppTheme.Success) : UiKit.Wash(AppTheme.Danger));
                    Color fg = item.IsArchived
                        ? UiKit.T.InkMuted
                        : (item.IsActive ? AppTheme.Success : AppTheme.Danger);
                    string txt = item.IsArchived ? "ARCHIVED" : (item.IsActive ? "ACTIVE" : "INACTIVE");

                    var pill = new Rectangle(e.CellBounds.Left + 8, e.CellBounds.Top + (e.CellBounds.Height - 22) / 2, 88, 22);
                    UiKit.FillRounded(stateG, pill, 11, bg);
                    UiKit.Text(stateG, txt, UiKit.MicroStrong, fg, pill, UiKit.Center);
                    e.Handled = true;
                }
            };
        }

        private async Task ReloadAsync()
        {
            try
            {
                _all = await _api.GetSubscriptionsAsync(includeArchived: true);

                int total = _all.Count;
                int active = _all.Count(p => p.IsActive && !p.IsArchived);
                int inactive = _all.Count(p => !p.IsActive && !p.IsArchived);
                int archived = _all.Count(p => p.IsArchived);
                int multiBranch = _all.Count(p => p.EnableMultiBranching && !p.IsArchived);
                int totalSubs = _all.Sum(p => p.SubscribedCompaniesCount);

                tileTotal.Value = total.ToString();
                tileActive.Value = active.ToString();
                tileMultiBranch.Value = multiBranch.ToString();
                tileSubscribers.Value = totalSubs.ToString();

                filterBar.UpdateCounts(new Dictionary<string, int?>
                {
                    ["All"] = total,
                    ["Active"] = active,
                    ["Inactive"] = inactive,
                    ["Archived"] = archived
                });

                ApplyFilter();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Failed to load subscription plans: {ex.Message}", ToastKind.Danger);
            }
        }

        private void ApplyFilter()
        {
            string query = searchBox.Query.Trim().ToLowerInvariant();

            _filtered = _all.Where(p =>
            {
                if (_activeFilter == "Active" && (!p.IsActive || p.IsArchived)) return false;
                if (_activeFilter == "Inactive" && (p.IsActive || p.IsArchived)) return false;
                if (_activeFilter == "Archived" && !p.IsArchived) return false;

                if (!string.IsNullOrWhiteSpace(query))
                {
                    bool matchName = p.SubscriptionName.ToLowerInvariant().Contains(query);
                    bool matchDesc = p.Description?.ToLowerInvariant().Contains(query) ?? false;
                    bool matchBilling = p.BillingCycle?.ToLowerInvariant().Contains(query) ?? false;
                    bool matchDuration = p.DurationDisplay.ToLowerInvariant().Contains(query);
                    if (!matchName && !matchDesc && !matchBilling && !matchDuration) return false;
                }

                return true;
            }).ToList();

            dgv.DataSource = null;
            dgv.DataSource = _filtered;

            bool hasData = _filtered.Count > 0;
            dgv.Visible = hasData;
            emptyState.Visible = !hasData;

            lblCount.Text = $"Showing {_filtered.Count} of {_all.Count} subscription plans";
        }

        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;

            if (e.ColumnIndex == dgv.Columns["colActions"]!.Index)
            {
                _menuRowIndex = e.RowIndex;
                var cellRect = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                _actionsMenu.Show(dgv, new Point(cellRect.Left, cellRect.Bottom));
            }
        }

        private SubscriptionDto? CurrentItem => _menuRowIndex >= 0 && _menuRowIndex < _filtered.Count
            ? _filtered[_menuRowIndex]
            : (dgv.CurrentRow != null && dgv.CurrentRow.Index < _filtered.Count ? _filtered[dgv.CurrentRow.Index] : null);

        private async Task AddPlanAsync()
        {
            using var dlg = new SubscriptionFormDialog(null);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK && dlg.ResultPlan != null)
            {
                SaasToast.Show(FindForm(),
                    $"Created plan '{dlg.ResultPlan.SubscriptionName}'!",
                    ToastKind.Success);
                await ReloadAsync();
            }
        }

        private void EditCurrentPlan()
        {
            var item = CurrentItem;
            if (item == null) return;

            using var dlg = new SubscriptionFormDialog(item);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
            {
                SaasToast.Show(FindForm(), $"Updated plan '{item.SubscriptionName}'.", ToastKind.Success);
                _ = ReloadAsync();
            }
        }

        private async Task TogglePlanStatusAsync()
        {
            var item = CurrentItem;
            if (item == null) return;

            try
            {
                bool newState = await _api.ToggleSubscriptionStatusAsync(item.SubscriptionId);
                item.IsActive = newState;
                SaasToast.Show(FindForm(),
                    $"Plan '{item.SubscriptionName}' is now {(newState ? "active" : "inactive")}.",
                    newState ? ToastKind.Success : ToastKind.Warning);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Failed to update status: {ex.Message}", ToastKind.Danger);
            }
        }

        private async Task ArchivePlanAsync()
        {
            var item = CurrentItem;
            if (item == null) return;

            bool confirm = SaasConfirm.Ask(
                FindForm(),
                "Archive Subscription Plan",
                $"Archive '{item.SubscriptionName}'?",
                confirmText: "Archive Plan",
                danger: true,
                detail: "Existing subscribed businesses will maintain their current plan, but new registrations will no longer see this tier.");

            if (!confirm) return;

            try
            {
                await _api.ArchiveSubscriptionAsync(item.SubscriptionId);
                SaasToast.Show(FindForm(), $"Plan '{item.SubscriptionName}' archived.", ToastKind.Warning);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Failed to archive plan: {ex.Message}", ToastKind.Danger);
            }
        }

        private async Task RestorePlanAsync()
        {
            var item = CurrentItem;
            if (item == null) return;

            try
            {
                await _api.RestoreSubscriptionAsync(item.SubscriptionId);
                SaasToast.Show(FindForm(), $"Plan '{item.SubscriptionName}' restored and activated.", ToastKind.Success);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Failed to restore plan: {ex.Message}", ToastKind.Danger);
            }
        }

        [DesignerCategory("Code")]
        private sealed class SurfaceCard : WorkbenchCard { }

        [DesignerCategory("Code")]
        private sealed class SegmentedFilter : Control
        {
            private readonly List<(string Key, int? Count)> _items = new();
            private int _selected = 0;
            private int _hover = -1;

            public event EventHandler<string>? SelectionChanged;

            public SegmentedFilter((string Key, int? Count)[] items)
            {
                _items.AddRange(items);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = UiKit.T.Surface;
                Cursor = Cursors.Hand;
                Font = UiKit.T.SmallStrong;
            }

            public int PreferredWidth
            {
                get
                {
                    int total = 16;
                    foreach (var item in _items)
                    {
                        string text = item.Count.HasValue ? $"{item.Key} ({item.Count})" : item.Key;
                        var sz = TextRenderer.MeasureText(text, Font);
                        total += Math.Max(104, sz.Width + 28);
                    }
                    return Math.Max(330, total);
                }
            }

            public void UpdateCounts(Dictionary<string, int?> counts)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (counts.TryGetValue(_items[i].Key, out var c))
                        _items[i] = (_items[i].Key, c);
                }
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                UiKit.FillRounded(g, new Rectangle(0, 0, Width, Height), 8, UiKit.T.LineSoft);

                if (_items.Count == 0) return;
                int segW = (Width - 4) / _items.Count;

                for (int i = 0; i < _items.Count; i++)
                {
                    var seg = new Rectangle(2 + i * segW, 2, segW, Height - 4);
                    bool active = i == _selected;
                    if (active)
                    {
                        UiKit.FillRounded(g, seg, 6, UiKit.T.Surface);
                        using var pen = new Pen(UiKit.T.Line, 1);
                        using var path = UiKit.Rounded(new Rectangle(seg.X, seg.Y, seg.Width - 1, seg.Height - 1), 6);
                        g.DrawPath(pen, path);
                    }

                    string text = _items[i].Count.HasValue
                        ? $"{_items[i].Key} ({_items[i].Count.GetValueOrDefault()})"
                        : _items[i].Key;

                    Color fg = active ? UiKit.T.Ink : (i == _hover ? UiKit.T.InkMuted : UiKit.T.InkFaint);
                    UiKit.Text(g, text, UiKit.T.SmallStrong, fg, seg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (_items.Count == 0) return;
                int segW = (Width - 4) / _items.Count;
                int idx = Math.Clamp((e.X - 2) / Math.Max(1, segW), 0, _items.Count - 1);
                if (idx != _hover) { _hover = idx; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hover = -1; Invalidate(); base.OnMouseLeave(e);
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                if (_items.Count == 0) return;
                int segW = (Width - 4) / _items.Count;
                int idx = Math.Clamp((e.X - 2) / Math.Max(1, segW), 0, _items.Count - 1);
                if (idx >= 0 && idx < _items.Count && idx != _selected)
                {
                    _selected = idx;
                    Invalidate();
                    SelectionChanged?.Invoke(this, _items[_selected].Key);
                }
                base.OnMouseClick(e);
            }
        }

        [DesignerCategory("Code")]
        private sealed class QuietMenuRenderer : ToolStripProfessionalRenderer
        {
            public QuietMenuRenderer() : base(new Colors()) { RoundedEdges = false; }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var r = new Rectangle(4, 0, e.Item.Width - 8, e.Item.Height);
                if (e.Item.Selected) UiKit.FillRounded(e.Graphics, r, 6, UiKit.T.RowHover);
            }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using var pen = new Pen(UiKit.T.LineSoft, 1);
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
            }
            private sealed class Colors : ProfessionalColorTable
            {
                public override Color ToolStripDropDownBackground => UiKit.T.Surface;
                public override Color MenuBorder => UiKit.T.Line;
                public override Color MenuItemBorder => UiKit.T.Surface;
                public override Color ImageMarginGradientBegin => UiKit.T.Surface;
                public override Color ImageMarginGradientMiddle => UiKit.T.Surface;
                public override Color ImageMarginGradientEnd => UiKit.T.Surface;
            }
        }
    }
}