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
					db.Aop.OnLogExecuting = (sql, pars) =>
					{
						// 如果有日志框架，可以替换为：logger.LogInformation(sql);
						Console.WriteLine($"SQL: {sql}");
						// 这里可以记录当前使用的 ConfigId
						var configId = db.CurrentConnectionConfig.ConfigId;
						Console.WriteLine($"Database: {configId}");
					};
				});

			// 注入为 ISqlSugarClient（Singleton 全局唯一）
			services.AddSingleton<ISqlSugarClient>(sqlSugarClient);
		}
	}
	
}
