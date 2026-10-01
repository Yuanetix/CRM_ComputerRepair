namespace CRM.winforms.Auth
{
    /// <summary>
    /// Holds the currently logged-in user's info.
    /// Populated by LoginForm after a successful sign-in.
    /// </summary>
    public static class UserSession
    {
        public static string UserId { get; set; } = "";
        public static string Username { get; set; } = "";
        public static string FullName { get; set; } = "";
        public static string Email { get; set; } = "";
        public static string Role { get; set; } = "";
        public static int CompanyId { get; set; } = 1;
        public static string CompanyName { get; set; } = "";
        public static bool HasAcceptedTerms { get; set; }
        public static System.DateTime? TermsAcceptedAt { get; set; }
        public static string Token { get; set; } = "";
        public static System.Collections.Generic.List<string> SubscribedModules { get; set; } = new();

        // ── Branch Scoping ──
        public static int? UserBranchId { get; set; }
        public static string? UserBranchName { get; set; }
        public static int? SelectedBranchId { get; set; }
        public static string SelectedBranchName { get; set; } = "All Branches";
        public static event System.Action? OnBranchScopeChanged;

        public static void SetSelectedBranch(int? branchId, string? branchName = null)
        {
            SelectedBranchId = branchId;
            SelectedBranchName = branchId.HasValue && !string.IsNullOrWhiteSpace(branchName)
                ? branchName
                : (branchId.HasValue ? $"Branch #{branchId}" : "All Branches");
            OnBranchScopeChanged?.Invoke();
        }

        public static bool HasModule(string moduleCode)
        {
            if (string.Equals(Role, "Super Admin", System.StringComparison.OrdinalIgnoreCase))
                return true;
            return SubscribedModules.Exists(m => string.Equals(m, moduleCode, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns a clean user display name. If full name is "Admin User", simplifies to "Admin".
        /// </summary>
        public static string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName))
                    return !string.IsNullOrWhiteSpace(Role) ? Role : (!string.IsNullOrWhiteSpace(Username) ? Username : "User");

                string clean = FullName.Trim();
                if (clean.Equals("Admin User", System.StringComparison.OrdinalIgnoreCase) ||
                    clean.Equals("Administrator User", System.StringComparison.OrdinalIgnoreCase))
                {
                    return "Admin";
                }
                if (clean.Equals("Super Admin User", System.StringComparison.OrdinalIgnoreCase))
                {
                    return "Super Admin";
                }
                if (clean.Equals("Manager User", System.StringComparison.OrdinalIgnoreCase))
                {
                    return "Manager";
                }
                if (clean.Equals("Staff User", System.StringComparison.OrdinalIgnoreCase))
                {
                    return "Staff";
                }
                return clean;
            }
        }

        /// <summary>
        /// Set when the user signs out of the running app, so Program.cs can
        /// reopen the login form instead of exiting the application.
        /// </summary>
        public static bool LogoutRequested { get; set; }

        public static bool IsAuthenticated => !string.IsNullOrEmpty(UserId);

        public static void Clear()
        {
            UserId = "";
            Username = "";
            FullName = "";
            Email = "";
            Role = "";
            CompanyId = 1;
            CompanyName = "";
            HasAcceptedTerms = false;
            TermsAcceptedAt = null;
            Token = "";
            SubscribedModules.Clear();
            UserBranchId = null;
            UserBranchName = null;
            SelectedBranchId = null;
            SelectedBranchName = "All Branches";
            LogoutRequested = false;
        }
    }
}