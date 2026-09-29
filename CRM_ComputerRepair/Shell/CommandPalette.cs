using CRM.winforms.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CRM.winforms
{
    /// <summary>
    /// SaaS command palette (Ctrl+K). HCI: recognition over recall (H6) —
    /// staff pick from a filtered list instead of remembering where each
    /// page lives in a role-filtered sidebar. Fuzzy substring match, full
    /// keyboard operation, Esc always cancels.
    /// </summary>
    [DesignerCategory("Code")]
    public sealed class CommandPalette : ModalForm
    {
        public sealed record Entry(string Key, string Title, string Group, string Glyph, string Keywords);

        private readonly List<Entry> _all;
        private List<Entry> _view;
        private int _selected;
        private string? _picked;

        private TextField _search = null!;
        private ListBox _list = null!;

        private CommandPalette(List<Entry> entries)
        {
            _all = entries;
            _view = new List<Entry>(entries);
            BuildCard("Go to…", subtitle: null, width: 560, height: 460, hasFooter: false);
            pnlBody.AutoScroll = false;

            int x = 20;
            int w = pnlBody.ClientSize.Width - 40;
            int y = 14;

            var lblHint = new Label
            {
                Text = "Type to filter  ·  ↑↓ to move  ·  Enter to open  ·  Esc to close",
                Font = UiKit.Small,
                ForeColor = UiKit.InkMuted,
                Location = new Point(x, y),
                Size = new Size(w, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            pnlBody.Controls.Add(lblHint);
            y += 26;

            _search = new TextField
            {
                PlaceholderText = "Search pages and actions…",
                Location = new Point(x, y),
                Size = new Size(w, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _search.InnerTextBox.TextChanged += (s, e) => ApplyFilter();
            _search.InnerTextBox.KeyDown += Search_KeyDown;
            pnlBody.Controls.Add(_search);
            y += 48;

            _list = new ListBox
            {
                Location = new Point(x, y),
                Size = new Size(w, Math.Max(100, pnlBody.ClientSize.Height - y - 16)),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.None,
                Font = UiKit.Body,
                ForeColor = UiKit.Ink,
                BackColor = Color.White,
                ItemHeight = 44,
                DrawMode = DrawMode.OwnerDrawFixed,
                IntegralHeight = false
            };
            _list.DrawItem += List_DrawItem;
            _list.MouseClick += (s, e) =>
            {
                int i = _list.IndexFromPoint(e.Location);
                if (i >= 0) { _selected = i; Confirm(); }
            };
            _list.MouseMove += (s, e) =>
            {
                int i = _list.IndexFromPoint(e.Location);
                if (i >= 0 && i != _selected) { _selected = i; _list.Invalidate(); }
            };
            pnlBody.Controls.Add(_list);
            RefreshList();
            Shown += (s, e) => _search.Focus();
        }

        private void Search_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { MoveSelection(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { MoveSelection(-1); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { Confirm(); e.Handled = true; }
        }

        private void MoveSelection(int d)
        {
            if (_view.Count == 0) return;
            _selected = Math.Clamp(_selected + d, 0, _view.Count - 1);
            _list.SelectedIndex = _selected;
            _list.Invalidate();
        }

        private void ApplyFilter()
        {
            string q = (_search.Text ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q)) _view = new List<Entry>(_all);
            else
            {
                _view = _all
                    .Select(e => (e, Score(e, q)))
                    .Where(t => t.Item2 < int.MaxValue)
                    .OrderBy(t => t.Item2)
                    .Select(t => t.e)
                    .ToList();
            }
            _selected = 0;
            RefreshList();
        }

        private static int Score(Entry e, string q)
        {
            string hay = (e.Title + " " + e.Group + " " + e.Keywords + " " + e.Key).ToLowerInvariant();
            int idx = hay.IndexOf(q, StringComparison.Ordinal);
            if (idx < 0) return int.MaxValue;
            bool prefix = e.Title.ToLowerInvariant().StartsWith(q);
            return (prefix ? 0 : 1000) + idx;
        }

        private void RefreshList()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var e in _view) _list.Items.Add(e);
            _list.SelectedIndex = _view.Count > 0 ? 0 : -1;
            _selected = _view.Count > 0 ? 0 : -1;
            _list.EndUpdate();
            _list.Invalidate();
        }

        private void List_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _view.Count) return;
            var entry = _view[e.Index];
            bool sel = e.Index == _selected;
            var g = e.Graphics;
            UiKit.Quality(g);
            using (var b = new SolidBrush(sel ? UiKit.Wash(UiKit.Accent) : Color.White))
                g.FillRectangle(b, e.Bounds);
            var iconBox = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 8, 28, 28);
            UiKit.FillRounded(g, iconBox, 8, sel ? Color.White : UiKit.Hover);
            using (var f = UiKit.GlyphFont(11F))
                UiKit.Text(g, entry.Glyph, f, sel ? UiKit.Accent : UiKit.InkMuted, iconBox, UiKit.Center);
            UiKit.Text(g, entry.Title, UiKit.BodyStrong, UiKit.Ink,
                new Rectangle(e.Bounds.X + 44, e.Bounds.Y + 2, e.Bounds.Width - 48, 22), UiKit.Left);
            UiKit.Text(g, entry.Group, UiKit.Small, UiKit.InkMuted,
                new Rectangle(e.Bounds.X + 44, e.Bounds.Y + 22, e.Bounds.Width - 48, 18), UiKit.Left);
            if (sel)
                UiKit.Text(g, "↵", UiKit.SmallStrong, UiKit.Accent,
                    new Rectangle(e.Bounds.Right - 30, e.Bounds.Y, 22, e.Bounds.Height), UiKit.Center);
        }

        private void Confirm()
        {
            if (_selected >= 0 && _selected < _view.Count)
            {
                _picked = _view[_selected].Key;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        public static string? Pick(IWin32Window? owner, List<Entry> entries)
        {
            using var dlg = new CommandPalette(entries);
            Form? f = owner as Form;
            var dr = f != null ? dlg.ShowModal(f) : dlg.ShowDialog();
            return dr == DialogResult.OK ? dlg._picked : null;
        }
    }
}
