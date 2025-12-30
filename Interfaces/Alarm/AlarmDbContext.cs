

using Microsoft.EntityFrameworkCore;

namespace Interfaces
{
    public class AlarmDbContext : DbContext
    {
        // 支持多个构造函数重载
        public AlarmDbContext() { }
        public AlarmDbContext(DbContextOptions<AlarmDbContext> options)
            : base(options) { }
        public DbSet<PersistentAlarm> Alarms { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 配置索引提升查询效率

            // 明确指定使用PostgreSQL的扩展方法
            SqlServerIndexBuilderExtensions.IncludeProperties(
                modelBuilder.Entity<PersistentAlarm>()
                    .HasIndex(a => new { a.Timestamp, a.Level }),
                a => new { a.StationId, a.Code }
            );

            // 配置精度（时间戳精确到100纳秒）
            modelBuilder.Entity<PersistentAlarm>()
                .Property(a => a.Timestamp)
                .HasPrecision(4);
            // 配置索引，加速查询
            modelBuilder.Entity<PersistentAlarm>()
                .HasIndex(a => a.Timestamp);
            modelBuilder.Entity<PersistentAlarm>()
                .HasIndex(a => a.StationId);
            modelBuilder.Entity<PersistentAlarm>()
                .HasIndex(a => a.Level);
            modelBuilder.Entity<PersistentAlarm>()
               .HasIndex(a => a.Id);

            // 在DbContext配置中添加
            // 在创建索引时确保性能
            modelBuilder.Entity<PersistentAlarm>()
                .HasIndex(a => new { a.Timestamp, a.Id })
                .IsDescending(false, true) // Timestamp ASC, Id DESC
                .HasFillFactor(90);
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
    }

}
