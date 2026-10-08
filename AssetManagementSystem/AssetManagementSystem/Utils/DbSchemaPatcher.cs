using SqlSugar;

namespace AssetManagementSystem.Utils
{
	/// <summary>
	/// 兼容既有库表：为早期版本已创建的表补齐新增列。
	/// SqlSugar 的 CodeFirst 只会创建不存在的表，不会自动 ALTER 已有表，
	/// 因此表结构发生过变更时（如 T_SYS_DEPARTMENT 新增主键与层级字段）需要在此补齐。
	/// </summary>
	public static class DbSchemaPatcher
	{
		public static void Patch(ISqlSugarClient db)
		{
			PatchDepartment(db);
			PatchRole(db);
		}

		/// <summary>
		/// 补齐角色表新增的 is_admin 列（超级管理员标记）
		/// </summary>
		private static void PatchRole(ISqlSugarClient db)
		{
			const string table = "T_SYS_ROLE";
			if (!db.DbMaintenance.IsAnyTable(table)) return;
			if (db.DbMaintenance.IsAnyColumn(table, "is_admin")) return;

			try
			{
				db.Ado.ExecuteCommand($"ALTER TABLE {table} ADD is_admin BIT NULL");
			}
			catch
			{
				// 列已存在或权限不足时忽略
			}
		}

		/// <summary>
		/// 补齐部门表新增列，并为历史数据生成主键值
		/// </summary>
		private static void PatchDepartment(ISqlSugarClient db)
		{
			const string table = "T_SYS_DEPARTMENT";

			// 表不存在时 CodeFirst 已按最新结构创建，无需处理
			if (!db.DbMaintenance.IsAnyTable(table)) return;

			var newColumns = new (string Name, string Definition)[]
			{
				("id", "NVARCHAR(50) NULL"),
				("parent_id", "NVARCHAR(50) NULL"),
				("leader_name", "NVARCHAR(50) NULL"),
				("phone", "NVARCHAR(20) NULL"),
				("sort_order", "INT NULL"),
				("create_user_id", "NVARCHAR(50) NULL"),
				("create_time", "DATETIME NULL"),
				("update_user_id", "NVARCHAR(50) NULL"),
				("update_time", "DATETIME NULL"),
				("is_enabled", "BIT NULL"),
				("status_code", "SMALLINT NULL"),
				("status_desc", "NVARCHAR(255) NULL"),
				("remark", "NVARCHAR(255) NULL"),
			};

			foreach (var (name, definition) in newColumns)
			{
				if (db.DbMaintenance.IsAnyColumn(table, name)) continue;
				try
				{
					db.Ado.ExecuteCommand($"ALTER TABLE {table} ADD {name} {definition}");
				}
				catch
				{
					// 列已存在或权限不足时忽略，不影响启动
				}
			}

			// 历史数据补齐主键值
			try
			{
				db.Ado.ExecuteCommand($"UPDATE {table} SET id = NEWID() WHERE id IS NULL OR id = ''");
			}
			catch
			{
				// 忽略
			}
		}
	}
}
