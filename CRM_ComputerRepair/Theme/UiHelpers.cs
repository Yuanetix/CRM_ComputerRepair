using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms
{
    public static class UiHelpers
    {
        // ═══════════ BUTTONS ═══════════

        public static Button CreatePrimaryButton(string text, Color background)
        {
            var btn = CreateBaseButton(text, background, Color.White);

            if (background == AppTheme.Primary)
            {
                btn.FlatAppearance.MouseOverBackColor = AppTheme.PrimaryHover;
                btn.FlatAppearance.MouseDownBackColor = AppTheme.PrimaryActive;
            }
            else if (background == AppTheme.Danger)
            {
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 60, 60);
                btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(200, 45, 45);
            }
            else if (background == AppTheme.Success)
            {
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(10, 160, 110);
                btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(5, 145, 100);
            }

            return btn;
        }

        public static Button CreateSecondaryButton(string text, Color border, Color textColor)
        {
            var btn = CreateBaseButton(text, Color.White, textColor);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = border;
            btn.FlatAppearance.MouseOverBackColor = AppTheme.Neutral;
            btn.FlatAppearance.MouseDownBackColor = AppTheme.Border;
            return btn;
        }

        public static Button CreateNeutralButton(string text)
        {
            var btn = CreateBaseButton(text, AppTheme.Neutral, AppTheme.TextPrimary);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(229, 231, 235);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(209, 213, 219);
            return btn;
        }

        public static Button CreateButton(string text, Color background, Color foreground)
        {
            return CreateBaseButton(text, background, foreground);
        }

        private static Button CreateBaseButton(string text, Color background, Color foreground)
        {
            var btn = new Button
            {
                Text = text,
                Font = AppTheme.FontButton,
                BackColor = background,
                ForeColor = foreground,
                FlatStyle = FlatStyle.Flat,
                Height = AppTheme.ButtonHeight,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Padding = new Padding(16, 0, 16, 0)
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.Resize += (s, e) => ApplyRoundedRegion(btn, 6);

            return btn;
        }

        // ═══════════ TEXT INPUTS ═══════════

        public static TextBox CreateTextBox()
        {
            return new TextBox
            {
                Font = AppTheme.FontInput,
                BorderStyle = BorderStyle.FixedSingle,
                Height = AppTheme.InputHeight,
                BackColor = AppTheme.InputBg,
                ForeColor = AppTheme.TextPrimary,
                Padding = new Padding(8, 6, 8, 6)
            };
        }

        // ═══════════ LABELS ═══════════

        public static Label CreateLabel(string text, Font font, Color color)
        {
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        public static Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = AppTheme.FontLabel,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        public static Label CreateErrorLabel()
        {
            return new Label
            {
                Text = string.Empty,
                Font = AppTheme.FontError,
                ForeColor = AppTheme.Danger,
                AutoSize = true,
                BackColor = Color.Transparent,
                Visible = false
            };
        }

        // ═══════════ CARDS ═══════════

        public static Panel CreateCard()
        {
            var panel = new Panel
            {
                BackColor = AppTheme.Surface,
                Padding = new Padding(AppTheme.CardPadding)
            };

            panel.Paint += (s, e) =>
            {
                var p = (Panel)s;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using var pen = new Pen(AppTheme.Border, 1);
                var rect = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
                DrawRoundedRectangle(e.Graphics, pen, rect, AppTheme.Radius);
            };

            return panel;
        }

        // ═══════════ ROUNDED CORNERS ═══════════

        public static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0) return;

            using var path = GetRoundedPath(
                new Rectangle(0, 0, control.Width, control.Height), radius);
            control.Region = new Region(path);
        }

        public static void DrawRoundedRectangle(
            Graphics g, Pen pen, Rectangle rect, int radius)
        {
            using var path = GetRoundedPath(rect, radius);
            g.DrawPath(pen, path);
        }

        public static void FillRoundedRectangle(
            Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using var path = GetRoundedPath(rect, radius);
            g.FillPath(brush, path);
        }

        private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        // ═══════════ GRID ═══════════

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(238, 240, 245);

            grid.EnableHeadersVisualStyles = false;

            // Header
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = AppTheme.TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5F);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 10, 12, 10);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(249, 250, 252);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = AppTheme.TextPrimary;
            grid.ColumnHeadersHeight = 44;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Cells
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            grid.DefaultCellStyle.ForeColor = AppTheme.TextPrimary;
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = AppTheme.PrimarySoft;
            grid.DefaultCellStyle.SelectionForeColor = AppTheme.TextPrimary;
            grid.DefaultCellStyle.Padding = new Padding(12, 8, 12, 8);
            grid.RowTemplate.Height = 40;

            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 253, 255);

            // Behavior
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        // ═══════════ VALIDATION ═══════════

        public static void ShowFieldError(TextBox input, Label errorLabel, string message)
        {
            input.BackColor = AppTheme.DangerSoft;
            input.ForeColor = AppTheme.TextPrimary;
            errorLabel.Text = "⚠  " + message;
            errorLabel.Visible = true;
        }

        public static void ClearFieldError(TextBox input, Label errorLabel)
        {
            input.BackColor = AppTheme.InputBg;
            input.ForeColor = AppTheme.TextPrimary;
            errorLabel.Text = string.Empty;
            errorLabel.Visible = false;
        }
    }
}