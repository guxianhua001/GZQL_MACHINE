using Interfaces;
using Microsoft.EntityFrameworkCore;

public static class DbContextConfigurator
{
    public static DbContextOptionsBuilder<AlarmDbContext> ConfigureAlarmDbContext(
        this DbContextOptionsBuilder<AlarmDbContext> builder,
        string connectionString)
    {
        return builder.UseSqlServer(connectionString,
            b => b.MigrationsAssembly("MainApp"))  // 统一迁移程序集
            .EnableDetailedErrors(true)
            .EnableSensitiveDataLogging();
    }
}
