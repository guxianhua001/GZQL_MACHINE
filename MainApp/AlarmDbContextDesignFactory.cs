using Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class AlarmDbContextDesignFactory : IDesignTimeDbContextFactory<AlarmDbContext>
{
    public AlarmDbContext CreateDbContext(string[] args)
    {
        // 直接硬编码测试连接字符串
        var optionsBuilder = new DbContextOptionsBuilder<AlarmDbContext>()
            .UseSqlServer("Server=.;Database=SmartAlarms;User Id=sa;Password=123456;TrustServerCertificate=true;",
                // 关键：显式指定迁移程序集为 MainApp
                b => b.MigrationsAssembly("MainApp"));
        return new AlarmDbContext(optionsBuilder.Options);
    }

    // 必需步骤:
    // 1.在控制台中输入 Add-Migration Init  成功后会生成Migrations文件夹和文件  SmartAlarms
    // 2.在控制台中输入 Update-Database     成功后会生成数据库表结构,可在SQL Server中查看
}
