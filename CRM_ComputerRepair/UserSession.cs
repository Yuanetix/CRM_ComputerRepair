namespace CRM.winforms
{
    /// <summary>
    /// Holds the currently logged-in user's info.
    /// For this exam demo, the role is hardcoded to Staff.
    /// </summary>
    public static class UserSession
    {
        // Staff user for the exam demo
        public static string UserId { get; set; } = "staff-001";
        public static string FullName { get; set; } = "Juan Dela Cruz";
        public static string Role { get; set; } = "Staff";

        // Company the user belongs to (for tenant filtering)
        public static int CompanyId { get; set; } = 1;
    }
}