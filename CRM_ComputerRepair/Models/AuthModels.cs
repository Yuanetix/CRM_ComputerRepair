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
        public string CompanyName { get; set; } = "";
        public bool HasAcceptedTerms { get; set; }
        public System.DateTime? TermsAcceptedAt { get; set; }
        public string Token { get; set; } = "";
        public System.Collections.Generic.List<string> SubscribedModules { get; set; } = new();
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
    }
}
