namespace CRM.winforms
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

        public static bool IsAuthenticated => !string.IsNullOrEmpty(UserId);

        public static void Clear()
        {
            UserId = "";
            Username = "";
            FullName = "";
            Email = "";
            Role = "";
            CompanyId = 1;
        }
    }
}