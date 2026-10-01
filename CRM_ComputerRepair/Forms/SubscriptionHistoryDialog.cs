using CRM.winforms.Controls;
using CRM.winforms.Forms;
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
    public class SubscriptionHistoryDialog : ModalForm
    {
        private readonly ApiClient _api = new ApiClient();
        private readonly int? _companyId;
        private readonly string? _companyName;

        private DataGridView grid = null!;
        private Label lblCount = null!;
        private Label lblError = null!;
        private SaasButton btnDone = null!;
        private SaasButton btnRefresh = null!;

        public SubscriptionHistoryDialog(int? companyId = null, string? companyName = null)
        {
            _companyId = companyId;
            _companyName = companyName;

            string title = companyId.HasValue
                ? $"Subscription Audit History — {companyName}"
                : "Platform Subscription Audit History";
            string subtitle = companyId.HasValue
                ? $"Historical timeline of plan upgrades, downgrades, and module add-ons for {companyName}"
                : "Complete timeline of subscription tier transitions and billing changes across all companies";

            BuildCard(title, subtitle, width: 880, height: 620);
            BuildContent();
            this.Load += async (s, e) => await LoadDataAsync();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int y = ContentTopY;
            int w = ContentWidth;

            // Stats row
            lblCount = new Label
            {
                Text = "Loading timeline records...",
                Font = UiKit.Section,
                ForeColor = UiKit.Ink,
                Location = new Point(x, y),
                Size = new Size(w - 120, 24)
            };
            pnlBody.Controls.Add(lblCount);

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary);
            btnRefresh.Size = new Size(100, 30);
            btnRefresh.Location = new Point(x + w - 100, y - 2);
            btnRefresh.Click += async (s, e) => await LoadDataAsync();
            pnlBody.Controls.Add(btnRefresh);
            y += 34;

            // Grid
            grid = new DataGridView
            {
                Location = new Point(x, y),
                Size = new Size(w, 390),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                Font = UiKit.Body,
                GridColor = Color.FromArgb(243, 244, 246),
                EnableHeadersVisualStyles = false
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = UiKit.InkMuted;
            grid.ColumnHeadersDefaultCellStyle.Font = UiKit.SmallStrong;
            grid.ColumnHeadersHeight = 36;
            grid.RowTemplate.Height = 44;

            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "When", Width = 130 });
            if (!_companyId.HasValue)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Company", Width = 140 });
            }
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Event", Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Plan Transition", Width = 160 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Price", Width = 130 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Changed By", Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            pnlBody.Controls.Add(grid);
            y += 400;

            // Error Label
            lblError = new Label
            {
                Font = UiKit.Small,
                ForeColor = Color.FromArgb(239, 68, 68),
                Location = new Point(x, y),
                Size = new Size(w, 20),
                Visible = false
            };
            pnlBody.Controls.Add(lblError);

            // Footer
            btnDone = new SaasButton("Close", SaasButtonVariant.Primary);
            btnDone.Size = new Size(110, 36);
            btnDone.Location = new Point(ContentRightX - 110, 12);
            btnDone.Click += (s, e) => Close();
            pnlFooter.Controls.Add(btnDone);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                lblError.Visible = false;
                grid.Rows.Clear();
                lblCount.Text = "Fetching timeline records...";

                List<SubscriptionHistoryDto> history;
                if (_companyId.HasValue)
                {
                    history = await _api.GetCompanySubscriptionHistoryAsync(_companyId.Value);
                }
                else
                {
                    history = await _api.GetSubscriptionHistoryAsync();
                }

                lblCount.Text = $"{history.Count} Historical Event(s) Recorded";

                foreach (var h in history)
                {
                    int rowIdx = grid.Rows.Add();
                    var row = grid.Rows[rowIdx];

                    int col = 0;
                    row.Cells[col++].Value = h.WhenDisplay;
                    if (!_companyId.HasValue)
                    {
                        row.Cells[col++].Value = h.CompanyName;
                    }
                    row.Cells[col++].Value = h.ChangeType;
                    row.Cells[col++].Value = h.PlanTransitionDisplay;
                    row.Cells[col++].Value = h.PriceTransitionDisplay;
                    row.Cells[col++].Value = string.IsNullOrWhiteSpace(h.ChangedBy) ? "System" : h.ChangedBy;
                    row.Cells[col++].Value = h.Notes ?? "—";

                    // Styling cell based on change type
                    int eventCol = !_companyId.HasValue ? 2 : 1;
                    if (h.ChangeType.Equals("UPGRADE", StringComparison.OrdinalIgnoreCase))
                    {
                        row.Cells[eventCol].Style.ForeColor = Color.FromArgb(16, 185, 129); // Emerald
                        row.Cells[eventCol].Style.Font = UiKit.SmallStrong;
                    }
                    else if (h.ChangeType.Equals("DOWNGRADE", StringComparison.OrdinalIgnoreCase))
                    {
                        row.Cells[eventCol].Style.ForeColor = Color.FromArgb(245, 158, 11); // Amber
                        row.Cells[eventCol].Style.Font = UiKit.SmallStrong;
                    }
                    else if (h.ChangeType.Contains("ADDON"))
                    {
                        row.Cells[eventCol].Style.ForeColor = Color.FromArgb(59, 130, 246); // Blue
                    }
                }
            }
            catch (Exception ex)
            {
                lblError.Text = $"Failed to load history: {ex.Message}";
                lblError.Visible = true;
            }
        }
    }
}
