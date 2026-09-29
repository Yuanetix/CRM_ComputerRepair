using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// SaaS placeholder — teaches instead of dead-ending. Every unfinished
    /// page shows what it will do, why it matters, and a way back.
    /// </summary>
    [DesignerCategory("Code")]
    public partial class PlaceholderControl : UserControl
    {
        private readonly SaasEmptyState _empty;

        public PlaceholderControl(string title = "Coming Soon")
        {
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            Dock = DockStyle.Fill;

            _empty = new SaasEmptyState
            {
                Icon = SidebarControl.IconFor(Slug(title)),
                Text = title,
                Subtitle = "This section is on the roadmap. Your data is safe — this page will light up in a coming release.",
                Dock = DockStyle.Fill
            };
            Controls.Add(_empty);
        }

        private static string Slug(string title)
        {
            string t = title.ToLowerInvariant();
            if (t.Contains("setting")) return "terms";
            if (t.Contains("report")) return "reports";
            if (t.Contains("monitor")) return "system-monitor";
            if (t.Contains("admin")) return "admin-accounts";
            if (t.Contains("user")) return "user-accounts";
            if (t.Contains("loyal")) return "loyalty";
            if (t.Contains("subscri")) return "subscriptions";
            return "dashboard";
        }
    }
}
