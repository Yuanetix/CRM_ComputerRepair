using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CRM.winforms;

namespace CRM.winforms.Forms
{
    /// <summary>
    /// Shared modal kit — one field/button/error language for every dialog.
    ///
    /// Canonical rhythm (do not re-tune per dialog):
    ///   label Semibold 8.5 / TextSecondary (20px) → field 38px →
    ///   error 8F / Danger (20px, 4px gap) → next label.
    ///   half-column gap 12px. Multiline default 68px.
    ///   footer: Save 120×38 primary right, Cancel 100×38 secondary,
    ///   10px gap, Archive 110×38 danger-outline left,
    ///   footer Y = cardH − ShadowPad − 54 (16px bottom margin).
    ///   combos/dates/numerics: FontInput, Surface, single shared style.
    ///   chips: 28px, Surface + Line + rounded 6; selected = Primary wash.
    ///   section titles: Semibold 9.5 / TextPrimary.
    /// </summary>
    public static class ModalKit
    {
        public const int FieldH = 38;
        public const int MultiH = 68;
        public const int LabelH = 20;
        public const int ErrorH = 20;
        public const int ErrorGap = 4;
        public const int HalfGap = 12;
        public const int BtnH = 38;
        public const int SaveW = 120;
        public const int CancelW = 100;
        public const int ArchiveW = 110;
        public const int BtnGap = 10;
        public const int ChipH = 28;

        public static int FooterY(int cardHeight, int shadowPad) => cardHeight - shadowPad - 54;

        // ── Labels / errors ──

        public static Label MakeLabel(Control parent, string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = AppFonts.Strong(8.5F),
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            parent.Controls.Add(lbl);
            return lbl;
        }

        public static Label MakeErrorLabel(Control parent, int x, int y)
        {
            var lbl = new Label
            {
                Text = "",
                Font = AppFonts.Regular(8F),
                ForeColor = AppTheme.Danger,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Visible = false
            };
            parent.Controls.Add(lbl);
            return lbl;
        }

        public static void ShowError(TextField input, Label err, string msg)
        {
            input.HasError = true;
            err.Text = msg;
            err.Visible = true;
        }

        public static void ClearError(TextField input, Label err)
        {
            input.HasError = false;
            err.Text = "";
            err.Visible = false;
        }

        // ── Fields ──

        public static TextField MakeField(Control parent, int x, int y, int width,
            string placeholder, bool multiline = false, int multiH = MultiH,
            string? prefill = null)
        {
            var tf = new TextField
            {
                PlaceholderText = placeholder,
                Location = new Point(x, y),
                Size = new Size(width, multiline ? multiH : FieldH)
            };
            if (multiline) tf.Multiline = true;
            if (!string.IsNullOrEmpty(prefill)) tf.Text = prefill;
            parent.Controls.Add(tf);
            return tf;
        }

        public static ComboBox MakeCombo(Control parent, int x, int y, int width)
        {
            var cmb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.FontInput,
                Location = new Point(x, y),
                Size = new Size(width, FieldH),
                DropDownWidth = Math.Max(width, 360)
            };
            parent.Controls.Add(cmb);
            return cmb;
        }

        /// <summary>
        /// Automatically measures every item in the dropdown and expands DropDownWidth
        /// so options are NEVER cut off or truncated.
        /// </summary>
        public static void AdjustDropDownWidth(ComboBox cmb)
        {
            if (cmb.Items.Count == 0) return;
            int maxW = cmb.Width;
            using var g = cmb.CreateGraphics();
            foreach (var item in cmb.Items)
            {
                string s = item?.ToString() ?? "";
                if (string.IsNullOrEmpty(s)) continue;
                int itemW = (int)g.MeasureString(s, cmb.Font).Width + 36;
                if (itemW > maxW) maxW = itemW;
            }
            cmb.DropDownWidth = maxW;
        }

        public static DateTimePicker MakeDate(Control parent, int x, int y, int width,
            string customFormat = "")
        {
            var dtp = new DateTimePicker
            {
                Font = AppTheme.FontInput,
                Location = new Point(x, y),
                Size = new Size(width, 30)
            };
            if (!string.IsNullOrEmpty(customFormat))
            {
                dtp.Format = DateTimePickerFormat.Custom;
                dtp.CustomFormat = customFormat;
            }
            parent.Controls.Add(dtp);
            return dtp;
        }

        public static NumericUpDown MakeNumeric(Control parent, int x, int y, int width)
        {
            var num = new NumericUpDown
            {
                Font = AppTheme.FontInput,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(x, y),
                Size = new Size(width, 30)
            };
            parent.Controls.Add(num);
            return num;
        }

        // ── Buttons (single SaasButton system) ──

