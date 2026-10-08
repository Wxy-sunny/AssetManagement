using AssetManagementSystem.Models;
using AssetManagementSystem.Utils;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using System.Security.Cryptography;

namespace AssetManagementSystem.Services
{
	/// <summary>
	/// 初始化数据（系统管理员账号、内置角色）
	/// </summary>
	public static class DataSeeder
	{
		/// <summary>
		/// 确保存在一个系统管理员账号。
		/// 说明：账号已存在时不会做任何修改（不覆盖密码），因此可重复执行、安全幂等。
		/// 密码在运行时用 BCrypt 实时生成哈希（带随机盐），不存储明文。
		/// </summary>
		public static void SeedAdmin(ISqlSugarClient db, IConfiguration configuration)
		{
			var enabled = configuration.GetValue<bool>("AdminSeed:Enabled", true);
			if (!enabled) return;

			var userId = configuration["AdminSeed:UserId"] ?? "admin";
			var userName = configuration["AdminSeed:UserName"] ?? "系统管理员";
			var password = configuration["AdminSeed:Password"];
			var roleId = configuration["AdminSeed:RoleId"] ?? "admin";
			var roleName = configuration["AdminSeed:RoleName"] ?? "系统管理员";

			// 未配置密码时，生成随机强密码（仅首次创建时生效），避免提交弱口令
			var generatedPassword = false;
			if (string.IsNullOrWhiteSpace(password))
			{
				password = GenerateRandomPassword(12);
				generatedPassword = true;
			}

			// 1. 内置管理员角色
			var role = db.Queryable<SysRole>().First(r => r.RoleId == roleId);
			if (role == null)
			{
				db.Insertable(new SysRole
				{
					Id = Guid.NewGuid().ToString(),
					RoleId = roleId,
					RoleName = roleName,
					Description = "系统内置管理员角色",
					IsAdmin = true, // 超级管理员：默认拥有全部权限
					IsEnabled = true,
					StatusCode = 1,
					StatusDesc = "启用",
					CreateTime = DateTime.Now
				}).ExecuteCommand();
			}
			else if (role.IsAdmin != true)
			{
				// 角色已存在（如 is_admin 字段上线前创建的历史数据）：
				// 补齐超管标记，否则管理员登录后会被权限校验拦截、看不到任何菜单
				role.IsAdmin = true;
				db.Updateable(role).UpdateColumns(r => new { r.IsAdmin }).ExecuteCommand();
			}

			// 2. 管理员账号：已存在则跳过，避免覆盖已有密码
			var exists = db.Queryable<SysUser>().Any(u => u.UserId == userId);
			if (exists)
			{
				// 已存在时顺带兜底：角色为空会导致登录后没有任何权限
				var admin = db.Queryable<SysUser>().First(u => u.UserId == userId);
				if (admin != null && string.IsNullOrWhiteSpace(admin.RoleId))
				{
					admin.RoleId = roleId;
					admin.RoleName = roleName;
					db.Updateable(admin)
						.UpdateColumns(u => new { u.RoleId, u.RoleName })
						.ExecuteCommand();
				}
				return;
			}

			db.Insertable(new SysUser
			{
				Id = Guid.NewGuid().ToString(),
				UserId = userId,
				UserName = userName,
				NickName = userName,
				PassWord = PasswordHelper.Hash(password), // BCrypt 哈希，不存明文
				RoleId = roleId,
				RoleName = roleName,
				IsEnabled = true,
				StatusCode = 1,
				StatusDesc = "启用",
				CreateTime = DateTime.Now
			}).ExecuteCommand();

			if (generatedPassword)
				Console.WriteLine($"已初始化管理员账号：{userId}，初始密码已随机生成：{password}（请登录后及时修改密码）");
			else
				Console.WriteLine($"已初始化管理员账号：{userId}（如为首次创建，请登录后及时修改密码）");
			}

			/// <summary>
			/// 生成随机强密码（大小写字母 + 数字）
			/// </summary>
			private static string GenerateRandomPassword(int length)
			{
			const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
			var data = new byte[length];
			using var rng = RandomNumberGenerator.Create();
			rng.GetBytes(data);
			return new string(data.Select(b => chars[b % chars.Length]).ToArray());
			}

