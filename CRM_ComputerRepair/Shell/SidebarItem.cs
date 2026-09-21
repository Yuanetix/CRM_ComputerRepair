using System.Collections.Generic;

namespace CRM.winforms
{
    /// <summary>
    /// Represents one entry in the left sidebar.
    /// </summary>
    public class SidebarItem
    {
        public string Key { get; set; } = string.Empty;      // e.g., "customers"
        public string Title { get; set; } = string.Empty;    // e.g., "Customers"
        public string Icon { get; set; } = string.Empty;     // e.g., "👥"
        public bool IsAvailable { get; set; } = false;       // true = has a real screen

        /// <summary>
        /// The default sidebar layout for Fixory.
        /// Only "Customers" is fully implemented for this exam.
        /// </summary>
        public static List<SidebarItem> GetMenu()
        {
            return new List<SidebarItem>
            {
                new SidebarItem { Key = "dashboard",  Title = "Dashboard",       Icon = "🏠", IsAvailable = false },
                new SidebarItem { Key = "customers",  Title = "Customers",       Icon = "👥", IsAvailable = true  },
                new SidebarItem { Key = "repairs",    Title = "Repair Requests", Icon = "🔧", IsAvailable = false },
                new SidebarItem { Key = "devices",    Title = "Devices",         Icon = "📦", IsAvailable = false },
                new SidebarItem { Key = "parts",      Title = "Parts",           Icon = "🛠", IsAvailable = false },
                new SidebarItem { Key = "suppliers",  Title = "Suppliers",       Icon = "🏢", IsAvailable = false },
                new SidebarItem { Key = "reports",    Title = "Reports",         Icon = "📊", IsAvailable = false },
                new SidebarItem { Key = "loyalty",    Title = "Loyalty",         Icon = "💰", IsAvailable = false },
            };
        }
    }
}