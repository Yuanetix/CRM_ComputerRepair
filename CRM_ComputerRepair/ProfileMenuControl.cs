using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// Dropdown menu that pops up above the profile footer or below the topbar user chip.
    /// Options: Profile, Settings, Sign out.
    /// </summary>
    [DesignerCategory("Code")]
    public class ProfileMenuControl : Panel
    {
        public event EventHandler<string>? ItemClicked;

        private readonly List<ProfileMenuItem> _items;
        private int _hoverIndex = -1;

        private const int ItemHeight = 40;
        private const int ItemPadding = 8;

        public ProfileMenuControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Width = 200;

            _items = new List<ProfileMenuItem>
            {
                new ProfileMenuItem("profile",  IconFont.Profile,  "Profile"),
                new ProfileMenuItem("settings", IconFont.Settings, "Settings"),
                new ProfileMenuItem("-", "", ""),   // separator
                new ProfileMenuItem("signout",  IconFont.SignOut,  "Sign out", isDanger: true),
            };

            Height = ItemPadding * 2 + ItemHeight * _items.Count;

            MouseMove += OnMouseMoveHandler;
            MouseLeave += (s, e) => { _hoverIndex = -1; Invalidate(); };
            MouseClick += OnMouseClickHandler;
        }

        // ═══════════ SIZE ═══════════

        public int DesiredHeight => ItemPadding * 2 + ItemHeight * _items.Count;

        // ═══════════ PAINT ═══════════

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Shadow
            for (int i = 0; i < 5; i++)
            {
                var shadow = new Rectangle(rect.X - i, rect.Y - i + 2,
                    rect.Width + i * 2, rect.Height + i * 2);
                using var sp = GetRoundedPath(shadow, 10 + i);
                using var sb = new SolidBrush(Color.FromArgb(10 + i * 4, 0, 0, 0));
                g.FillPath(sb, sp);
            }

            // White body
            using (var path = GetRoundedPath(rect, 10))
            using (var brush = new SolidBrush(Color.White))
                g.FillPath(brush, path);

            // Border
            using (var path = GetRoundedPath(rect, 10))
            using (var pen = new Pen(Color.FromArgb(228, 231, 236), 1))
                g.DrawPath(pen, path);

            // Items
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                int y = ItemPadding + i * ItemHeight;

                if (item.IsSeparator)
                {
                    using var sepPen = new Pen(AppTheme.Divider, 1);
                    g.DrawLine(sepPen, ItemPadding + 8, y + ItemHeight / 2,
                        Width - ItemPadding - 8, y + ItemHeight / 2);
                    continue;
                }

                var itemRect = new Rectangle(ItemPadding, y,
                    Width - ItemPadding * 2, ItemHeight);

                // Hover bg
                if (_hoverIndex == i)
                {
                    using var path = GetRoundedPath(itemRect, 6);
                    using var brush = new SolidBrush(AppTheme.Neutral);
                    g.FillPath(brush, path);
                }

                // Icon
                Color iconColor = item.IsDanger ? AppTheme.Danger : AppTheme.TextSecondary;
                using (var iconFont = IconFont.Create(10F))
                using (var brush = new SolidBrush(iconColor))
                {
                    var sf = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Alignment = StringAlignment.Center
                    };
                    g.DrawString(item.Icon, iconFont, brush,
                        new RectangleF(itemRect.X + 8, itemRect.Y, 24, itemRect.Height), sf);
                }

                // Label
                Color textColor = item.IsDanger ? AppTheme.Danger : AppTheme.TextPrimary;
                using (var brush = new SolidBrush(textColor))
                {
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                    g.DrawString(item.Label, AppTheme.FontBody, brush,
                        new RectangleF(itemRect.X + 40, itemRect.Y,
                            itemRect.Width - 44, itemRect.Height), sf);
                }
            }
        }

        // ═══════════ INTERACTION ═══════════

        private void OnMouseMoveHandler(object? sender, MouseEventArgs e)
        {
            int index = (e.Y - ItemPadding) / ItemHeight;
            if (index < 0 || index >= _items.Count) index = -1;
            if (index >= 0 && _items[index].IsSeparator) index = -1;

            if (index != _hoverIndex)
            {
                _hoverIndex = index;
                Invalidate();
            }
        }

        private void OnMouseClickHandler(object? sender, MouseEventArgs e)
        {
            int index = (e.Y - ItemPadding) / ItemHeight;
            if (index < 0 || index >= _items.Count) return;
            if (_items[index].IsSeparator) return;

            ItemClicked?.Invoke(this, _items[index].Key);
        }

        // ═══════════ HELPERS ═══════════

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

        private class ProfileMenuItem
        {
            public string Key { get; }
            public string Icon { get; }
            public string Label { get; }
            public bool IsDanger { get; }
            public bool IsSeparator => Key == "-";

            public ProfileMenuItem(string key, string icon, string label, bool isDanger = false)
            {
                Key = key;
                Icon = icon;
                Label = label;
                IsDanger = isDanger;
            }
        }
    }
}