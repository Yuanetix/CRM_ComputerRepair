namespace CRM.winforms
{
    public class LoginResponseDto
    {
        public string UserId { get; set; } = "";
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public string Email { get; set; } = "";
        public int CompanyId { get; set; }
        public string Token { get; set; } = "";
    }
}
