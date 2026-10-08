using System;

namespace AssetManagementSystem.Attributes
{
	/// <summary>
	/// 标记实体类属于哪个数据库
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public class DbBelongAttribute : Attribute
	{


		/// <summary>
		/// 所属数据库名称（对应 DbNames 常量）
		/// </summary>
		public string DbName { get; }

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="dbName">数据库名称</param>
		public DbBelongAttribute(string dbName)
		{
			DbName = dbName;
		}

	}
}
