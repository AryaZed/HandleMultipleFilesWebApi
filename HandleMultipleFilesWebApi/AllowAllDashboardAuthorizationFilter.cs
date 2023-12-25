using Hangfire.Dashboard;

namespace HandleMultipleFilesWebApi
{
    public class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            return true; // Allow all users to access the Dashboard
        }
    }

}