		/// <summary>
		/// 生成演示数据（角色 / 部门 / 用户 / 资产分类 / 资产 / 通知）
		/// 说明：各表仅在"没有数据"时才生成，可重复执行；生产环境请把 DemoData:Enabled 设为 false
		/// </summary>
		public static void SeedDemoData(ISqlSugarClient db, IConfiguration configuration)
		{
			var enabled = configuration.GetValue<bool>("DemoData:Enabled", false);
			if (!enabled) return;

			SeedDemoRoles(db);
			SeedDemoDepartments(db);
			var userIds = SeedDemoUsers(db);
			var categoryIds = SeedDemoCategories(db);
			SeedDemoAssets(db, categoryIds, userIds);
			SeedDemoNotifications(db);
		}

		// ============ 演示角色 ============
		private static void SeedDemoRoles(ISqlSugarClient db)
		{
			var roles = new (string RoleId, string RoleName, string Desc)[]
			{
				("asset_manager", "资产管理员", "负责资产登记、变更、盘点与折旧管理"),
				("employee", "普通员工", "仅可查看资产与自己名下的领用记录"),
			};

			foreach (var (roleId, roleName, desc) in roles)
			{
				if (db.Queryable<SysRole>().Any(r => r.RoleId == roleId)) continue;

				db.Insertable(new SysRole
				{
					Id = Guid.NewGuid().ToString(),
					RoleId = roleId,
					RoleName = roleName,
					Description = desc,
					IsAdmin = false,
					IsEnabled = true,
					StatusCode = 1,
					StatusDesc = "启用",
					CreateTime = DateTime.Now
				}).ExecuteCommand();
			}
		}

		// ============ 演示部门（总部 → 各职能部门） ============
		private static void SeedDemoDepartments(ISqlSugarClient db)
		{
			if (db.Queryable<Department>().Any()) return;

			var hqId = Guid.NewGuid().ToString();
			var now = DateTime.Now;

			var departments = new List<Department>
			{
				new Department
				{
					Id = hqId, DepartmentId = "HQ", DepartmentName = "总公司",
					ParentId = null, LeaderName = "陈总", Phone = "021-60000001", SortOrder = 1,
					Description = "公司总部", IsEnabled = true, StatusCode = 1, StatusDesc = "启用", CreateTime = now
				},
				new Department
				{
					Id = Guid.NewGuid().ToString(), DepartmentId = "HQ-TECH", DepartmentName = "技术部",
					ParentId = hqId, LeaderName = "张伟", Phone = "021-60000010", SortOrder = 1,
					Description = "负责研发与 IT 设备维护", IsEnabled = true, StatusCode = 1, StatusDesc = "启用", CreateTime = now
				},
				new Department
				{
					Id = Guid.NewGuid().ToString(), DepartmentId = "HQ-FIN", DepartmentName = "财务部",
					ParentId = hqId, LeaderName = "李娜", Phone = "021-60000020", SortOrder = 2,
					Description = "负责资产账务与折旧核算", IsEnabled = true, StatusCode = 1, StatusDesc = "启用", CreateTime = now
				},
				new Department
				{
					Id = Guid.NewGuid().ToString(), DepartmentId = "HQ-HR", DepartmentName = "人事部",
					ParentId = hqId, LeaderName = "王强", Phone = "021-60000030", SortOrder = 3,
					Description = "负责人事与资产领用登记", IsEnabled = true, StatusCode = 1, StatusDesc = "启用", CreateTime = now
				},
				new Department
				{
					Id = Guid.NewGuid().ToString(), DepartmentId = "HQ-MKT", DepartmentName = "市场部",
					ParentId = hqId, LeaderName = "刘洋", Phone = "021-60000040", SortOrder = 4,
					Description = "负责市场推广", IsEnabled = true, StatusCode = 1, StatusDesc = "启用", CreateTime = now
				},
			};

			db.Insertable(departments).ExecuteCommand();
		}

