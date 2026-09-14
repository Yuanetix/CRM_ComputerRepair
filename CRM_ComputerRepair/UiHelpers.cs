using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// HCI-aware UI helpers.
    /// </summary>
    public static class UiHelpers
    {
        // ═══════════ BUTTONS ═══════════

        /// <summary>
        /// Primary button — filled with brand color.
        /// </summary>
        public static Button CreatePrimaryButton(string text, Color background)
        {
            var btn = CreateBaseButton(text, background, Color.White);
            return btn;
        }

        /// <summary>
        /// Secondary button — white with colored text and border.
        /// </summary>
        public static Button CreateSecondaryButton(string text, Color border, Color textColor)
        {
            var btn = CreateBaseButton(text, Color.White, textColor);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = border;
            btn.FlatAppearance.MouseOverBackColor = FixoryTheme.Neutral;
            return btn;
        }

        /// <summary>
        /// Legacy helper — safe if you use it elsewhere.
        /// </summary>
        public static Button CreateButton(string text, Color background, Color foreground)
        {
            return CreateBaseButton(text, background, foreground);
        }

        private static Button CreateBaseButton(string text, Color background, Color foreground)
        {
            var btn = new Button
            {
                Text = text,
                Font = FixoryTheme.FontButton,
                BackColor = background,
                ForeColor = foreground,
                FlatStyle = FlatStyle.Flat,
                Height = FixoryTheme.ButtonHeight,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                FlatAppearance = { BorderSize = 0 }
            };

            // Hover / pressed effects (only for filled buttons)
            if (background == FixoryTheme.Primary)
            {
                btn.FlatAppearance.MouseOverBackColor = FixoryTheme.PrimaryHover;
                btn.FlatAppearance.MouseDownBackColor = FixoryTheme.PrimaryLight;
            }
            else if (background == FixoryTheme.Danger)
            {
                btn.FlatAppearance.MouseOverBackColor =
                    Color.FromArgb(220, 60, 60);
            }
            else if (background == FixoryTheme.Success)
            {
                btn.FlatAppearance.MouseOverBackColor =
                    Color.FromArgb(10, 160, 110);
            }

            // Rounded corners
            btn.Resize += (s, e) =>
                ApplyRoundedRegion(btn, FixoryTheme.Radius);

            return btn;
        }

        // ═══════════ TEXT INPUTS ═══════════

        public static TextBox CreateTextBox()
        {
            return new TextBox
            {
                Font = FixoryTheme.FontInput,
                BorderStyle = BorderStyle.FixedSingle,
                Height = FixoryTheme.InputHeight,
                BackColor = FixoryTheme.InputBg,
                ForeColor = FixoryTheme.TextPrimary
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
                Font = FixoryTheme.FontLabel,
                ForeColor = FixoryTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        /// <summary>
        /// Small inline error label — hidden by default.
        /// HCI: error prevention with immediate feedback.
        /// </summary>
        public static Label CreateErrorLabel()
        {
            return new Label
            {
                Text = string.Empty,
                Font = FixoryTheme.FontError,
                ForeColor = FixoryTheme.Danger,
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
                BackColor = FixoryTheme.Surface,
                Padding = new Padding(FixoryTheme.CardPadding)
            };

            panel.Paint += (s, e) =>
            {
                var p = (Panel)s;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using var pen = new Pen(FixoryTheme.Border, 1);
                var rect = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
                DrawRoundedRectangle(e.Graphics, pen, rect, FixoryTheme.Radius);
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

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        // ═══════════ GRID ═══════════

        /// <summary>
        /// HCI: neutral header, subtle stripes, comfortable rows.
        /// </summary>
        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = FixoryTheme.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = FixoryTheme.Border;

            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = FixoryTheme.Neutral;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = FixoryTheme.TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.Font = FixoryTheme.FontLabel;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 8, 10, 8);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = FixoryTheme.Neutral;
            grid.ColumnHeadersHeight = 40;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            grid.DefaultCellStyle.Font = FixoryTheme.FontBody;
            grid.DefaultCellStyle.ForeColor = FixoryTheme.TextPrimary;
            grid.DefaultCellStyle.BackColor = FixoryTheme.Surface;
            grid.DefaultCellStyle.SelectionBackColor = FixoryTheme.PrimarySoft;
            grid.DefaultCellStyle.SelectionForeColor = FixoryTheme.TextPrimary;
            grid.DefaultCellStyle.Padding = new Padding(10, 6, 10, 6);
            grid.RowTemplate.Height = 34;

            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 252);

            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
        }

        // ═══════════ VALIDATION HELPERS ═══════════

        /// <summary>
        /// Show inline error below a field + red border.
        /// </summary>
        public static void ShowFieldError(TextBox input, Label errorLabel, string message)
        {
            input.BackColor = Color.FromArgb(254, 242, 242);   // very light red
            errorLabel.Text = "⚠ " + message;
            errorLabel.Visible = true;
        }

        /// <summary>
        /// Clear inline error for a field.
        /// </summary>
        public static void ClearFieldError(TextBox input, Label errorLabel)
        {
            input.BackColor = FixoryTheme.InputBg;
            errorLabel.Text = string.Empty;
            errorLabel.Visible = false;
        }
    }
}