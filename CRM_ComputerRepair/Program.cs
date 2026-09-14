namespace CRM.winforms
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Launch the main shell with sidebar
            Application.Run(new MainForm());
        }
    }
}