        public static SaasButton AddPrimary(Control parent, string text)
        {
            var b = new SaasButton(text, SaasButtonVariant.Primary);
            parent.Controls.Add(b);
            return b;
        }

        public static SaasButton AddSecondary(Control parent, string text)
        {
            var b = new SaasButton(text, SaasButtonVariant.Secondary);
            parent.Controls.Add(b);
            return b;
        }

        public static SaasButton AddDangerOutline(Control parent, string text)
        {
            var b = new SaasButton(text, SaasButtonVariant.DangerOutline);
            parent.Controls.Add(b);
            return b;
        }

        /// <summary>Canonical footer: archive left, cancel + save right, 10px gap.</summary>
        public static void LayoutFooter(int cardHeight, int shadowPad, int leftX, int rightX,
            SaasButton save, SaasButton cancel, SaasButton? archive = null,
            int saveW = SaveW, int cancelW = CancelW)
        {
            var modalForm = FindModalForm(save) ?? FindModalForm(cancel);
            if (modalForm != null)
            {
                modalForm.LayoutFooter(save, cancel, archive, saveW, cancelW);
                return;
            }

            int btnY = FooterY(cardHeight, shadowPad);
            save.Size = new Size(saveW, BtnH);
            save.Location = new Point(rightX - saveW, btnY);
            cancel.Size = new Size(cancelW, BtnH);
            cancel.Location = new Point(save.Left - cancelW - BtnGap, btnY);
            if (archive != null)
            {
                archive.Size = new Size(ArchiveW, BtnH);
                archive.Location = new Point(leftX, btnY);
            }
        }

        private static ModalForm? FindModalForm(Control? c)
        {
            while (c != null)
            {
                if (c is ModalForm mf) return mf;
                c = c.Parent;
            }
            return null;
        }

        // ── Sections / banners / chips ──

        public static Label MakeSection(Control parent, string text, int x, int y, int width)
        {
            var lbl = new Label
            {
                Text = text.ToUpperInvariant(),
                Font = UiKit.MicroStrong,
                ForeColor = UiKit.InkMuted,
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            parent.Controls.Add(lbl);
            return lbl;
        }

        public static Panel MakeBanner(Control parent, int x, int y, int width, int height, Color bg)
        {
            var pnl = new Panel
            {
                BackColor = Color.Transparent,
                Location = new Point(x, y),
                Size = new Size(width, height)
            };
            pnl.Paint += (s, e) =>
            {
                UiKit.Quality(e.Graphics);
                UiKit.FillRounded(e.Graphics, new Rectangle(0, 0, pnl.Width, pnl.Height), UiKit.Radius, bg);
            };
            parent.Controls.Add(pnl);
            return pnl;
        }

        public static Label MakeBannerText(Panel banner, string text)
        {
            var lbl = new Label
            {
                Text = text,
                Font = UiKit.Small,
                ForeColor = UiKit.InkMuted,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 10, 12, 10),
                BackColor = Color.Transparent
            };
            banner.Controls.Add(lbl);
            return lbl;
        }

        public static Button MakeChip(Control parent, string text, bool selected, int x, int y, int width, int height = ChipH)
        {
            var b = new Button
            {
                Text = text,
                Font = UiKit.SmallStrong,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Location = new Point(x, y),
                Size = new Size(width, height)
            };
            b.FlatAppearance.BorderSize = 1;
            StyleChip(b, selected);
            b.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(b, 6);
            UiHelpers.ApplyRoundedRegion(b, 6);
            parent.Controls.Add(b);
            return b;
        }

        /// <summary>Auto-size chip for FlowLayoutPanel preset rows.</summary>
        public static Button MakeFlowChip(Control parent, string text)
        {
            var b = new Button
            {
                Text = text,
                Font = UiKit.SmallStrong,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                AutoSize = true,
                Height = ChipH,
                Margin = new Padding(0, 0, 8, 6)
            };
            b.FlatAppearance.BorderSize = 1;
            StyleChip(b, false);
            b.Resize += (s, e) => UiHelpers.ApplyRoundedRegion(b, 6);
            UiHelpers.ApplyRoundedRegion(b, 6);
            parent.Controls.Add(b);
            return b;
        }

        public static void StyleChip(Button b, bool selected)
        {
            if (selected)
            {
                b.BackColor = UiKit.Wash(AppTheme.Primary);
                b.ForeColor = AppTheme.Primary;
                b.FlatAppearance.BorderColor = AppTheme.Primary;
                b.FlatAppearance.MouseOverBackColor = AppTheme.PrimarySoft;
            }
            else
            {
                b.BackColor = UiKit.Surface;
                b.ForeColor = UiKit.Ink;
                b.FlatAppearance.BorderColor = UiKit.Line;
                b.FlatAppearance.MouseOverBackColor = UiKit.Hover;
            }
        }
    }
}