		// ============ 演示用户 ============
		private static Dictionary<string, string> SeedDemoUsers(ISqlSugarClient db)
		{
			var now = DateTime.Now;

			var users = new (string UserId, string Name, string DeptId, string RoleId, string Mobile)[]
			{
				("zhangwei", "张伟", "HQ-TECH", "asset_manager", "13800138001"),
				("lina", "李娜", "HQ-FIN", "asset_manager", "13800138002"),
				("wangqiang", "王强", "HQ-HR", "employee", "13800138003"),
				("liuyang", "刘洋", "HQ-MKT", "employee", "13800138004"),
				("chenjing", "陈静", "HQ-TECH", "employee", "13800138005"),
			};

			// 部门编码 → 主键 Id
			var deptMap = db.Queryable<Department>()
				.ToList()
				.Where(d => !string.IsNullOrWhiteSpace(d.DepartmentId))
				.ToDictionary(d => d.DepartmentId!, d => d.Id);

			// 角色编码 → 角色名
			var roleMap = db.Queryable<SysRole>()
				.ToList()
				.Where(r => !string.IsNullOrWhiteSpace(r.RoleId))
				.ToDictionary(r => r.RoleId!, r => r.RoleName);

			// 返回：部门编码 → 该部门下第一个演示用户的主键 Id（用于资产分摊使用人）
			var result = new Dictionary<string, string>();

			foreach (var (userId, name, deptId, roleId, mobile) in users)
			{
				var exist = db.Queryable<SysUser>().First(u => u.UserId == userId);
				if (exist != null)
				{
					if (deptMap.TryGetValue(deptId, out var existedDeptId))
						result[deptId] = existedDeptId;
					continue;
				}

				var newId = Guid.NewGuid().ToString();
				db.Insertable(new SysUser
				{
					Id = newId,
					UserId = userId,
					UserName = name,
					NickName = name,
					PassWord = PasswordHelper.Hash("123456"), // 演示账号统一密码
					Email = $"{userId}@example.com",
					Mobile = mobile,
					RoleId = roleId,
					RoleName = roleMap.TryGetValue(roleId, out var roleName) ? roleName : null,
					DeptId = deptMap.TryGetValue(deptId, out var deptPk) ? deptPk : null,
					IsEnabled = true,
					StatusCode = 1,
					StatusDesc = "启用",
					CreateTime = now
				}).ExecuteCommand();

				result[deptId] = newId;
			}

			return result;
		}

		// ============ 演示资产分类（两级） ============
		private static Dictionary<string, (string Id, string Name)> SeedDemoCategories(ISqlSugarClient db)
		{
			var map = new Dictionary<string, (string Id, string Name)>();
			if (db.Queryable<AssetCategory>().Any()) return map;

			var now = DateTime.Now;

			// 一级分类
			var roots = new (string Code, string Name, string Desc)[]
			{
				("IT", "IT 设备", "计算机、网络与外围设备"),
				("FUR", "办公家具", "桌椅、柜类等"),
				("OFF", "办公设备", "空调、饮水机等"),
				("VEH", "交通工具", "公司车辆"),
			};

			var rootMap = new Dictionary<string, string>();
			foreach (var (code, name, desc) in roots)
			{
				var id = Guid.NewGuid().ToString();
				rootMap[code] = id;
				map[code] = (id, name);

				db.Insertable(new AssetCategory
				{
					Id = id, ParentId = null, CategoryCode = code, CategoryName = name,
					Description = desc, IsEnabled = true, StatusCode = 1, StatusDesc = "启用", CreateTime = now
				}).ExecuteCommand();
			}

			// 二级分类
			var children = new (string Code, string Name, string Parent, string Desc)[]
			{
				("IT-PC", "计算机", "IT", "笔记本与台式机"),
				("IT-NET", "网络设备", "IT", "交换机、路由器"),
				("IT-PER", "外部设备", "IT", "打印机、投影仪"),
				("FUR-DESK", "桌类", "FUR", "办公桌、会议桌"),
				("FUR-CHAIR", "椅类", "FUR", "办公椅"),
				("OFF-AC", "空调", "OFF", "中央空调与分体空调"),
			};

			foreach (var (code, name, parent, desc) in children)
			{
				var id = Guid.NewGuid().ToString();
				map[code] = (id, name);

				db.Insertable(new AssetCategory
				{
					Id = id,
					ParentId = rootMap.TryGetValue(parent, out var pid) ? pid : null,
					CategoryCode = code, CategoryName = name,
					Description = desc, IsEnabled = true, StatusCode = 1, StatusDesc = "启用", CreateTime = now
				}).ExecuteCommand();
			}

			return map;
		}

