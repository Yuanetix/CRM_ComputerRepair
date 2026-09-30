using CRM.winforms.Auth;
using CRM.winforms.Common;

namespace CRM.winforms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            AppFonts.EnsureLoaded();
            WindowsFontResolver.EnsureRegistered();
            ApplicationConfiguration.Initialize();

            // Login → main shell → (logout) → login → main shell → ...
            // The loop is the app's lifetime, so closing the main form after
            // re-login can no longer terminate the whole application.
            while (true)
            {
                UserSession.LogoutRequested = false;

                // 1. Show login
                using (var login = new LoginForm())
                {
                    if (login.ShowDialog() != DialogResult.OK)
                    {
                        // User closed the login form without signing in
                        return;
                    }
                }

                // 2. Only if authenticated, open the main shell
                if (!UserSession.IsAuthenticated)
                {
                    return;
                }

                // 2b. Initial Terms & Conditions acceptance check for new/tenant companies
                // When a new company logs in using credentials created by the Super Admin,
                // they must initially review and accept the Super Admin's Terms & Conditions before proceeding.
                if (!string.Equals(UserSession.Role, "Super Admin", StringComparison.OrdinalIgnoreCase) &&
                    !UserSession.HasAcceptedTerms)
                {
                    using (var termsDlg = new CRM.winforms.Forms.TermsAgreementDialog())
                    {
                        if (termsDlg.ShowDialog() != DialogResult.OK)
                        {
                            // Declined or closed without accepting -> clear session and return to login screen
                            UserSession.Clear();
                            continue;
                        }
                    }
                }

                // 3. Run the main form; it signals logout via UserSession
                Application.Run(new MainForm());

                // 4. Loop back to login only when the user signed out.
                // (Do NOT check IsAuthenticated here — logout clears the session,
                //  so it is always false at this point.)
                if (!UserSession.LogoutRequested)
                {
                    return;
                }
            }
        }
    }
}