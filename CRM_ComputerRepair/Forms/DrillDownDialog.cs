using CRM.winforms.Controls;
using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Drill-down modal for analytics: shows the actual customer / transaction
    /// records behind a KPI or chart data point.
    ///
    /// Accepts either a pre-fetched <see cref="AnalyticsDetailsDto"/> (generic
    /// columns + rows from /analytics/details or /analytics/inactive-customers)
    /// or a list of typed rows loaded by the caller via <see cref="Loader"/>.
    /// </summary>
    [DesignerCategory("Code")]
    public class DrillDownDialog : ModalForm
    {
        private readonly Func<Task<AnalyticsDetailsDto?>>? _loader;
        private readonly AnalyticsDetailsDto? _prefetched;

        private Label lblSubtitle = null!;
        private Button btnExport = null!;
        private Button btnRefresh = null!;

        private DataGridView dgv = null!;
        private Label lblEmpty = null!;
        private Label lblCount = null!;

        public DrillDownDialog(string title, AnalyticsDetailsDto details)
        {
            _prefetched = details;
            BuildCard(title, width: 900, height: 620);
            BuildContent();
        }

        public DrillDownDialog(string title, Func<Task<AnalyticsDetailsDto?>> loader)
        {
            _loader = loader;
            BuildCard(title, width: 900, height: 620);
            BuildContent();
            _ = LoadAsync();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            lblSubtitle = new Label
            {
                Text = "",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w - 220, 34)
            };
            pnlCard.Controls.Add(lblSubtitle);

            btnRefresh = MakeSecondaryButton("Refresh", "\uE72C");
            btnRefresh.Location = new Point(x + w - btnRefresh.Width, y + 2);
            pnlCard.Controls.Add(btnRefresh);
            btnRefresh.Click += (s, e) => _ = LoadAsync();

            btnExport = MakeSecondaryButton("Export CSV", "\uE74E");
            btnExport.Location = new Point(btnRefresh.Left - btnExport.Width - 8, y + 2);
            pnlCard.Controls.Add(btnExport);
            btnExport.Click += (s, e) => ExportCsv();

            y += 44;

            dgv = new DataGridView
            {
                Location = new Point(x, y),
                Size = new Size(w, pnlCard.Height - y - ShadowPad - 60),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = AppTheme.Neutral,
                RowTemplate = { Height = 34 }
            };
            dgv.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.Surface;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = AppTheme.TextSecondary;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
            dgv.ColumnHeadersHeight = 36;
            dgv.DefaultCellStyle.BackColor = Color.White;
            dgv.DefaultCellStyle.ForeColor = AppTheme.TextPrimary;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgv.DefaultCellStyle.SelectionBackColor = AppTheme.PrimaryHover;
            dgv.DefaultCellStyle.SelectionForeColor = AppTheme.TextPrimary;
            dgv.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            dgv.AlternatingRowsDefaultCellStyle.BackColor = AppTheme.Neutral;
            pnlCard.Controls.Add(dgv);

            lblEmpty = new Label
            {
                Text = "No records found for this selection.",
                Font = new Font("Segoe UI", 10F),
                ForeColor = AppTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Visible = false,
                Size = dgv.Size,
                Location = dgv.Location
            };
            pnlCard.Controls.Add(lblEmpty);

            lblCount = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, pnlCard.Height - ShadowPad - 40)
            };
            pnlCard.Controls.Add(lblCount);

            var btnClose = new Button
            {
                Text = "Close",
                Font = new Font("Segoe UI Semibold", 9.5F),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 40),
                Location = new Point(ContentRightX - 100, pnlCard.Height - ShadowPad - 52),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.FlatAppearance.BorderColor = AppTheme.BorderStrong;
            btnClose.FlatAppearance.MouseOverBackColor = AppTheme.Neutral;
            btnClose.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(btnClose, 8);
            btnClose.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            pnlCard.Controls.Add(btnClose);
        }

        private async Task LoadAsync()
        {
            try
            {
                dgv.Visible = true;
                lblEmpty.Visible = false;

                var data = _prefetched ?? await _loader!();

                if (data is null)
                {
                    ShowEmpty("No data returned.");
                    return;
                }

                lblSubtitle.Text = data.Title ?? $"Metric: {data.Metric}";

                dgv.Columns.Clear();
                dgv.Rows.Clear();

                // Columns from the API's column contract.
                foreach (var col in data.Columns)
                {
                    var c = new DataGridViewTextBoxColumn
                    {
                        Name = col.Key,
                        HeaderText = col.Label,
                        SortMode = DataGridViewColumnSortMode.Automatic,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                        MinimumWidth = 80
                    };

                    if (col.Type == "currency")
                        c.DefaultCellStyle.Format = "N2";
                    else if (col.Type == "date")
                        c.DefaultCellStyle.Format = "MMM d, yyyy";

                    dgv.Columns.Add(c);
                }

                // Rows: dictionary values aligned by column key.
                foreach (var row in data.Rows)
                {
                    var cells = new object?[data.Columns.Count];
                    for (int i = 0; i < data.Columns.Count; i++)
                        cells[i] = FormatCell(row.TryGetValue(data.Columns[i].Key, out var v) ? v : null,
                            data.Columns[i].Type);
                    dgv.Rows.Add(cells);
                }

                lblCount.Text = $"{data.Rows.Count:N0} record{(data.Rows.Count == 1 ? "" : "s")}";
                dgv.Visible = data.Rows.Count > 0;
                lblEmpty.Visible = data.Rows.Count == 0;
            }
            catch (Exception ex)
            {
                ShowEmpty($"Couldn't load details.\n\n{ex.Message}");
            }
        }

        private void ShowEmpty(string message)
        {
            lblEmpty.Text = message;
            lblEmpty.Visible = true;
            dgv.Visible = false;
            lblCount.Text = "";
        }

        private static object? FormatCell(object? value, string type)
        {
            if (value is null) return "—";
            try
            {
                switch (type)
                {
                    case "currency":
                        return value is System.Text.Json.JsonElement je1 && je1.ValueKind == System.Text.Json.JsonValueKind.Number
                            ? je1.GetDecimal()
                            : Convert.ToDecimal(value);
                    case "number":
                        return value is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.Number
                            ? je2.GetDecimal().ToString("0.##")
                            : value;
                    case "date":
                        if (value is System.Text.Json.JsonElement je3 &&
                            je3.ValueKind == System.Text.Json.JsonValueKind.String &&
                            DateTime.TryParse(je3.GetString(), out var dt))
                            return dt;
                        if (DateTime.TryParse(value.ToString(), out var d2))
                            return d2;
                        return value;
                    default:
                        return value.ToString();
                }
            }
            catch
            {
                return value.ToString();
            }
        }

        private void ExportCsv()
        {
            if (dgv.Rows.Count == 0)
            {
                MessageBox.Show("Nothing to export.", "Export CSV",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "CSV files|*.csv",
                FileName = $"drilldown_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(",",
                    dgv.Columns.Cast<DataGridViewColumn>().Select(c => Csv(c.HeaderText))));

                foreach (DataGridViewRow row in dgv.Rows)
                {
                    sb.AppendLine(string.Join(",",
                        row.Cells.Cast<DataGridViewCell>().Select(c => Csv(c.FormattedValue?.ToString() ?? ""))));
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Exported {dgv.Rows.Count} rows.\n\n{sfd.FileName}",
                    "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed.\n\n{ex.Message}",
                    "Export error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string Csv(string value) =>
            $"\"{(value ?? "").Replace("\"", "\"\"")}\"";

        private static Button MakeSecondaryButton(string text, string glyph)
        {
            var b = new Button
            {
                Text = $"{glyph}  {text}",
                Font = new Font("Segoe UI Semibold", 9F),
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(120, 34),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = AppTheme.BorderStrong;
            b.FlatAppearance.MouseOverBackColor = AppTheme.Neutral;
            b.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(b, 8);
            return b;
        }
    }
}
