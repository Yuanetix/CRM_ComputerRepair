namespace CRM.winforms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

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

            Application.Run(new MainForm());
        }
    }
}