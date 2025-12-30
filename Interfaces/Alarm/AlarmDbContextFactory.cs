using Microsoft.EntityFrameworkCore;

namespace Interfaces
{
    // Interfaces项目
    public class AlarmDbContextFactory : IDbContextFactory<AlarmDbContext>
    {
        // 改用具体的 Options 类型
        private readonly DbContextOptions<AlarmDbContext> _options;
        public AlarmDbContextFactory(DbContextOptions<AlarmDbContext> options)
        {
            _options = options;
        }
        public AlarmDbContext CreateDbContext()
        {
            return new AlarmDbContext(_options);
        }

        public static AlarmDbContext Create(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AlarmDbContext>()
                .UseSqlServer(connectionString,
                    b => b.MigrationsAssembly("MainApp"));  // [!] 同步配置
            return new AlarmDbContext(optionsBuilder.Options);
        }


    }

}
