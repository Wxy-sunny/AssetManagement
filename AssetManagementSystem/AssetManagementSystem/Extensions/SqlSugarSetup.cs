using SqlSugar;

namespace AssetManagementSystem.Extensions
{
	public static class SqlSugarSetup
	{
		/// <summary>
		/// AddSqlSugarSetup方法，用来读取配置文件中的连接字符串，并把 SqlSugarClient注入到系统中
		/// </summary>
		/// <param name="services"></param>
		/// <param name="configuration"></param>
		/// <param name="dbName"></param>
		public static void AddSqlsugarSetup(this IServiceCollection services, IConfiguration configuration)
		{
			// 读取数据库类型配置
			var dbType = configuration["SqlSugarConfig:DbType"].ToLower() switch
			{
				"sqlserver" => DbType.SqlServer,
				"mysql" => DbType.MySql,
				"postgresql" => DbType.PostgreSQL,
				"oracle" => DbType.Oracle,
				"sqlite" => DbType.Sqlite,
				_ => DbType.SqlServer
			};
			// 读取所有连接字符串配置
			var connectionConfigs = new List<ConnectionConfig>();
			// 从配置中读取所有 ConnectionStrings
			var connectionStrings = configuration.GetSection("ConnectionStrings").GetChildren();
			foreach (var conn in connectionStrings)
			{
				connectionConfigs.Add(new ConnectionConfig()
				{
					ConfigId = conn.Key,  // 使用配置的 Key 作为 ConfigId（如 "AssetDB"）
					ConnectionString = conn.Value,
					DbType = dbType,
					IsAutoCloseConnection = true
				});
			}
			// 创建 SqlSugarScope（支持多库）
			var sqlSugarClient = new SqlSugarScope(
				connectionConfigs,  // 传入多个连接配置
				db =>
				{
					// 全局 AOP 配置
					// 说明：SQL 日志默认关闭。原实现每条 SQL 都 Console.WriteLine，
					// 高并发下同步控制台 I/O 会成为吞吐瓶颈；现改为可配置 + Serilog Debug（默认级别 Information 下零成本丢弃）。
					var logSql = configuration.GetValue<bool>("SqlSugarConfig:LogSql", false);
					db.Aop.OnLogExecuting = (sql, pars) =>
					{
						if (!logSql) return;
						Serilog.Log.ForContext("Database", db.CurrentConnectionConfig.ConfigId)
							.Debug("SQL: {Sql}", sql);
					};
				});

			// 注入为 ISqlSugarClient（Singleton 全局唯一）
			services.AddSingleton<ISqlSugarClient>(sqlSugarClient);
		}
	}
	
}
