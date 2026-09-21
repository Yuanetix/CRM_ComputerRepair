using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CRM.winforms.Controls
{
    /// <summary>
    /// Host control for the Customers page.
    /// Just embeds the list view (which opens modals internally).
    /// </summary>
    [DesignerCategory("Code")]
    public class CustomerControl : UserControl
    {
        private CustomerListControl listView = null!;

        public event EventHandler<string>? StatusChanged;

        public CustomerControl()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.Background;
            AutoScaleMode = AutoScaleMode.Font;

            listView = new CustomerListControl { Dock = DockStyle.Fill };
            Controls.Add(listView);

            SetStatus("Customer Data Collection loaded.");
        }

        private void SetStatus(string message)
        {
            StatusChanged?.Invoke(this, message);
        }
    }
}