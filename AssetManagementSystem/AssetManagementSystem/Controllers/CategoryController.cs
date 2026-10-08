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

		public CategoryController(DbServer dbServer)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);  // 使用 Asset 数据库
		}

		/// <summary>
		/// 获取分类树形结构（用于前端树形表格）
		/// </summary>
		[HttpGet("tree")]
		[Permission(Permissions.CategoryList)]
		public async Task<ApiResult<List<CategoryDto>>> GetTree([FromQuery] string? keyword = null)
		{
			var query = _db.Queryable<AssetCategory>();
			// 无关键字时仅显示启用项（与原行为一致）；有关键字时在全部数据中搜索
			if (string.IsNullOrWhiteSpace(keyword))
			{
				query = query.Where(c => c.IsEnabled == true);
			}
			else
			{
				query = query.Where(c =>
					c.CategoryCode.Contains(keyword) ||
					c.CategoryName.Contains(keyword));
			}

			var categories = await query
				.OrderBy(c => c.CategoryCode)
				.ToListAsync();

			// 构建树形 DTO
			var dtoList = categories.Select(MapToDto).ToList();

			var tree = TreeHelper.BuildTree(
				dtoList,
				d => d.id,
				d => d.parentId,
				(parent, children) => parent.children = children,
				rootParentId: null  // 根节点的 ParentId 为 null 或空
			);

			return ApiResult<List<CategoryDto>>.Success(tree, "获取成功");
		}

		/// <summary>
		/// 获取平铺列表（用于下拉选择器，可带查询条件）
		/// </summary>
		[HttpGet("list")]
		[Permission(Permissions.CategoryList)]
		public async Task<ApiResult<List<CategoryDto>>> GetList([FromQuery] string? keyword = null)
		{
			var query = _db.Queryable<AssetCategory>();
			if (!string.IsNullOrWhiteSpace(keyword))
			{
				query = query.Where(c =>
					c.CategoryCode.Contains(keyword) ||
					c.CategoryName.Contains(keyword));
			}
			var categories = await query.OrderBy(c => c.CategoryCode).ToListAsync();
			var dtoList = categories.Select(MapToDto).ToList();
			return ApiResult<List<CategoryDto>>.Success(dtoList, "获取成功");
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
			return count > 0
				? ApiResult<object>.Success(null, "新增成功")
				: ApiResult<object>.Fail("500", "新增失败");
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
			return count > 0
				? ApiResult<object>.Success(null, "修改成功")
				: ApiResult<object>.Fail("500", "修改失败");
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
			return count > 0
				? ApiResult<object>.Success(null, "删除成功")
				: ApiResult<object>.Fail("500", "删除失败");
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