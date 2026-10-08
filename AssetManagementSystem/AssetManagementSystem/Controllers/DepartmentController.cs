using AssetManagementSystem.Attributes;
using AssetManagementSystem.Dtos;
using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AssetManagementSystem.Controllers
{
	[ApiController]
	[Route("dept")]
	[Authorize]
	public class DepartmentController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;

		public DepartmentController(DbServer dbServer)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>
		/// 部门树（用于树形表格 / 上级部门选择器）
		/// </summary>
		[HttpGet("tree")]
		[Permission(Permissions.DeptList)]
		public async Task<ApiResult<List<DepartmentDto>>> GetTree([FromQuery] string? keyword = null)
		{
			var list = await _db.Queryable<Department>()
				.WhereIF(!string.IsNullOrWhiteSpace(keyword), d =>
					d.DepartmentId.Contains(keyword) || d.DepartmentName.Contains(keyword))
				.OrderBy(d => d.SortOrder)
				.OrderBy(d => d.DepartmentId)
				.ToListAsync();

			var dtoList = list.Select(MapToDto).ToList();

			var tree = TreeHelper.BuildTree(
				dtoList,
				d => d.id,
				d => d.parentId,
				(parent, children) => parent.children = children,
				rootParentId: null
			);

			return ApiResult<List<DepartmentDto>>.Success(tree, "获取成功");
		}

		/// <summary>
		/// 部门平铺列表（下拉选择器用）
		/// </summary>
		[HttpGet("list")]
		[Permission(Permissions.DeptList)]
		public async Task<ApiResult<List<DepartmentDto>>> GetList([FromQuery] string? keyword = null)
		{
			var list = await _db.Queryable<Department>()
				.WhereIF(!string.IsNullOrWhiteSpace(keyword), d =>
					d.DepartmentId.Contains(keyword) || d.DepartmentName.Contains(keyword))
				.OrderBy(d => d.SortOrder)
				.OrderBy(d => d.DepartmentId)
				.ToListAsync();

			var dtoList = list.Select(MapToDto).ToList();
			return ApiResult<List<DepartmentDto>>.Success(dtoList, "获取成功");
		}

		/// <summary>
		/// 部门详情
		/// </summary>
		[HttpGet("{id}")]
		[Permission(Permissions.DeptList)]
		public async Task<ApiResult<DepartmentDto>> GetById(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<DepartmentDto>.Fail("400", "部门ID不能为空");

			var entity = await _db.Queryable<Department>().FirstAsync(d => d.Id == id);
			if (entity == null)
				return ApiResult<DepartmentDto>.Fail("404", "部门不存在");

			var dto = MapToDto(entity);
			if (!string.IsNullOrWhiteSpace(entity.ParentId))
			{
				var parent = await _db.Queryable<Department>().FirstAsync(d => d.Id == entity.ParentId);
				dto.parentName = parent?.DepartmentName;
			}

			return ApiResult<DepartmentDto>.Success(dto, "获取成功");
		}

		/// <summary>
		/// 新增部门
		/// </summary>
		[HttpPost]
		[Permission(Permissions.DeptManage)]
		public async Task<ApiResult<object>> Add([FromBody] DepartmentDto dto)
		{
			if (dto == null)
				return ApiResult<object>.Fail("400", "参数不能为空");
			if (string.IsNullOrWhiteSpace(dto.departmentId))
				return ApiResult<object>.Fail("400", "部门编码不能为空");
			if (string.IsNullOrWhiteSpace(dto.departmentName))
				return ApiResult<object>.Fail("400", "部门名称不能为空");

			var exist = await _db.Queryable<Department>().AnyAsync(d => d.DepartmentId == dto.departmentId);
			if (exist)
				return ApiResult<object>.Fail("400", "该部门编码已存在");

			var entity = new Department
			{
				Id = Guid.NewGuid().ToString(),
				DepartmentId = dto.departmentId.Trim(),
				DepartmentName = dto.departmentName.Trim(),
				ParentId = string.IsNullOrWhiteSpace(dto.parentId) ? null : dto.parentId,
				LeaderName = dto.leaderName,
				Phone = dto.phone,
				SortOrder = dto.sortOrder ?? 0,
				Description = dto.description,
				IsEnabled = dto.isEnabled ?? true,
				StatusCode = 1,
				StatusDesc = "启用",
				CreateTime = DateTime.Now
			};

			var count = await _db.Insertable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(new { id = entity.Id }, "新增成功")
				: ApiResult<object>.Fail("500", "新增失败");
		}

		/// <summary>
		/// 修改部门（不允许将上级设为自己或其子孙，避免成环）
		/// </summary>
		[HttpPut]
		[Permission(Permissions.DeptManage)]
		public async Task<ApiResult<object>> Update([FromBody] DepartmentDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.id))
				return ApiResult<object>.Fail("400", "部门ID不能为空");
			if (string.IsNullOrWhiteSpace(dto.departmentName))
				return ApiResult<object>.Fail("400", "部门名称不能为空");

			var entity = await _db.Queryable<Department>().FirstAsync(d => d.Id == dto.id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "部门不存在");

			var exist = await _db.Queryable<Department>()
				.AnyAsync(d => d.DepartmentId == dto.departmentId && d.Id != dto.id);
			if (exist)
				return ApiResult<object>.Fail("400", "该部门编码已存在");

			var newParentId = string.IsNullOrWhiteSpace(dto.parentId) ? null : dto.parentId;
			if (!string.IsNullOrWhiteSpace(newParentId))
			{
				if (newParentId == dto.id)
					return ApiResult<object>.Fail("400", "上级部门不能是自己");

				// 校验新上级不能是自己的子孙节点
				var all = await _db.Queryable<Department>().ToListAsync();
				var descendantIds = new List<string>();
				CollectDescendants(all, dto.id, descendantIds);
				if (descendantIds.Contains(newParentId))
					return ApiResult<object>.Fail("400", "上级部门不能是自己的下级部门");
			}

			entity.DepartmentId = dto.departmentId?.Trim() ?? entity.DepartmentId;
			entity.DepartmentName = dto.departmentName.Trim();
			entity.ParentId = newParentId;
			entity.LeaderName = dto.leaderName;
			entity.Phone = dto.phone;
			entity.SortOrder = dto.sortOrder ?? 0;
			entity.Description = dto.description;
			entity.IsEnabled = dto.isEnabled ?? true;
			entity.UpdateTime = DateTime.Now;

			var count = await _db.Updateable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, "修改成功")
				: ApiResult<object>.Fail("500", "修改失败");
		}

		/// <summary>
		/// 删除部门（存在子部门或用户引用时拒绝）
		/// </summary>
		[HttpDelete("{id}")]
		[Permission(Permissions.DeptManage)]
		public async Task<ApiResult<object>> Delete(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "部门ID不能为空");

			var entity = await _db.Queryable<Department>().FirstAsync(d => d.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "部门不存在");

			var hasChildren = await _db.Queryable<Department>().AnyAsync(d => d.ParentId == id);
			if (hasChildren)
				return ApiResult<object>.Fail("400", "该部门包含子部门，不能直接删除");

			var hasUser = await _db.Queryable<SysUser>().AnyAsync(u => u.DeptId == id);
			if (hasUser)
				return ApiResult<object>.Fail("400", "该部门下仍有用户，不能删除");

			var count = await _db.Deleteable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, "删除成功")
				: ApiResult<object>.Fail("500", "删除失败");
		}

		private DepartmentDto MapToDto(Department entity)
		{
			return new DepartmentDto
			{
				id = entity.Id,
				departmentId = entity.DepartmentId,
				departmentName = entity.DepartmentName,
				parentId = entity.ParentId,
				leaderName = entity.LeaderName,
				phone = entity.Phone,
				sortOrder = entity.SortOrder,
				description = entity.Description,
				isEnabled = entity.IsEnabled ?? true,
				createTime = entity.CreateTime,
				children = new List<DepartmentDto>()
			};
		}

		/// <summary>
		/// 递归收集某部门的所有子孙 Id
		/// </summary>
		private void CollectDescendants(List<Department> all, string parentId, List<string> result)
		{
			var children = all.Where(d => d.ParentId == parentId).ToList();
			foreach (var child in children)
			{
				if (result.Contains(child.Id)) continue; // 防御异常数据造成的死循环
				result.Add(child.Id);
				CollectDescendants(all, child.Id, result);
			}
		}
	}
}
