using CRM.winforms.Auth;
using CRM.winforms.Controls;
using CRM.winforms.Forms;
using CRM.winforms.Reports;
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

        private SaasButton btnExport = null!;
        private SaasButton btnRefresh = null!;
        private Label lblDataTitle = null!;

        private DataGridView dgv = null!;
        private SaasEmptyState emptyState = null!;
        private Label lblCount = null!;

        public DrillDownDialog(string title, AnalyticsDetailsDto details)
        {
            _prefetched = details;
            BuildCard(title, "Detailed analytics records and metric breakdown.", width: 940, height: 640);
            BuildContent();
        }

        public DrillDownDialog(string title, Func<Task<AnalyticsDetailsDto?>> loader)
        {
            _loader = loader;
            BuildCard(title, "Detailed analytics records and metric breakdown.", width: 940, height: 640);
            BuildContent();
            _ = LoadAsync();
        }

        private void BuildContent()
        {
            int x = 24;
            int w = pnlBody.ClientSize.Width - 48;
            int y = 16;

            lblDataTitle = new Label
            {
                Text = "",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(w - 260, 32)
            };
            pnlBody.Controls.Add(lblDataTitle);

            btnRefresh = new SaasButton("Refresh", SaasButtonVariant.Secondary, "\uE72C");
            btnRefresh.Size = new Size(110, 32);
            btnRefresh.Location = new Point(x + w - btnRefresh.Width, y);
            pnlBody.Controls.Add(btnRefresh);
            btnRefresh.Click += (s, e) => _ = LoadAsync();

            btnExport = new SaasButton("Export PDF", SaasButtonVariant.Secondary, "\uE74E");
            btnExport.Size = new Size(120, 32);
            btnExport.Location = new Point(btnRefresh.Left - btnExport.Width - 8, y);
            pnlBody.Controls.Add(btnExport);
            btnExport.Click += (s, e) => ExportPdf();

            y += 42;

            dgv = new DataGridView
            {
                Location = new Point(x, y),
                Size = new Size(w, Math.Max(200, pnlBody.ClientSize.Height - y - 16)),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Both
            };
            TableKit.StyleGrid(dgv);
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            pnlBody.Controls.Add(dgv);

            emptyState = new SaasEmptyState
            {
                Icon = "\uE9D9",
                Text = "No records found",
                Subtitle = "No records found for this selection.",
                Location = dgv.Location,
                Size = dgv.Size,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false
            };
            pnlBody.Controls.Add(emptyState);

            lblCount = new Label
            {
                Text = "",
                Font = UiKit.T.Small,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(24, 22)
            };
            pnlFooter.Controls.Add(lblCount);

            var btnClose = new SaasButton("Close", SaasButtonVariant.Secondary);
            btnClose.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            LayoutFooter(btnClose, null, saveW: 100);

            AcceptButton = btnClose;
            CancelButton = btnClose;
            Shown += (s, e) => btnClose.Focus();
        }

        private async Task LoadAsync()
        {
            try
            {
                dgv.Visible = true;
                emptyState.Visible = false;

                var data = _prefetched ?? await _loader!();

                if (data is null)
                {
                    ShowEmpty("No data returned.");
                    return;
                }

                lblDataTitle.Text = data.Title ?? $"Metric: {data.Metric}";

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
                    dgv.Rows.Add(cells!);
                }

                lblCount.Text = $"{data.Rows.Count:N0} record{(data.Rows.Count == 1 ? "" : "s")}";
                bool hasRows = data.Rows.Count > 0;
                dgv.Visible = hasRows;
                emptyState.Visible = !hasRows;
                if (!hasRows)
                {
                    if (string.IsNullOrWhiteSpace(emptyState.Subtitle))
                        emptyState.Subtitle = "No records found for this selection.";
                    emptyState.BringToFront();
                }
                else dgv.ClearSelection();
            }
            catch (Exception ex)
            {
                ShowEmpty($"Couldn't load details.\n\n{ex.Message}");
            }
        }

        private void ShowEmpty(string message)
        {
            emptyState.Subtitle = message;
            emptyState.Visible = true;
            emptyState.BringToFront();
            dgv.Visible = false;
            lblCount.Text = "";
        }

        private static object? FormatCell(object? value, string type)
        {
            if (value is null) return "—";
            // API dictionaries deserialize as JsonElement — unwrap to CLR first.
            if (value is System.Text.Json.JsonElement je)
            {
                switch (je.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.Null:
                    case System.Text.Json.JsonValueKind.Undefined:
                        return "—";
                    case System.Text.Json.JsonValueKind.String:
                        value = je.GetString();
                        if (value is null) return "—";
                        break;
                    case System.Text.Json.JsonValueKind.Number:
                        // Keep as decimal for currency/number formatting below.
                        value = je.GetDecimal();
                        break;
                    case System.Text.Json.JsonValueKind.True:
                        value = true;
                        break;
                    case System.Text.Json.JsonValueKind.False:
                        value = false;
                        break;
                    default:
                        value = je.GetRawText();
                        break;
                }
                if (value is null) return "—";
            }
            try
            {
                switch (type)
                {
                    case "currency":
                        return Convert.ToDecimal(value);
                    case "number":
                        if (value is decimal dec) return dec.ToString("0.##");
                        if (value is double dbl) return dbl.ToString("0.##");
                        if (decimal.TryParse(value.ToString(), out var n)) return n.ToString("0.##");
                        return value.ToString();
                    case "date":
                        if (value is DateTime dto) return dto;
                        if (DateTime.TryParse(value.ToString(), out var d2))
                            return d2;
                        return "—";
                    default:
                        {
                            var s = value.ToString();
                            return string.IsNullOrEmpty(s) ? "—" : s;
                        }
                }
            }
            catch
            {
                var s = value.ToString();
                return string.IsNullOrEmpty(s) ? "—" : s;
            }
        }

        private void ExportPdf()
        {
            if (dgv.Rows.Count == 0)
            {
                MessageBox.Show("Nothing to export.", "Export PDF",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string safeTitle = string.Concat(Text.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
            if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "DrillDown";

            using var sfd = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"Fixory_{safeTitle}_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                var visibleCols = dgv.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).ToList();
                var orientation = visibleCols.Count > 5 ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;

                var doc = new PdfReportBuilder.ReportDocument
                {
                    Orientation = orientation,
                    Metadata = new PdfReportBuilder.ReportMetadata
                    {
                        CompanyName = "FIXORY COMPUTER REPAIR",
                        SystemTagline = "Executive Business Intelligence & Operational Audit",
                        ReportTitle = Text,
                        Subtitle = lblDataTitle.Text,
                        PeriodText = "Analytics Drill-Down",
                        GeneratedBy = !string.IsNullOrWhiteSpace(UserSession.FullName) ? UserSession.FullName : (!string.IsNullOrWhiteSpace(UserSession.Username) ? UserSession.Username : "User"),
                        GeneratedAt = DateTime.Now
                    }
                };

                // Add columns
                foreach (var col in visibleCols)
                {
                    string h = col.HeaderText ?? "";
                    string hLower = h.ToLowerInvariant();

                    var align = PdfSharp.Drawing.XStringAlignment.Near;
                    bool isBold = false;
                    bool isPill = false;

                    if (hLower.Contains("amount") || hLower.Contains("price") || hLower.Contains("cost") ||
                        hLower.Contains("total") || hLower.Contains("revenue") || hLower.Contains("spent") ||
                        hLower.Contains("balance") || hLower.Contains("points"))
                    {
                        align = PdfSharp.Drawing.XStringAlignment.Far;
                        isBold = true;
                    }
                    else if (hLower.Contains("status") || hLower.Contains("priority") || hLower.Contains("tier") || hLower.Contains("active"))
                    {
                        align = PdfSharp.Drawing.XStringAlignment.Center;
                        isPill = true;
                    }
                    else if (hLower.Contains("date") || hLower.Contains("id") || hLower.Contains("ticket"))
                    {
                        align = PdfSharp.Drawing.XStringAlignment.Center;
                    }

                    double w = Math.Max(60.0, Math.Min(200.0, col.Width * 0.75));
                    doc.Columns.Add(new PdfReportBuilder.ColumnDef
                    {
                        Header = h,
                        Width = w,
                        Alignment = align,
                        IsBold = isBold,
                        IsPillBadge = isPill
                    });
                }

                // Add rows
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    var cells = visibleCols.Select(c => row.Cells[c.Index].FormattedValue?.ToString() ?? "—").ToArray();
                    doc.Rows.Add(cells);
                }

                doc.SummaryFooterText = $"Total Records: {dgv.Rows.Count:N0}";

                PdfReportBuilder.GenerateReport(doc, sfd.FileName);
                MessageBox.Show($"Exported {dgv.Rows.Count:N0} records to PDF successfully.\n\n{sfd.FileName}",
                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed.\n\n{ex.Message}",
                    "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
