using AssetManagementSystem.Attributes;
using AssetManagementSystem.Dtos;
using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AssetManagementSystem.Controllers
{
	[ApiController]
	[Route("asset")]
	[Authorize]
	public class AssetController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;
		private readonly IWebHostEnvironment _env;

		public AssetController(DbServer dbServer, IWebHostEnvironment env)
		{
			_dbServer = dbServer;
			_env = env;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>
		/// 分页查询资产（支持关键字 / 分类 / 状态 / 部门 / 采购日期区间）
		/// </summary>
		[HttpGet("page")]
		[Permission(Permissions.AssetList)]
		public async Task<ApiResult<PageResult<AssetDto>>> GetPage([FromQuery] AssetQueryDto query)
		{
			query ??= new AssetQueryDto();
			if (query.pageIndex < 1) query.pageIndex = 1;
			if (query.pageSize < 1) query.pageSize = 10;

			var total = 0;
			var list = await _db.Queryable<Asset>()
				.WhereIF(!string.IsNullOrWhiteSpace(query.keyword), a =>
					a.AssetCode.Contains(query.keyword) ||
					a.AssetName.Contains(query.keyword) ||
					a.Spec.Contains(query.keyword))
				.WhereIF(!string.IsNullOrWhiteSpace(query.categoryId), a => a.CategoryId == query.categoryId)
				.WhereIF(!string.IsNullOrWhiteSpace(query.assetStatus), a => a.AssetStatus == query.assetStatus)
				.WhereIF(!string.IsNullOrWhiteSpace(query.deptId), a => a.DeptId == query.deptId)
				.WhereIF(query.purchaseDateStart.HasValue, a => a.PurchaseDate >= query.purchaseDateStart)
				.WhereIF(query.purchaseDateEnd.HasValue, a => a.PurchaseDate <= query.purchaseDateEnd)
				.OrderBy(a => a.CreateTime, OrderByType.Desc)
				.ToPageListAsync(query.pageIndex, query.pageSize, total);

			// 列表返回前实时重算折旧，保证净值始终最新
			foreach (var item in list)
			{
				DepreciationService.Calculate(item);
			}

			var dtoList = list.Select(MapToDto).ToList();
			var result = new PageResult<AssetDto>(dtoList, total, query.pageIndex, query.pageSize);
			return ApiResult<PageResult<AssetDto>>.Success(result, "获取成功");
		}

		/// <summary>
		/// 不分页列表（下拉选择器用）
		/// </summary>
		[HttpGet("list")]
		[Permission(Permissions.AssetList)]
		public async Task<ApiResult<List<AssetDto>>> GetList([FromQuery] string? keyword = null)
		{
			var list = await _db.Queryable<Asset>()
				.WhereIF(!string.IsNullOrWhiteSpace(keyword), a =>
					a.AssetCode.Contains(keyword) || a.AssetName.Contains(keyword))
				.OrderBy(a => a.AssetCode)
				.ToListAsync();

			var dtoList = list.Select(MapToDto).ToList();
			return ApiResult<List<AssetDto>>.Success(dtoList, "获取成功");
		}

		/// <summary>
		/// 资产详情
		/// </summary>
		[HttpGet("{id}")]
		[Permission(Permissions.AssetList)]
		public async Task<ApiResult<AssetDto>> GetById(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<AssetDto>.Fail("400", "资产ID不能为空");

			var entity = await _db.Queryable<Asset>().FirstAsync(a => a.Id == id);
			if (entity == null)
				return ApiResult<AssetDto>.Fail("404", "资产不存在");

			DepreciationService.Calculate(entity);
			return ApiResult<AssetDto>.Success(MapToDto(entity), "获取成功");
		}

		private AssetDto MapToDto(Asset entity)
		{
			return new AssetDto
			{
				id = entity.Id,
				assetCode = entity.AssetCode ?? "",
				assetName = entity.AssetName ?? "",
				categoryId = entity.CategoryId,
				categoryName = entity.CategoryName,
				spec = entity.Spec,
				unit = entity.Unit,
				quantity = entity.Quantity,
				supplier = entity.Supplier,
				unitPrice = entity.UnitPrice,
				originalValue = entity.OriginalValue,
				purchaseDate = entity.PurchaseDate,
				deptId = entity.DeptId,
				deptName = entity.DeptName,
				useUserId = entity.UseUserId,
				useUserName = entity.UseUserName,
				location = entity.Location,
				assetStatus = entity.AssetStatus,
				depreciationMethod = entity.DepreciationMethod,
				usefulLifeMonths = entity.UsefulLifeMonths,
				salvageRate = entity.SalvageRate,
				salvageValue = entity.SalvageValue,
				monthlyDepreciation = entity.MonthlyDepreciation,
				depreciationStartDate = entity.DepreciationStartDate,
				usedMonths = entity.UsedMonths,
				accumulatedDepreciation = entity.AccumulatedDepreciation,
				netValue = entity.NetValue,
				isEnabled = entity.IsEnabled ?? true,
				remark = entity.Remark,
				createTime = entity.CreateTime,
				updateTime = entity.UpdateTime
			};
		}

		/// <summary>
		/// 新增资产
		/// </summary>
		[HttpPost]
		[Permission(Permissions.AssetAdd)]
		public async Task<ApiResult<object>> Add([FromBody] AssetDto dto)
		{
			if (dto == null)
				return ApiResult<object>.Fail("400", "参数不能为空");
			if (string.IsNullOrWhiteSpace(dto.assetCode))
				return ApiResult<object>.Fail("400", "资产编码不能为空");
			if (string.IsNullOrWhiteSpace(dto.assetName))
				return ApiResult<object>.Fail("400", "资产名称不能为空");

			var exist = await _db.Queryable<Asset>().AnyAsync(a => a.AssetCode == dto.assetCode);
			if (exist)
				return ApiResult<object>.Fail("400", "该资产编码已存在");

			var entity = new Asset
			{
				Id = Guid.NewGuid().ToString(),
				AssetCode = dto.assetCode.Trim(),
				AssetName = dto.assetName.Trim(),
				CategoryId = dto.categoryId,
				CategoryName = await ResolveCategoryName(dto.categoryId, dto.categoryName),
				Spec = dto.spec,
				Unit = dto.unit,
				Quantity = dto.quantity ?? 1,
				Supplier = dto.supplier,
				UnitPrice = dto.unitPrice,
				PurchaseDate = dto.purchaseDate,
				DeptId = dto.deptId,
				DeptName = await ResolveDeptName(dto.deptId, dto.deptName),
				UseUserId = dto.useUserId,
				UseUserName = dto.useUserName,
				Location = dto.location,
				AssetStatus = string.IsNullOrWhiteSpace(dto.assetStatus) ? AssetStatusCodes.InUse : dto.assetStatus,
				DepreciationMethod = string.IsNullOrWhiteSpace(dto.depreciationMethod)
					? DepreciationMethods.StraightLine
					: dto.depreciationMethod,
				UsefulLifeMonths = dto.usefulLifeMonths ?? 60,
				SalvageRate = dto.salvageRate ?? 5,
				DepreciationStartDate = dto.depreciationStartDate ?? dto.purchaseDate,
				Remark = dto.remark,
				IsEnabled = dto.isEnabled ?? true,
				StatusCode = 1,
				StatusDesc = "启用",
				CreateTime = DateTime.Now
			};

			// 原值未填时按 单价 × 数量 自动计算
			entity.OriginalValue = dto.originalValue ?? (entity.UnitPrice ?? 0) * (entity.Quantity ?? 0);

			DepreciationService.Calculate(entity);

			var count = await _db.Insertable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(new { id = entity.Id }, "新增成功")
				: ApiResult<object>.Fail("500", "新增失败");
		}

		/// <summary>
		/// 修改资产
		/// </summary>
		[HttpPut]
		[Permission(Permissions.AssetEdit)]
		public async Task<ApiResult<object>> Update([FromBody] AssetDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.id))
				return ApiResult<object>.Fail("400", "资产ID不能为空");
			if (string.IsNullOrWhiteSpace(dto.assetCode))
				return ApiResult<object>.Fail("400", "资产编码不能为空");
			if (string.IsNullOrWhiteSpace(dto.assetName))
				return ApiResult<object>.Fail("400", "资产名称不能为空");

			var entity = await _db.Queryable<Asset>().FirstAsync(a => a.Id == dto.id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "资产不存在");

			var exist = await _db.Queryable<Asset>()
				.AnyAsync(a => a.AssetCode == dto.assetCode && a.Id != dto.id);
			if (exist)
				return ApiResult<object>.Fail("400", "该资产编码已存在");

			entity.AssetCode = dto.assetCode.Trim();
			entity.AssetName = dto.assetName.Trim();
			entity.CategoryId = dto.categoryId;
			entity.CategoryName = await ResolveCategoryName(dto.categoryId, dto.categoryName);
			entity.Spec = dto.spec;
			entity.Unit = dto.unit;
			entity.Quantity = dto.quantity ?? 1;
			entity.Supplier = dto.supplier;
			entity.UnitPrice = dto.unitPrice;
			entity.OriginalValue = dto.originalValue ?? (dto.unitPrice ?? 0) * (dto.quantity ?? 0);
			entity.PurchaseDate = dto.purchaseDate;
			entity.DeptId = dto.deptId;
			entity.DeptName = await ResolveDeptName(dto.deptId, dto.deptName);
			entity.UseUserId = dto.useUserId;
			entity.UseUserName = dto.useUserName;
			entity.Location = dto.location;
			entity.AssetStatus = string.IsNullOrWhiteSpace(dto.assetStatus) ? AssetStatusCodes.InUse : dto.assetStatus;
			entity.DepreciationMethod = string.IsNullOrWhiteSpace(dto.depreciationMethod)
				? DepreciationMethods.StraightLine
				: dto.depreciationMethod;
			entity.UsefulLifeMonths = dto.usefulLifeMonths ?? 60;
			entity.SalvageRate = dto.salvageRate ?? 5;
			entity.DepreciationStartDate = dto.depreciationStartDate ?? dto.purchaseDate;
			entity.Remark = dto.remark;
			entity.IsEnabled = dto.isEnabled ?? true;
			entity.UpdateTime = DateTime.Now;

			DepreciationService.Calculate(entity);

			var count = await _db.Updateable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, "修改成功")
				: ApiResult<object>.Fail("500", "修改失败");
		}

		/// <summary>
		/// 删除资产（同时清理其关联附件记录与物理文件）
		/// </summary>
		[HttpDelete("{id}")]
		[Permission(Permissions.AssetDelete)]
		public async Task<ApiResult<object>> Delete(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "资产ID不能为空");

			var entity = await _db.Queryable<Asset>().FirstAsync(a => a.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "资产不存在");

			await RemoveFilesByAssetId(id);

			var count = await _db.Deleteable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, "删除成功")
				: ApiResult<object>.Fail("500", "删除失败");
		}

		/// <summary>
		/// 批量删除资产
		/// </summary>
		[HttpPost("batch-delete")]
		[Permission(Permissions.AssetDelete)]
		public async Task<ApiResult<object>> BatchDelete([FromBody] List<string> ids)
		{
			if (ids == null || ids.Count == 0)
				return ApiResult<object>.Fail("400", "请选择要删除的资产");

			foreach (var id in ids)
			{
				await RemoveFilesByAssetId(id);
			}

			var count = await _db.Deleteable<Asset>().In(a => a.Id, ids).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, $"已删除 {count} 条")
				: ApiResult<object>.Fail("500", "删除失败");
		}

		/// <summary>
		/// 变更资产状态（在用 / 闲置 / 维修中 / 已报废）
		/// </summary>
		[HttpPut("{id}/status")]
		[Permission(Permissions.AssetEdit)]
		public async Task<ApiResult<object>> UpdateStatus(string id, [FromBody] StatusUpdateDto dto)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "资产ID不能为空");
			if (dto == null || string.IsNullOrWhiteSpace(dto.status))
				return ApiResult<object>.Fail("400", "状态不能为空");

			var entity = await _db.Queryable<Asset>().FirstAsync(a => a.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "资产不存在");

			entity.AssetStatus = dto.status;
			entity.UpdateTime = DateTime.Now;

			var count = await _db.Updateable(entity)
				.UpdateColumns(a => new { a.AssetStatus, a.UpdateTime })
				.ExecuteCommandAsync();

			return count > 0
				? ApiResult<object>.Success(null, "状态更新成功")
				: ApiResult<object>.Fail("500", "状态更新失败");
		}

		/// <summary>
		/// 重新计算全部资产的折旧并落库
		/// </summary>
		[HttpPost("recalculate")]
		[Permission(Permissions.AssetEdit)]
		public async Task<ApiResult<object>> Recalculate()
		{
			var list = await _db.Queryable<Asset>().ToListAsync();
			if (list.Count == 0)
				return ApiResult<object>.Success(null, "暂无资产需要计算");

			foreach (var item in list)
			{
				DepreciationService.Calculate(item);
			}

			var count = await _db.Updateable(list)
				.UpdateColumns(a => new
				{
					a.SalvageValue,
					a.MonthlyDepreciation,
					a.UsedMonths,
					a.AccumulatedDepreciation,
					a.NetValue
				})
				.ExecuteCommandAsync();

			return ApiResult<object>.Success(null, $"已重算 {count} 条资产折旧");
		}

		/// <summary>
		/// 解析分类名称：优先用传入值，否则按 categoryId 反查
		/// </summary>
		private async Task<string?> ResolveCategoryName(string? categoryId, string? fallback)
		{
			if (!string.IsNullOrWhiteSpace(fallback)) return fallback;
			if (string.IsNullOrWhiteSpace(categoryId)) return null;

			var category = await _db.Queryable<AssetCategory>().FirstAsync(c => c.Id == categoryId);
			return category?.CategoryName;
		}

		/// <summary>
		/// 解析部门名称：优先用传入值，否则按 deptId 反查
		/// </summary>
		private async Task<string?> ResolveDeptName(string? deptId, string? fallback)
		{
			if (!string.IsNullOrWhiteSpace(fallback)) return fallback;
			if (string.IsNullOrWhiteSpace(deptId)) return null;

			// 关联统一使用部门主键 Id（与 Department.ParentId 一致）
			var dept = await _db.Queryable<Department>().FirstAsync(d => d.Id == deptId);
			return dept?.DepartmentName;
		}

		/// <summary>
		/// 删除某资产关联的所有附件记录及其物理文件
		/// </summary>
		private async Task RemoveFilesByAssetId(string assetId)
		{
			var files = await _db.Queryable<AssetFile>().Where(f => f.AssetId == assetId).ToListAsync();
			foreach (var file in files)
			{
				if (!string.IsNullOrWhiteSpace(file.PhysicalPath) && System.IO.File.Exists(file.PhysicalPath))
				{
					try { System.IO.File.Delete(file.PhysicalPath); } catch { /* 忽略文件删除异常 */ }
				}
			}
			if (files.Count > 0)
			{
				await _db.Deleteable<AssetFile>().In(f => f.Id, files.Select(f => f.Id).ToList()).ExecuteCommandAsync();
			}
		}
	}
}
