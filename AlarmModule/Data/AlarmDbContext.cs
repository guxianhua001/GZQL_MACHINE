using AlarmModule.Models;
using Microsoft.EntityFrameworkCore;

namespace AlarmModule.Data
{
    /// <summary>
    /// 报警系统数据库上下文：使用SQLite本地文件数据库
    /// 数据库文件路径：Config/alarms.db
    /// </summary>
    public class AlarmDbContext : DbContext
    {
        public DbSet<AlarmRecord> AlarmRecords { get; set; }
        public DbSet<AlarmThresholdConfig> AlarmThresholdConfigs { get; set; }

        // 必须通过带参数的构造函数创建，由DI工厂预配置DbContextOptions
        public AlarmDbContext(DbContextOptions<AlarmDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlarmRecord>(entity =>
            {
                entity.HasIndex(a => a.AlarmTime);
                entity.HasIndex(a => a.AlarmLevel);
                entity.HasIndex(a => a.AlarmCode);
                entity.HasIndex(a => a.AlarmSource);
                entity.HasIndex(a => a.Status);
                entity.HasIndex(a => new { a.AlarmCode, a.AlarmSource });

                // 枚举属性存储为整数，避免SQLite索引创建时的IComparable异常
                entity.Property(a => a.AlarmLevel).HasConversion<int>();
                entity.Property(a => a.AlarmType).HasConversion<int>();
                entity.Property(a => a.Status).HasConversion<int>();
            });

            modelBuilder.Entity<AlarmThresholdConfig>(entity =>
            {
                entity.HasIndex(t => t.AlarmCode);
                entity.HasIndex(t => new { t.AlarmCode, t.AlarmSource }).IsUnique();

                entity.Property(t => t.AlarmLevel).HasConversion<int>();
                entity.Property(t => t.AlarmType).HasConversion<int>();
            });
        }
    }
}