		// ============ 演示资产（含折旧计算） ============
		private static void SeedDemoAssets(
			ISqlSugarClient db,
			Dictionary<string, (string Id, string Name)> categoryIds,
			Dictionary<string, string> userIds)
		{
			if (db.Queryable<Asset>().Any()) return;

			// 部门编码 → (主键Id, 部门名)
			var deptMap = db.Queryable<Department>().ToList()
				.Where(d => !string.IsNullOrWhiteSpace(d.DepartmentId))
				.ToDictionary(d => d.DepartmentId!, d => (d.Id, d.DepartmentName));

			// 部门主键 → 该部门下的使用人（用于分摊资产）
			var userMap = db.Queryable<SysUser>().ToList()
				.Where(u => !string.IsNullOrWhiteSpace(u.DeptId))
				.GroupBy(u => u.DeptId!)
				.ToDictionary(g => g.Key, g => (g.First().Id, g.First().UserName));

			// 分类编码、名称、规格、单位、单价、条数、年限(月)、折旧方式、归属部门
			var templates = new (string Cat, string Name, string Spec, string Unit, decimal Price, int Count, int Life, string Method, string Dept, string Supplier)[]
			{
				("IT-PC", "ThinkPad T14 笔记本电脑", "i7/16G/512G", "台", 6800, 6, 36, DepreciationMethods.StraightLine, "HQ-TECH", "联想（北京）"),
				("IT-PC", "Dell OptiPlex 台式机", "i5/8G/256G", "台", 4500, 5, 36, DepreciationMethods.StraightLine, "HQ-FIN", "戴尔（中国）"),
				("IT-PC", "MacBook Pro 14 笔记本", "M3/16G/512G", "台", 12999, 2, 36, DepreciationMethods.SumOfYears, "HQ-MKT", "苹果中国"),
				("IT-NET", "华为 S5720 交换机", "S5720-28X", "台", 3200, 2, 60, DepreciationMethods.StraightLine, "HQ-TECH", "华为技术"),
				("IT-NET", "企业级路由器", "AR2200", "台", 5600, 1, 60, DepreciationMethods.StraightLine, "HQ-TECH", "华为技术"),
				("IT-PER", "HP LaserJet M427 打印机", "M427fdn", "台", 2100, 3, 36, DepreciationMethods.DoubleDeclining, "HQ-HR", "惠普中国"),
				("IT-PER", "爱普生投影仪", "CB-X06", "台", 4800, 2, 36, DepreciationMethods.StraightLine, "HQ-MKT", "爱普生（中国）"),
				("FUR-DESK", "办公桌 1.6m", "1600*800", "张", 1200, 8, 60, DepreciationMethods.StraightLine, "HQ-TECH", "震旦办公家具"),
				("FUR-CHAIR", "人体工学办公椅", "ERG-200", "把", 800, 10, 60, DepreciationMethods.StraightLine, "HQ-TECH", "震旦办公家具"),
				("OFF-AC", "格力空调 3 匹", "KFR-72LW", "台", 6200, 2, 60, DepreciationMethods.StraightLine, "HQ-FIN", "格力电器"),
				("VEH", "别克 GL8 商务车", "ES 陆尊", "辆", 238000, 1, 60, DepreciationMethods.StraightLine, "HQ-MKT", "上汽通用"),
				("FUR", "钢制文件柜", "1800*900*400", "个", 900, 4, 60, DepreciationMethods.StraightLine, "HQ-HR", "震旦办公家具"),
			};

			// 状态分布：多数在用，少量闲置 / 维修
			var statuses = new[]
			{
				AssetStatusCodes.InUse, AssetStatusCodes.InUse, AssetStatusCodes.InUse,
				AssetStatusCodes.InUse, AssetStatusCodes.Idle, AssetStatusCodes.Repair,
			};

			var assets = new List<Asset>();
			var now = DateTime.Now;
			var seq = 0;

			foreach (var t in templates)
			{
				for (var i = 0; i < t.Count; i++)
				{
					seq++;
					deptMap.TryGetValue(t.Dept, out var dept);

					// 采购日期分布在过去 1~3 年，让折旧进度有明显差异
					var purchaseDate = now.AddMonths(-(6 + (seq % 30))).Date;

					var asset = new Asset
					{
						Id = Guid.NewGuid().ToString(),
						AssetCode = $"ZC-{purchaseDate:yyyy}{seq:D4}",
						AssetName = t.Name,
						Spec = t.Spec,
						Unit = t.Unit,
						Quantity = 1,
						Supplier = t.Supplier,
						UnitPrice = t.Price,
						OriginalValue = t.Price,
						PurchaseDate = purchaseDate,
						DeptId = dept.Id,
						DeptName = dept.DepartmentName,
						Location = $"{dept.DepartmentName}办公区",
						AssetStatus = statuses[seq % statuses.Length],
						DepreciationMethod = t.Method,
						UsefulLifeMonths = t.Life,
						SalvageRate = 5,
						DepreciationStartDate = purchaseDate,
						IsEnabled = true,
						StatusCode = 1,
						StatusDesc = "启用",
						CreateTime = purchaseDate
					};

					if (categoryIds.TryGetValue(t.Cat, out var cat))
					{
						asset.CategoryId = cat.Id;
						asset.CategoryName = cat.Name;
					}

					if (!string.IsNullOrWhiteSpace(dept.Id) && userMap.TryGetValue(dept.Id, out var user))
					{
						asset.UseUserId = user.Id;
						asset.UseUserName = user.UserName;
					}

					// 按当前日期计算折旧与净值
					DepreciationService.Calculate(asset);
					assets.Add(asset);
				}
			}

			db.Insertable(assets).ExecuteCommand();
			Console.WriteLine($"[演示数据] 已生成 {assets.Count} 条资产（含折旧计算）");
		}

