using CRM.winforms.Auth;

namespace CRM.winforms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
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