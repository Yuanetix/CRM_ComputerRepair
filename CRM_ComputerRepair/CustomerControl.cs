using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CRM.winforms
{
    public partial class CustomerControl : UserControl
    {
        private readonly ApiClient _api = new ApiClient();
        private int? _selectedCustomerId = null;
        private List<CustomerDto> _allCustomers = new List<CustomerDto>();

        public CustomerControl()
        {
            InitializeComponent();
            this.Load += CustomerControl_Load;
        }

        // ═══════════ LOAD ═══════════

        private async void CustomerControl_Load(object sender, EventArgs e)
        {
            await LoadCustomersAsync();
        }

        // ═══════════ SEARCH ═══════════

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            var term = txtSearch.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(term))
            {
                dgvCustomers.DataSource = null;
                dgvCustomers.DataSource = _allCustomers;
            }
            else
            {
                var lower = term.ToLowerInvariant();

                var filtered = _allCustomers
                    .Where(c =>
                        (c.FirstName ?? "").ToLowerInvariant().Contains(lower) ||
                        (c.LastName ?? "").ToLowerInvariant().Contains(lower) ||
                        (c.Email ?? "").ToLowerInvariant().Contains(lower) ||
                        (c.Phone ?? "").ToLowerInvariant().Contains(lower) ||
                        (c.Address ?? "").ToLowerInvariant().Contains(lower))
                    .ToList();

                dgvCustomers.DataSource = null;
                dgvCustomers.DataSource = filtered;
            }

            HideInternalColumns();

            lblStatus.ForeColor = FixoryTheme.TextSecondary;
            lblStatus.Text = string.IsNullOrEmpty(term)
                ? $"{_allCustomers.Count} customer(s) loaded."
                : $"{dgvCustomers.Rows.Count} match(es) for \"{term}\".";
        }

        // ═══════════ GRID SELECTION ═══════════

        private void dgvCustomers_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvCustomers.CurrentRow?.DataBoundItem is CustomerDto customer)
            {
                _selectedCustomerId = customer.CustomerId;
                txtFirstName.Text = customer.FirstName;
                txtLastName.Text = customer.LastName;
                txtEmail.Text = customer.Email ?? string.Empty;
                txtPhone.Text = customer.Phone ?? string.Empty;
                txtAddress.Text = customer.Address ?? string.Empty;

                lblStatus.ForeColor = FixoryTheme.TextSecondary;
                lblStatus.Text = $"Selected ID: {customer.CustomerId} — {(customer.IsActive ? "Active" : "Archived")}";
            }
        }

        // ═══════════ BUTTONS ═══════════

        private async void btnSave_Click(object sender, EventArgs e)
        {
            if (!ValidateInputs()) return;

            try
            {
                var customer = new CustomerDto
                {
                    FirstName = txtFirstName.Text.Trim(),
                    LastName = txtLastName.Text.Trim(),
                    Email = txtEmail.Text.Trim(),
                    Phone = txtPhone.Text.Trim(),
                    Address = txtAddress.Text.Trim()
                };

                await _api.CreateCustomerAsync(customer);
                SetStatus("Customer saved successfully.", FixoryTheme.Success);
                ClearForm();
                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                ShowError("Save failed", ex.Message);
            }
        }

        private async void btnUpdate_Click(object sender, EventArgs e)
        {
            if (_selectedCustomerId is null)
            {
                ShowWarning("Please select a customer from the grid first.");
                return;
            }

            if (!ValidateInputs()) return;

            try
            {
                var customer = new CustomerDto
                {
                    CustomerId = _selectedCustomerId.Value,
                    FirstName = txtFirstName.Text.Trim(),
                    LastName = txtLastName.Text.Trim(),
                    Email = txtEmail.Text.Trim(),
                    Phone = txtPhone.Text.Trim(),
                    Address = txtAddress.Text.Trim()
                };

                await _api.UpdateCustomerAsync(_selectedCustomerId.Value, customer);
                SetStatus("Customer updated successfully.", FixoryTheme.Success);
                ClearForm();
                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                ShowError("Update failed", ex.Message);
            }
        }

        private async void btnArchive_Click(object sender, EventArgs e)
        {
            if (_selectedCustomerId is null)
            {
                ShowWarning("Please select a customer to archive.");
                return;
            }

            var confirm = MessageBox.Show(
                $"Archive customer #{_selectedCustomerId}?\n\nThis is a soft delete — data is preserved.",
                "Confirm Archive",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _api.ArchiveCustomerAsync(_selectedCustomerId.Value);
                SetStatus("Customer archived.", FixoryTheme.Warning);
                ClearForm();
                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                ShowError("Archive failed", ex.Message);
            }
        }

        private async void btnRestore_Click(object sender, EventArgs e)
        {
            if (_selectedCustomerId is null)
            {
                ShowWarning("Please select an archived customer to restore.");
                return;
            }

            try
            {
                await _api.RestoreCustomerAsync(_selectedCustomerId.Value);
                SetStatus("Customer restored.", FixoryTheme.Success);
                ClearForm();
                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                ShowError("Restore failed", ex.Message);
            }
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            await LoadCustomersAsync();
            SetStatus("Refreshed.", FixoryTheme.TextSecondary);
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            ClearForm();
            SetStatus("Form cleared.", FixoryTheme.TextSecondary);
        }

        private async void chkShowArchived_CheckedChanged(object sender, EventArgs e)
        {
            await LoadCustomersAsync();
        }

        // ═══════════ HELPERS ═══════════

        private async Task LoadCustomersAsync()
        {
            try
            {
                _allCustomers = await _api.GetCustomersAsync(chkShowArchived.Checked);
                ApplySearchFilter();
            }
            catch (Exception ex)
            {
                ShowError("Failed to load customers", ex.Message);
            }
        }

        private void HideInternalColumns()
        {
            string[] hidden = { "LoyaltyPoints", "CreatedAt", "FullName" };

            foreach (var col in hidden)
            {
                if (dgvCustomers.Columns[col] != null)
                    dgvCustomers.Columns[col].Visible = false;
            }

            if (dgvCustomers.Columns["CustomerId"] != null)
                dgvCustomers.Columns["CustomerId"].HeaderText = "ID";

            if (dgvCustomers.Columns["IsActive"] != null)
                dgvCustomers.Columns["IsActive"].HeaderText = "Active";
        }

        private void ClearForm()
        {
            txtFirstName.Clear();
            txtLastName.Clear();
            txtEmail.Clear();
            txtPhone.Clear();
            txtAddress.Clear();
            _selectedCustomerId = null;
            dgvCustomers.ClearSelection();
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(txtFirstName.Text))
            {
                ShowWarning("First Name is required.");
                txtFirstName.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtLastName.Text))
            {
                ShowWarning("Last Name is required.");
                txtLastName.Focus();
                return false;
            }

            if (!string.IsNullOrWhiteSpace(txtEmail.Text) &&
                !txtEmail.Text.Contains("@"))
            {
                ShowWarning("Email must contain '@'.");
                txtEmail.Focus();
                return false;
            }

            return true;
        }

        private void SetStatus(string message, System.Drawing.Color color)
        {
            lblStatus.ForeColor = color;
            lblStatus.Text = message;
        }

        private void ShowWarning(string message)
        {
            MessageBox.Show(message, "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowError(string title, string message)
        {
            MessageBox.Show($"{message}", title,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}