using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using SqlSugar;

namespace AssetManagementSystem.Extensions
{
	/// <summary>
	/// DbServer 扩展方法
	/// </summary>
	public static class DbServerExtensions
	{
		// 简化的属性风格：_db.Asset().Queryable<User>()
		public static ISqlSugarClient Asset(this DbServer db) => db.Use(DbNames.Asset);
		public static ISqlSugarClient Log(this DbServer db) => db.Use(DbNames.Log);
		public static ISqlSugarClient Demo(this DbServer db) => db.Use(DbNames.Demo);
		public static ISqlSugarClient Cache(this DbServer db) => db.Use(DbNames.Cache);
	}
}
