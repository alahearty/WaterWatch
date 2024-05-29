using Microsoft.EntityFrameworkCore;
using WatchWaterConsumption;

namespace WatchWaterConsumption.Extensions
{
    internal static class MigrationExtension
    {
        public static WebApplication Migrate(this WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetService<DatabaseContext>();
                dbContext?.Database.Migrate();
            }
            return app;
        }
    }
}