		// ============ 演示通知 ============
		private static void SeedDemoNotifications(ISqlSugarClient db)
		{
			if (db.Queryable<Notification>().Any()) return;

			var now = DateTime.Now;
			var list = new List<Notification>
			{
				new Notification
				{
					Id = Guid.NewGuid().ToString(),
					Title = "资产管理系统正式上线",
					Content = "各位同事：\n\n公司资产管理系统即日起正式启用，后续资产领用、归还、报修请统一在本系统登记。\n\n原有纸质登记流程同步废止。",
					NoticeType = NoticeTypes.Notice,
					IsPublished = true, PublishTime = now.AddDays(-10), Publisher = "系统管理员",
					IsEnabled = true, StatusCode = 1, StatusDesc = "启用", CreateTime = now.AddDays(-10)
				},
				new Notification
				{
					Id = Guid.NewGuid().ToString(),
					Title = "V1.1 更新：新增折旧与净值统计",
					Content = "本次更新内容：\n1. 资产支持平均年限法、年数总和法、双倍余额递减法三种折旧方式\n2. 首页新增资产总价值、账面净值统计卡片\n3. 支持一键重算全部资产折旧",
					NoticeType = NoticeTypes.Update,
					IsPublished = true, PublishTime = now.AddDays(-3), Publisher = "系统管理员",
					IsEnabled = true, StatusCode = 1, StatusDesc = "启用", CreateTime = now.AddDays(-3)
				},
				new Notification
				{
					Id = Guid.NewGuid().ToString(),
					Title = "关于年底资产盘点的紧急通知",
					Content = "请各部门负责人于本周五前完成本部门资产自查，核对资产状态与存放位置，如有差异请在系统中提交变更申请。",
					NoticeType = NoticeTypes.Urgent,
					IsPublished = true, PublishTime = now.AddDays(-1), Publisher = "系统管理员",
					IsEnabled = true, StatusCode = 1, StatusDesc = "启用", CreateTime = now.AddDays(-1)
				},
				new Notification
				{
					Id = Guid.NewGuid().ToString(),
					Title = "（草稿）固定资产折旧年限调整说明",
					Content = "根据最新财务制度，部分类别资产的折旧年限将进行调整，正式文件待审批后发布。",
					NoticeType = NoticeTypes.Notice,
					IsPublished = false, Publisher = "系统管理员",
					IsEnabled = true, StatusCode = 1, StatusDesc = "启用", CreateTime = now
				},
			};

			db.Insertable(list).ExecuteCommand();
		}
	}
}
