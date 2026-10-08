using AssetManagementSystem.Attributes;
using AssetManagementSystem.Utils;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using System;
using System.Configuration;
using System.Reflection;

namespace AssetManagementSystem.Services
{
	/// <summary>
	/// 数据库服务类 - 支持多数据库切换
	/// </summary>
	public class DbServer
	{
		private readonly ISqlSugarClient _db;
		private readonly ITenant _dbTenant;//多租户模式
		private readonly IConfiguration _configuration;

		public DbServer(ISqlSugarClient db,IConfiguration configuration)
		{
			_db = db;
			_dbTenant = db as ITenant ?? throw new InvalidOperationException("当前 SqlSugar 实例不支持多数据库切换");
			_configuration = configuration;
		}

		/// <summary>
		/// 切换到指定数据库并返回操作对象
		/// </summary>
		/// <param name="dbName">数据库名称（对应 DbNames 常量）</param>
		/// <returns>切换后的数据库客户端</returns>
		public ISqlSugarClient Use(string dbName)
		{
			_dbTenant.ChangeDatabase(dbName);
			return _db;
		}

		/// <summary>
		/// 默认数据库（不需要切换）
		/// </summary>
		public ISqlSugarClient Default => _db;


		/// <summary>
		/// 在指定数据库中创建指定实体对应的表
		/// </summary>
		public void CreateTablesInDb(string dbName, params Type[] entityTypes)
		{
			var enabled = _configuration.GetValue<bool>("DatabaseInitialization:Enabled", true);
			var dropFirst = _configuration.GetValue<bool>("DatabaseInitialization:DropTablesFirst", false);

			if (!enabled)
			{
				Console.WriteLine("数据库初始化已禁用");
				return;
			}
			if (entityTypes == null || entityTypes.Length == 0)
			{
				Console.WriteLine($"警告：未指定任何实体类型，数据库 {dbName} 跳过建表");
				return;
			}
			Use(dbName).CodeFirst.InitTables(entityTypes);
			Console.WriteLine($"数据库 {dbName} 成功创建 {entityTypes.Length} 个表");
		}

		/// <summary>
		/// 在指定数据库中创建程序集中所有 SugarTable 实体对应的表
		/// </summary>
		public void CreateTablesInDb(string dbName, Assembly assembly = null)
		{

			var enabled = _configuration.GetValue<bool>("DatabaseInitialization:Enabled", true);
			var dropFirst = _configuration.GetValue<bool>("DatabaseInitialization:DropTablesFirst", false);

			if (!enabled)
			{
				Console.WriteLine("数据库初始化已禁用");
				return;
			}
			assembly ??= Assembly.GetCallingAssembly();

			var entityTypes = assembly.GetTypes()
				.Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
				.Where(t => t.GetCustomAttribute<SugarTable>() != null)
				.ToArray();

			if (entityTypes.Length == 0)
			{
				Console.WriteLine($"在程序集 {assembly.GetName().Name} 中未找到 SugarTable 实体");
				return;
			}

			Use(dbName).CodeFirst.InitTables(entityTypes);
			Console.WriteLine($"数据库 {dbName} 成功创建 {entityTypes.Length} 个表");
		}


		/// <summary>
		/// 根据 DbBelong 特性，按数据库分组创建表（自动分组）
		/// </summary>
		/// <param name="assembly">要扫描的程序集，默认当前程序集</param>
		public void CreateTablesByGroup(Assembly assembly = null)
		{
			var enabled = _configuration.GetValue<bool>("DatabaseInitialization:Enabled", true);
			var dropFirst = _configuration.GetValue<bool>("DatabaseInitialization:DropTablesFirst", false);

			if (!enabled)
			{
				Console.WriteLine("数据库初始化已禁用");
				return;
			}
			assembly ??= Assembly.GetExecutingAssembly();

			// 1. 找出所有带 SugarTable 和 DbBelong 特性的实体
			var entityTypes = assembly.GetTypes()
				.Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
				.Where(t => t.GetCustomAttribute<SugarTable>() != null)
				.ToList();

			if (entityTypes.Count == 0)
			{
				Console.WriteLine("未找到任何 SugarTable 实体");
				return;
			}

			// 2. 按 DbBelong 分组
			var grouped = entityTypes
				.Select(t => new
				{
					Type = t,
					DbBelong = t.GetCustomAttribute<DbBelongAttribute>()?.DbName
				})
				.Where(x => !string.IsNullOrEmpty(x.DbBelong))
				.GroupBy(x => x.DbBelong)
				.ToDictionary(g => g.Key, g => g.Select(x => x.Type).ToList());

			// 3. 检查是否有未指定所属数据库的实体
			var unassigned = entityTypes
				.Where(t => t.GetCustomAttribute<DbBelongAttribute>() == null)
				.ToList();

			if (unassigned.Any())
			{
				Console.WriteLine($"⚠️ 警告：以下实体未指定所属数据库（建议添加 [DbBelong] 特性）：");
				foreach (var type in unassigned)
				{
					Console.WriteLine($"   - {type.FullName}");
				}
			}

			// 4. 按组创建表
			foreach (var group in grouped)
			{
				var dbName = group.Key;
				var types = group.Value.ToArray();

				Console.WriteLine($"📊 正在为数据库 [{dbName}] 创建 {types.Length} 个表...");

				// 切换到对应数据库并建表
				Use(dbName).CodeFirst.InitTables(types);

				Console.WriteLine($"✅ 数据库 [{dbName}] 表创建完成");
			}

			// 5. 如果没有分组，提示
			if (grouped.Count == 0)
			{
				Console.WriteLine("⚠️ 未找到任何带有 [DbBelong] 特性的实体，请检查实体类是否标记了所属数据库");
			}
		}
	}
}