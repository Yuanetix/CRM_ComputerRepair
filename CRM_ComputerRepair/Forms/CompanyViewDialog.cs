using CRM.winforms.Controls;
using CRM.winforms.Forms;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms
{
    [DesignerCategory("Code")]
    public class CompanyViewDialog : ModalForm
    {
        private readonly CompanyDto _company;
        public bool RequestedEdit { get; private set; }

        public CompanyViewDialog(CompanyDto company)
        {
            _company = company;

            BuildCard(
                company.CompanyName,
                subtitle: null,
                width: 720,
                height: 640);

            BuildContent();
        }

        private void BuildContent()
        {
            int x = ContentLeftX;
            int w = ContentWidth;
            int y = ContentTopY;

            // ── Header Banner ──
            var pnlBanner = ModalKit.MakeBanner(pnlBody, x, y, w, 68, UiKit.Wash(AppTheme.Primary));
            pnlBanner.Paint += (s, e) =>
            {
                var g = e.Graphics;
                UiKit.Quality(g);

                // Avatar initials circle
                var avRect = new Rectangle(14, 11, 46, 46);
                UiKit.FillRounded(g, avRect, 23, AppTheme.Primary);
                string initials = _company.CompanyName.Length >= 2
                    ? _company.CompanyName.Substring(0, 2).ToUpperInvariant()
                    : _company.CompanyName.ToUpperInvariant();
                UiKit.Text(g, initials, UiKit.Section, Color.White, avRect, UiKit.Center);

                // Title
                var titleRect = new Rectangle(70, 13, w - 210, 22);
                UiKit.Text(g, _company.CompanyName, UiKit.Section, UiKit.Ink, titleRect, UiKit.Left);

                // Subtitle
                var subRect = new Rectangle(70, 36, w - 210, 18);
                string subText = $"Code: {_company.CompanyCode}  ·  Registered {_company.CreatedAt:MMM d, yyyy}";
                UiKit.Text(g, subText, UiKit.Small, UiKit.InkMuted, subRect, UiKit.Left);

                // Status Pill
                Color pillBg = _company.IsActive ? UiKit.Wash(AppTheme.Success) : UiKit.Wash(AppTheme.Danger);
                Color pillFg = _company.IsActive ? AppTheme.Success : AppTheme.Danger;
                string statusTxt = _company.IsActive ? "ACTIVE" : "DEACTIVATED";
                var pillRect = new Rectangle(w - 120, 20, 106, 26);
                UiKit.FillRounded(g, pillRect, 13, pillBg);
                UiKit.Text(g, statusTxt, UiKit.MicroStrong, pillFg, pillRect, UiKit.Center);
            };
            y += 80;

            int gap = 16;
            int halfW = (w - gap) / 2;

            // ── Card 1: Contact & Database ──
            ModalKit.MakeSection(pnlBody, "CONTACT & LOCATION", x, y, halfW);
            ModalKit.MakeSection(pnlBody, "DATABASE INSTANCE", x + halfW + gap, y, halfW);
            y += 24;

            var pnlContact = CreateInfoCard(x, y, halfW, 175);
            AddInfoRow(pnlContact, "Phone", string.IsNullOrWhiteSpace(_company.ContactPhone) ? "—" : _company.ContactPhone, 12);
            AddInfoRow(pnlContact, "Email", string.IsNullOrWhiteSpace(_company.ContactEmail) ? "—" : _company.ContactEmail, 50);
            AddInfoRow(pnlContact, "Address", _company.FullAddress, 88, valHeight: 52);
            pnlBody.Controls.Add(pnlContact);

            var pnlDb = CreateInfoCard(x + halfW + gap, y, halfW, 175);
            AddInfoRow(pnlDb, "Server", _company.DatabaseServer, 12);
            AddInfoRow(pnlDb, "Database", _company.DatabaseName, 50);
            AddInfoRow(pnlDb, "Status", _company.IsActive ? "Ready" : "Suspended", 88);
            AddInfoRow(pnlDb, "Devices", $"{_company.TotalDevicesCount} registered", 126);
            pnlBody.Controls.Add(pnlDb);
            y += 191;

            // ── Card 2: Subscription & Admin ──
            ModalKit.MakeSection(pnlBody, "SUBSCRIPTION", x, y, halfW);
            ModalKit.MakeSection(pnlBody, "ADMINISTRATOR", x + halfW + gap, y, halfW);
            y += 24;

            var pnlSub = CreateInfoCard(x, y, halfW, 175);
            string planTitle = !string.IsNullOrWhiteSpace(_company.SubscriptionPlanName) ? _company.SubscriptionPlanName : "No Active Plan";
            AddInfoRow(pnlSub, "Plan", planTitle, 12);
            AddInfoRow(pnlSub, "Pricing", _company.SubscriptionPrice.HasValue ? $"₱{_company.SubscriptionPrice:N2} / {_company.SubscriptionDuration ?? "mo"}" : "—", 50);
            AddInfoRow(pnlSub, "Limits", $"{_company.MaxUsers ?? 5} Users / {_company.MaxDevices ?? 100} Units", 88);
            AddInfoRow(pnlSub, "Multi-Branch", _company.EnableMultiBranching ? "Enabled" : "Disabled", 126);
            pnlBody.Controls.Add(pnlSub);

            var pnlAdmin = CreateInfoCard(x + halfW + gap, y, halfW, 175);
            AddInfoRow(pnlAdmin, "Name", !string.IsNullOrWhiteSpace(_company.AdminFullName) ? _company.AdminFullName : "—", 12);
            AddInfoRow(pnlAdmin, "Username", !string.IsNullOrWhiteSpace(_company.AdminUsername) ? _company.AdminUsername : "—", 50);
            AddInfoRow(pnlAdmin, "Email", !string.IsNullOrWhiteSpace(_company.AdminEmail) ? _company.AdminEmail : "—", 88, valHeight: 32);
            AddInfoRow(pnlAdmin, "Users", $"{_company.TotalUsersCount} user(s)", 126);
            pnlBody.Controls.Add(pnlAdmin);
            y += 191;

            // ── Footer Buttons ──
            var btnClose = ModalKit.AddSecondary(pnlCard, "Close");
            btnClose.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            var btnEdit = ModalKit.AddPrimary(pnlCard, "Edit Business");
            btnEdit.Click += (s, e) =>
            {
                RequestedEdit = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            LayoutFooter(btnEdit, btnClose, saveW: 130, cancelW: 90);
            AcceptButton = btnEdit;
            CancelButton = btnClose;
        }

        private static Panel CreateInfoCard(int x, int y, int w, int h)
        {
            var pnl = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = Color.White
            };
            pnl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                UiKit.Quality(g);
                UiKit.FillRounded(g, new Rectangle(0, 0, w - 1, h - 1), 8, Color.White);
                using var pen = new Pen(UiKit.Line, 1f);
                using var path = UiKit.Rounded(new Rectangle(0, 0, w - 1, h - 1), 8);
                g.DrawPath(pen, path);
            };
            return pnl;
        }

        private static void AddInfoRow(Panel card, string label, string value, int y, int valHeight = 20)
        {
            var lblCaption = new Label
            {
                Text = label.ToUpperInvariant(),
                Font = UiKit.MicroStrong,
                ForeColor = UiKit.InkMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, y)
            };
            card.Controls.Add(lblCaption);

            var lblVal = new Label
            {
                Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
                Font = UiKit.Body,
                ForeColor = UiKit.Ink,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(14, y + 15),
                Size = new Size(card.Width - 28, valHeight)
            };
            card.Controls.Add(lblVal);
        }
    }
}
