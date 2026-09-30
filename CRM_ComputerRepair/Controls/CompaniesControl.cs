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
    public class CompaniesControl : UserControl
    {
        private readonly ApiClient _api = new ApiClient();
        private List<CompanyDto> _all = new();
        private List<CompanyDto> _filtered = new();
        private string _activeFilter = "All";

        // ── Controls ──
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private SaasButton btnRegister = null!;
        private SaasButton btnRefresh = null!;

        // ── Stat Tiles ──
        private StatTile tileTotal = null!;
        private StatTile tileActive = null!;
        private StatTile tileDeactivated = null!;
        private StatTile tileDatabases = null!;

        // ── Workbench Card ──
        private SurfaceCard card = null!;
        private WorkbenchSearch searchBox = null!;
        private SegmentedFilter filterBar = null!;
        private Label lblCount = null!;
        private DataGridView dgv = null!;
        private SaasEmptyState emptyState = null!;

        private ContextMenuStrip _actionsMenu = null!;
        private int _menuRowIndex = -1;
        private int _hoverRow = -1;

        public CompaniesControl()
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

            var miView = new ToolStripMenuItem("View Dossier") { Height = 32 };
            miView.Click += (s, e) => ViewCompany();

            var miEdit = new ToolStripMenuItem("Edit Details") { Height = 32 };
            miEdit.Click += (s, e) => EditCompany();

            var miToggle = new ToolStripMenuItem("Toggle Active / Deactivate") { Height = 32 };
            miToggle.Click += async (s, e) => await ToggleCompanyStatusAsync();

            _actionsMenu.Items.AddRange(new ToolStripItem[] { miView, miEdit, new ToolStripSeparator(), miToggle });
        }

        private void BuildUi()
        {
            // Header
            lblTitle = new Label
            {
                Text = "Tenants / Businesses",
                Font = UiKit.T.Title,
                ForeColor = UiKit.T.Ink,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Manage client companies and tenant instances.",
                Font = UiKit.T.Subtitle,
                ForeColor = UiKit.T.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            btnRegister = new SaasButton("Register Business", SaasButtonVariant.Primary, "\uE710");
            btnRegister.Size = new Size(185, UiKit.T.ButtonHeight);
            btnRegister.Click += async (s, e) => await RegisterAsync();

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Size = new Size(100, UiKit.T.ButtonHeight);
            btnRefresh.Click += async (s, e) => await ReloadAsync();

            Controls.Add(lblTitle);
            Controls.Add(lblSubtitle);
            Controls.Add(btnRegister);
            Controls.Add(btnRefresh);

            // KPI Tiles with distinct accent color coding
            tileTotal = new StatTile("TOTAL BUSINESSES", "0", "\uE716");
            tileTotal.SetColors(AppTheme.Surface, AppTheme.Primary);

            tileActive = new StatTile("ACTIVE TENANTS", "0", "\uE73E");
            tileActive.SetColors(AppTheme.Surface, AppTheme.Success);

            tileDeactivated = new StatTile("DEACTIVATED", "0", "\uE711");
            tileDeactivated.SetColors(AppTheme.Surface, AppTheme.Warning);

            tileDatabases = new StatTile("ISOLATED DATABASES", "0", "\uE7BA");
            tileDatabases.SetColors(AppTheme.Surface, Color.FromArgb(139, 92, 246));

            Controls.Add(tileTotal);
            Controls.Add(tileActive);
            Controls.Add(tileDeactivated);
            Controls.Add(tileDatabases);

            // Workbench Surface Card
            card = new SurfaceCard();

            searchBox = new WorkbenchSearch
            {
                Placeholder = "Search by company name, code, city, admin, or database..."
            };
            searchBox.QueryChanged += (s, e) => ApplyFilter();

            filterBar = new SegmentedFilter(new (string, int?)[]
            {
                ("All", null),
                ("Active", null),
                ("Deactivated", null)
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
                    ViewCompany();
            };
            dgv.CellMouseMove += (s, e) =>
            {
                if (e.RowIndex != _hoverRow)
                {
                    int old = _hoverRow;
                    _hoverRow = e.RowIndex;
                    if (old >= 0 && old < dgv.RowCount) dgv.InvalidateRow(old);
                    if (_hoverRow >= 0 && _hoverRow < dgv.RowCount) dgv.InvalidateRow(_hoverRow);
                }
            };
            dgv.CellMouseLeave += (s, e) =>
            {
                if (_hoverRow >= 0 && _hoverRow < dgv.RowCount)
                {
                    int old = _hoverRow;
                    _hoverRow = -1;
                    dgv.InvalidateRow(old);
                }
            };

            emptyState = new SaasEmptyState
            {
                Icon = "\uE716",
                Text = "No businesses registered yet",
                Subtitle = "Click 'Register Business' to provision your first client tenant workspace."
            };
            emptyState.SetAction("Register Business", async (s, e) => await RegisterAsync());

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
            btnRegister.Location = new Point(btnRefresh.Left - btnRegister.Width - 12, pad);

            // Stats
            int tileY = lblSubtitle.Bottom + UiKit.T.S5;
            int gap = UiKit.T.S4;
            int tileW = Math.Max(180, (Width - pad * 2 - gap * 3) / 4);
            int tileH = 92;

            tileTotal.SetBounds(pad, tileY, tileW, tileH);
            tileActive.SetBounds(pad + (tileW + gap), tileY, tileW, tileH);
            tileDeactivated.SetBounds(pad + (tileW + gap) * 2, tileY, tileW, tileH);
            tileDatabases.SetBounds(pad + (tileW + gap) * 3, tileY, tileW, tileH);

            // Card
            int cardY = tileY + tileH + UiKit.T.S5;
            int cardH = Height - cardY - pad;
            card.SetBounds(pad, cardY, Width - pad * 2, Math.Max(260, cardH));

            // Card content
            int innerW = card.Width - pad * 2;
            int filterW = filterBar.PreferredWidth;
            int searchW = Math.Min(460, innerW - filterW - 20);
            searchBox.SetBounds(pad, pad, Math.Max(240, searchW), 38);
            filterBar.SetBounds(card.Width - pad - filterW, pad + 1, filterW, 36);

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
            g.RowTemplate.Height = 60;
            g.ColumnHeadersHeight = 44;

            g.ColumnHeadersDefaultCellStyle.BackColor = UiKit.T.Surface;
            g.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.T.InkMuted;
            g.ColumnHeadersDefaultCellStyle.Font = UiKit.T.SmallStrong;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 0, 12, 0);

            g.DefaultCellStyle.BackColor = UiKit.T.Surface;
            g.DefaultCellStyle.ForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Font = UiKit.T.Body;
            g.DefaultCellStyle.SelectionBackColor = UiKit.T.RowHover;
            g.DefaultCellStyle.SelectionForeColor = UiKit.T.Ink;
            g.DefaultCellStyle.Padding = new Padding(12, 8, 12, 8);
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            g.Columns.Clear();

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCode",
                HeaderText = "CODE",
                DataPropertyName = "CompanyCode",
                Width = 145,
                MinimumWidth = 130
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "BUSINESS NAME",
                DataPropertyName = "CompanyName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 200,
                MinimumWidth = 220
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colContact",
                HeaderText = "CONTACT",
                DataPropertyName = "ContactEmail",
                Width = 240,
                MinimumWidth = 210
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colLocation",
                HeaderText = "LOCATION",
                DataPropertyName = "LocationDisplay",
                Width = 180,
                MinimumWidth = 160
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDatabase",
                HeaderText = "TENANT DB",
                DataPropertyName = "DatabaseName",
                Width = 240,
                MinimumWidth = 220
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPlan",
                HeaderText = "SUBSCRIPTION",
                DataPropertyName = "PlanDisplay",
                Width = 220,
                MinimumWidth = 190
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                HeaderText = "STATUS",
                Width = 115,
                MinimumWidth = 105
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colTerms",
                HeaderText = "TERMS & CONDITIONS",
                Width = 145,
                MinimumWidth = 130
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCreated",
                HeaderText = "REGISTERED",
                Width = 135,
                MinimumWidth = 125
            });

            var btnCol = new DataGridViewButtonColumn
            {
                Name = "colActions",
                HeaderText = "",
                Text = "\u22EF",
                UseColumnTextForButtonValue = true,
                Width = 52,
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

                // Status Pill Cell
                if (e.ColumnIndex == g.Columns["colStatus"]!.Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    var stateG = e.Graphics;
                    UiKit.Quality(stateG);

                    Color bg = item.IsActive ? UiKit.Wash(AppTheme.Success) : UiKit.Wash(AppTheme.Danger);
                    Color fg = item.IsActive ? AppTheme.Success : AppTheme.Danger;
                    string txt = item.IsActive ? "ACTIVE" : "DEACTIVATED";

                    var pill = new Rectangle(e.CellBounds.Left + 8, e.CellBounds.Top + (e.CellBounds.Height - 22) / 2, 94, 22);
                    UiKit.FillRounded(stateG, pill, 11, bg);
                    UiKit.Text(stateG, txt, UiKit.MicroStrong, fg, pill, UiKit.Center);
                    e.Handled = true;
                }
                else if (e.ColumnIndex == g.Columns["colTerms"]!.Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    var stateG = e.Graphics;
                    UiKit.Quality(stateG);

                    Color bg = item.HasAcceptedTerms ? Color.FromArgb(235, 248, 238) : Color.FromArgb(254, 243, 235);
                    Color fg = item.HasAcceptedTerms ? AppTheme.Success : Color.FromArgb(196, 92, 0);
                    string txt = item.HasAcceptedTerms ? "ACCEPTED" : "PENDING";

                    var pill = new Rectangle(e.CellBounds.Left + 8, e.CellBounds.Top + (e.CellBounds.Height - 22) / 2, 100, 22);
                    UiKit.FillRounded(stateG, pill, 11, bg);
                    UiKit.Text(stateG, txt, UiKit.MicroStrong, fg, pill, UiKit.Center);
                    e.Handled = true;
                }
                else if (e.ColumnIndex == g.Columns["colCreated"]!.Index)
                {
                    e.PaintBackground(e.CellBounds, true);
                    var textRect = new Rectangle(e.CellBounds.Left + 12, e.CellBounds.Top, Math.Max(0, e.CellBounds.Width - 16), e.CellBounds.Height);
                    UiKit.Text(e.Graphics, item.CreatedAt.ToString("MMM d, yyyy"), UiKit.T.Small, UiKit.T.InkMuted,
                        textRect, UiKit.Left);
                    e.Handled = true;
                }
            };
        }

        private async Task ReloadAsync()
        {
            try
            {
                _all = await _api.GetCompaniesAsync();

                int total = _all.Count;
                int active = _all.Count(c => c.IsActive);
                int deact = _all.Count(c => !c.IsActive);

                tileTotal.Value = total.ToString();
                tileActive.Value = active.ToString();
                tileDeactivated.Value = deact.ToString();
                tileDatabases.Value = total.ToString();

                filterBar.UpdateCounts(new Dictionary<string, int?>
                {
                    ["All"] = total,
                    ["Active"] = active,
                    ["Deactivated"] = deact
                });

                ApplyFilter();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Failed to load businesses: {ex.Message}", ToastKind.Danger);
            }
        }

        private void ApplyFilter()
        {
            string query = searchBox.Query.Trim().ToLowerInvariant();

            _filtered = _all.Where(c =>
            {
                if (_activeFilter == "Active" && !c.IsActive) return false;
                if (_activeFilter == "Deactivated" && c.IsActive) return false;

                if (!string.IsNullOrWhiteSpace(query))
                {
                    bool matchName = c.CompanyName.ToLowerInvariant().Contains(query);
                    bool matchCode = c.CompanyCode.ToLowerInvariant().Contains(query);
                    bool matchContact = (c.ContactEmail?.ToLowerInvariant().Contains(query) ?? false) || (c.ContactPhone?.Contains(query) ?? false);
                    bool matchLoc = c.LocationDisplay.ToLowerInvariant().Contains(query);
                    bool matchDb = c.DatabaseName.ToLowerInvariant().Contains(query);
                    bool matchAdmin = (c.AdminUsername?.ToLowerInvariant().Contains(query) ?? false) || (c.AdminFullName?.ToLowerInvariant().Contains(query) ?? false);
                    if (!matchName && !matchCode && !matchContact && !matchLoc && !matchDb && !matchAdmin) return false;
                }

                return true;
            }).ToList();

            dgv.DataSource = null;
            dgv.DataSource = _filtered;

            bool hasData = _filtered.Count > 0;
            dgv.Visible = hasData;
            emptyState.Visible = !hasData;

            lblCount.Text = $"Showing {_filtered.Count} of {_all.Count} businesses";
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

        private CompanyDto? CurrentItem => _menuRowIndex >= 0 && _menuRowIndex < _filtered.Count
            ? _filtered[_menuRowIndex]
            : (dgv.CurrentRow != null && dgv.CurrentRow.Index < _filtered.Count ? _filtered[dgv.CurrentRow.Index] : null);

        private async Task RegisterAsync()
        {
            using var dlg = new CompanyRegisterDialog();
            if (dlg.ShowModal(FindForm()) == DialogResult.OK && dlg.RegisteredCompany != null)
            {
                SaasToast.Show(FindForm(),
                    $"Successfully registered '{dlg.RegisteredCompany.CompanyName}'!",
                    ToastKind.Success,
                    $"Tenant Database & Admin account provisioned.");
                await ReloadAsync();
            }
        }

        private void ViewCompany()
        {
            var item = CurrentItem;
            if (item == null) return;

            using var dlg = new CompanyViewDialog(item);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK && dlg.RequestedEdit)
            {
                EditCompany();
            }
        }

        private void EditCompany()
        {
            var item = CurrentItem;
            if (item == null) return;

            using var dlg = new CompanyEditDialog(item);
            if (dlg.ShowModal(FindForm()) == DialogResult.OK)
            {
                SaasToast.Show(FindForm(), $"Updated '{item.CompanyName}'.", ToastKind.Success);
                _ = ReloadAsync();
            }
        }

        private async Task ToggleCompanyStatusAsync()
        {
            var item = CurrentItem;
            if (item == null) return;

            string action = item.IsActive ? "deactivate" : "activate";
            bool confirm = SaasConfirm.Ask(
                FindForm(),
                $"Confirm {char.ToUpper(action[0]) + action.Substring(1)}",
                $"Are you sure you want to {action} '{item.CompanyName}' ({item.CompanyCode})?",
                confirmText: item.IsActive ? "Deactivate" : "Activate",
                danger: item.IsActive,
                detail: item.IsActive
                    ? "Users belonging to this business tenant will not be able to log in while deactivated."
                    : "Access to this business tenant workspace will be restored immediately.");

            if (!confirm) return;

            try
            {
                bool newState = await _api.ToggleCompanyStatusAsync(item.CompanyId);
                item.IsActive = newState;
                SaasToast.Show(FindForm(),
                    $"Business '{item.CompanyName}' is now {(newState ? "active" : "deactivated")}.",
                    newState ? ToastKind.Success : ToastKind.Warning);
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                SaasToast.Show(FindForm(), $"Failed to update status: {ex.Message}", ToastKind.Danger);
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
