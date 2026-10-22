using AssetManagementSystem.Attributes;
using AssetManagementSystem.Dtos;
using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AssetManagementSystem.Controllers
{
	[ApiController]
	[Route("category")]
	[Authorize]  // 必须登录
	public class CategoryController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;
		private readonly AssetManagementSystem.Services.ICacheService _cache;

		// 缓存键：分类树（无关键字）/ 平铺列表（无关键字）均为稳定读路径
		private const string TreeCacheKey = "ams:category:tree";
		private const string ListCacheKey = "ams:category:list";

		public CategoryController(DbServer dbServer, AssetManagementSystem.Services.ICacheService cache)
		{
			_dbServer = dbServer;
			_cache = cache;
			_db = dbServer.Use(DbNames.Asset);  // 使用 Asset 数据库
		}

		/// <summary>
		/// 获取分类树形结构（用于前端树形表格）
		/// 无关键字走缓存（读多写少），有关键字按原逻辑实时搜索（不缓存）。
		/// </summary>
		[HttpGet("tree")]
		[Permission(Permissions.CategoryList)]
		public async Task<ApiResult<List<CategoryDto>>> GetTree([FromQuery] string? keyword = null)
		{
			if (!string.IsNullOrWhiteSpace(keyword))
			{
				// 搜索场景：跨全部数据，结果不稳定，直接查库不缓存
				var search = await _db.Queryable<AssetCategory>()
					.Where(c => c.CategoryCode.Contains(keyword) || c.CategoryName.Contains(keyword))
					.OrderBy(c => c.CategoryCode)
					.ToListAsync();
				return ApiResult<List<CategoryDto>>.Success(search.Select(MapToDto).ToList(), "获取成功");
			}

			// 稳定读路径：走缓存（内置防穿透/防雪崩/防击穿）
			var tree = await _cache.GetOrSetAsync<List<CategoryDto>>(
				TreeCacheKey,
				async () =>
				{
					var categories = await _db.Queryable<AssetCategory>()
						.Where(c => c.IsEnabled == true)
						.OrderBy(c => c.CategoryCode)
						.ToListAsync();
					var dtoList = categories.Select(MapToDto).ToList();
					return TreeHelper.BuildTree(
						dtoList,
						d => d.id,
						d => d.parentId,
						(parent, children) => parent.children = children,
						rootParentId: null);
				},
				TimeSpan.FromMinutes(10));

			return ApiResult<List<CategoryDto>>.Success(tree, "获取成功");
		}

		/// <summary>
		/// 获取平铺列表（用于下拉选择器，可带查询条件）
		/// 无关键字走缓存；有关键字实时查询。
		/// </summary>
		[HttpGet("list")]
		[Permission(Permissions.CategoryList)]
		public async Task<ApiResult<List<CategoryDto>>> GetList([FromQuery] string? keyword = null)
		{
			if (!string.IsNullOrWhiteSpace(keyword))
			{
				var search = await _db.Queryable<AssetCategory>()
					.Where(c => c.CategoryCode.Contains(keyword) || c.CategoryName.Contains(keyword))
					.OrderBy(c => c.CategoryCode)
					.ToListAsync();
				return ApiResult<List<CategoryDto>>.Success(search.Select(MapToDto).ToList(), "获取成功");
			}

			var list = await _cache.GetOrSetAsync<List<CategoryDto>>(
				ListCacheKey,
				async () =>
				{
					var categories = await _db.Queryable<AssetCategory>()
						.OrderBy(c => c.CategoryCode)
						.ToListAsync();
					return categories.Select(MapToDto).ToList();
				},
				TimeSpan.FromMinutes(10));

			return ApiResult<List<CategoryDto>>.Success(list, "获取成功");
		}

		/// <summary>
		/// 新增分类
		/// </summary>
		[HttpPost]
		[Permission(Permissions.CategoryManage)]
		public async Task<ApiResult<object>> Add([FromBody] CategoryDto dto)
		{
			if (string.IsNullOrWhiteSpace(dto.categoryCode))
				return ApiResult<object>.Fail("400", "分类编码不能为空");

			if (string.IsNullOrWhiteSpace(dto.categoryName))
				return ApiResult<object>.Fail("400", "分类名称不能为空");

			var exist = await _db.Queryable<AssetCategory>()
				.AnyAsync(c => c.ParentId == dto.parentId && c.CategoryCode == dto.categoryCode);
			if (exist)
				return ApiResult<object>.Fail("400", "该分类编码已存在");

			// 检查同一父级下名称是否重复（可选）
			var exist2 = await _db.Queryable<AssetCategory>()
				.AnyAsync(c => c.ParentId == dto.parentId && c.CategoryName == dto.categoryName);
			if (exist2)
				return ApiResult<object>.Fail("400", "该分类名称已存在");

			var entity = new AssetCategory
			{
				Id = Guid.NewGuid().ToString(),
				ParentId = dto.parentId,
				CategoryCode = dto.categoryCode ?? "",
				CategoryName = dto.categoryName,
				Description = dto.description,
				IsEnabled = dto.isEnabled,
				CreateTime = DateTime.Now,
				StatusCode = 1,
				StatusDesc = "启用"
			};
			var count = await _db.Insertable(entity).ExecuteCommandAsync();
			if (count > 0)
			{
				await _cache.RemoveAsync(TreeCacheKey);
				await _cache.RemoveAsync(ListCacheKey);
				return ApiResult<object>.Success(null, "新增成功");
			}
			return ApiResult<object>.Fail("500", "新增失败");
		}

		/// <summary>
		/// 修改分类
		/// </summary>
		[HttpPut]
		[Permission(Permissions.CategoryManage)]
		public async Task<ApiResult<object>> Update([FromBody] CategoryDto dto)
		{
			if (string.IsNullOrEmpty(dto.id))
				return ApiResult<object>.Fail("400", "分类ID不能为空");

			var entity = await _db.Queryable<AssetCategory>().FirstAsync(c => c.Id == dto.id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "分类不存在");

			// 检查名称是否与其他同级节点重复（排除自身）
			var exist = await _db.Queryable<AssetCategory>()
				.AnyAsync(c => c.ParentId == dto.parentId && c.CategoryName == dto.categoryName && c.Id != dto.id);
			if (exist)
				return ApiResult<object>.Fail("400", "该分类名称已存在");

			entity.ParentId = dto.parentId;
			entity.CategoryCode = dto.categoryCode ?? "";
			entity.CategoryName = dto.categoryName;
			entity.Description = dto.description;
			entity.IsEnabled = dto.isEnabled;
			entity.UpdateTime = DateTime.Now;

			var count = await _db.Updateable(entity).ExecuteCommandAsync();
			if (count > 0)
			{
				await _cache.RemoveAsync(TreeCacheKey);
				await _cache.RemoveAsync(ListCacheKey);
				return ApiResult<object>.Success(null, "修改成功");
			}
			return ApiResult<object>.Fail("500", "修改失败");
		}

		/// <summary>
		/// 删除分类（存在子分类时拒绝）
		/// </summary>
		[HttpDelete("{id}")]
		[Permission(Permissions.CategoryManage)]
		public async Task<ApiResult<object>> Delete(string id)
		{
			if (string.IsNullOrEmpty(id))
				return ApiResult<object>.Fail("400", "分类ID不能为空");

			var entity = await _db.Queryable<AssetCategory>().FirstAsync(c => c.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "分类不存在");

			// 检查是否存在子分类
			var hasChildren = await _db.Queryable<AssetCategory>().AnyAsync(c => c.ParentId == id);
			if (hasChildren)
				return ApiResult<object>.Fail("400", "该分类包含子分类，不能直接删除");

			var count = await _db.Deleteable(entity).ExecuteCommandAsync();
			if (count > 0)
			{
				await _cache.RemoveAsync(TreeCacheKey);
				await _cache.RemoveAsync(ListCacheKey);
				return ApiResult<object>.Success(null, "删除成功");
			}
			return ApiResult<object>.Fail("500", "删除失败");
		}

		// 私有映射方法：Entity -> Dto
		private CategoryDto MapToDto(AssetCategory entity)
		{
			return new CategoryDto
			{
				id = entity.Id,
				parentId = entity.ParentId??"",
				categoryCode = entity.CategoryCode ?? "",
				categoryName = entity.CategoryName ?? "",
				description = entity.Description,
				isEnabled = entity.IsEnabled ?? true,
				children = new List<CategoryDto>()
			};
		}
	}
}