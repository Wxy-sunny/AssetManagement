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
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AssetManagementSystem.Controllers
{
	/// <summary>
	/// 资产流转单据（领用 / 归还 / 报修 / 维修完成 / 报废）
	/// </summary>
	[ApiController]
	[Route("asset-flow")]
	[Authorize]
	public class AssetFlowController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;

		public AssetFlowController(DbServer dbServer)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>
		/// 分页查询流转单据
		/// </summary>
		[HttpGet("page")]
		[Permission(Permissions.FlowList)]
		public async Task<ApiResult<PageResult<AssetFlowDto>>> GetPage([FromQuery] AssetFlowQueryDto query)
		{
			query ??= new AssetFlowQueryDto();
			(query.pageIndex, query.pageSize) = PageGuard.Normalize(query.pageIndex, query.pageSize);

			var total = 0;
			var totalRef = new RefAsync<int>(0);
			var list = await _db.Queryable<AssetFlow>()
				.WhereIF(!string.IsNullOrWhiteSpace(query.keyword), f =>
					f.FlowNo.Contains(query.keyword) ||
					(f.AssetCode != null && f.AssetCode.Contains(query.keyword)) ||
					(f.AssetName != null && f.AssetName.Contains(query.keyword)) ||
					(f.UseUserName != null && f.UseUserName.Contains(query.keyword)))
				.WhereIF(!string.IsNullOrWhiteSpace(query.flowType), f => f.FlowType == query.flowType)
				.WhereIF(!string.IsNullOrWhiteSpace(query.flowStatus), f => f.FlowStatus == query.flowStatus)
				.WhereIF(!string.IsNullOrWhiteSpace(query.assetId), f => f.AssetId == query.assetId)
				.WhereIF(!string.IsNullOrWhiteSpace(query.deptId), f => f.DeptId == query.deptId)
				.WhereIF(query.startDate.HasValue, f => f.CreateTime >= query.startDate)
				.WhereIF(query.endDate.HasValue, f => f.CreateTime <= query.endDate)
				.OrderBy(f => f.CreateTime, OrderByType.Desc)
				.ToPageListAsync(query.pageIndex, query.pageSize, totalRef);
			total = totalRef.Value;

			var dtoList = list.Select(MapToDto).ToList();
			var result = new PageResult<AssetFlowDto>(dtoList, total, query.pageIndex, query.pageSize);
			return ApiResult<PageResult<AssetFlowDto>>.Success(result, "获取成功");
		}

		/// <summary>
		/// 单据详情
		/// </summary>
		[HttpGet("{id}")]
		[Permission(Permissions.FlowList)]
		public async Task<ApiResult<AssetFlowDto>> GetById(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<AssetFlowDto>.Fail("400", "单据ID不能为空");

			var flow = await _db.Queryable<AssetFlow>().FirstAsync(f => f.Id == id);
			if (flow == null)
				return ApiResult<AssetFlowDto>.Fail("404", "单据不存在");

			return ApiResult<AssetFlowDto>.Success(MapToDto(flow), "获取成功");
		}

		/// <summary>
		/// 查询某资产当前可执行的流转动作（前端操作列下拉）
		/// </summary>
		[HttpGet("allowed-actions")]
		[Permission(Permissions.FlowList)]
		public async Task<ApiResult<List<AssetFlowActionDto>>> GetAllowedActions([FromQuery] string assetId)
		{
			if (string.IsNullOrWhiteSpace(assetId))
				return ApiResult<List<AssetFlowActionDto>>.Fail("400", "资产ID不能为空");

			var asset = await _db.Queryable<Asset>().FirstAsync(a => a.Id == assetId);
			if (asset == null)
				return ApiResult<List<AssetFlowActionDto>>.Fail("404", "资产不存在");

			var actions = BuildActions(asset.AssetStatus);
			return ApiResult<List<AssetFlowActionDto>>.Success(actions, "获取成功");
		}
/// <summary>
		/// 办理流转单据（领用 / 归还 / 报修 / 报废）
		/// </summary>
		[HttpPost]
		[Permission(Permissions.FlowManage)]
		public async Task<ApiResult<object>> Create([FromBody] AssetFlowCreateDto dto)
		{
			var result = await AssetFlowService.CreateAsync(_db, dto, CurrentUserId(), CurrentUserName());
			if (!result.Success)
				return ApiResult<object>.Fail("400", result.Message);

			return ApiResult<object>.Success(
				result.Flow == null ? null : MapToDto(result.Flow),
				result.Message);
		}

		/// <summary>
		/// 完成单据（维修完成：恢复在用 / 闲置）
		/// </summary>
		[HttpPost("{id}/complete")]
		[Permission(Permissions.FlowManage)]
		public async Task<ApiResult<object>> Complete(string id, [FromBody] AssetFlowCompleteDto dto)
		{
			var result = await AssetFlowService.CompleteAsync(_db, id, dto, CurrentUserId(), CurrentUserName());
			if (!result.Success)
				return ApiResult<object>.Fail("400", result.Message);

			return ApiResult<object>.Success(
				result.Flow == null ? null : MapToDto(result.Flow),
				result.Message);
		}

		/// <summary>
		/// 取消单据（资产状态回退）
		/// </summary>
		[HttpPost("{id}/cancel")]
		[Permission(Permissions.FlowManage)]
		public async Task<ApiResult<object>> Cancel(string id, [FromBody] AssetFlowCompleteDto? dto)
		{
			var result = await AssetFlowService.CloseAsync(
				_db, id, false, dto?.handleResult, CurrentUserId(), CurrentUserName());
			if (!result.Success)
				return ApiResult<object>.Fail("400", result.Message);

			return ApiResult<object>.Success(null, result.Message);
		}

		/// <summary>
		/// 驳回单据（资产状态回退）
		/// </summary>
		[HttpPost("{id}/reject")]
		[Permission(Permissions.FlowManage)]
		public async Task<ApiResult<object>> Reject(string id, [FromBody] AssetFlowCompleteDto? dto)
		{
			var result = await AssetFlowService.CloseAsync(
				_db, id, true, dto?.handleResult, CurrentUserId(), CurrentUserName());
			if (!result.Success)
				return ApiResult<object>.Fail("400", result.Message);

			return ApiResult<object>.Success(null, result.Message);
		}

		/// <summary>
		/// 根据资产当前状态生成可执行动作列表
		/// </summary>
		private static List<AssetFlowActionDto> BuildActions(string? status)
		{
			var actions = new List<AssetFlowActionDto>();

			foreach (var flowType in AssetStatusMachine.GetAllowedFlowTypes(status))
			{
				var action = flowType switch
				{
					AssetFlowTypes.Borrow => new AssetFlowActionDto
					{
						flowType = AssetFlowTypes.Borrow,
						label = "领用",
						toStatus = AssetStatusCodes.InUse,
						description = "将该资产从闲置池领用并登记使用人"
					},
					AssetFlowTypes.Return => new AssetFlowActionDto
					{
						flowType = AssetFlowTypes.Return,
						label = "归还",
						toStatus = AssetStatusCodes.Idle,
						description = "归还资产，资产回到闲置池"
					},
					AssetFlowTypes.Repair => new AssetFlowActionDto
					{
						flowType = AssetFlowTypes.Repair,
						label = "报修",
						toStatus = AssetStatusCodes.Repair,
						description = "提交报修单，资产转入维修中"
					},
					AssetFlowTypes.Scrap => new AssetFlowActionDto
					{
						flowType = AssetFlowTypes.Scrap,
						label = "报废",
						toStatus = AssetStatusCodes.Scrapped,
						description = "提交报废单，资产进入终态（不可再流转）"
					},
					_ => null
				};

				if (action != null) actions.Add(action);
			}

			return actions;
		}
private static AssetFlowDto MapToDto(AssetFlow entity)
		{
			return new AssetFlowDto
			{
				id = entity.Id,
				flowNo = entity.FlowNo ?? "",
				flowType = entity.FlowType ?? "",
				flowTypeName = AssetStatusMachine.GetFlowTypeName(entity.FlowType),
				flowStatus = entity.FlowStatus ?? "",
				flowStatusName = AssetStatusMachine.GetFlowStatusName(entity.FlowStatus),
				assetId = entity.AssetId ?? "",
				assetCode = entity.AssetCode,
				assetName = entity.AssetName,
				fromStatus = entity.FromStatus,
				fromStatusName = AssetStatusMachine.GetStatusName(entity.FromStatus),
				toStatus = entity.ToStatus,
				toStatusName = AssetStatusMachine.GetStatusName(entity.ToStatus),
				deptId = entity.DeptId,
				deptName = entity.DeptName,
				useUserId = entity.UseUserId,
				useUserName = entity.UseUserName,
				applyUserId = entity.ApplyUserId,
				applyUserName = entity.ApplyUserName,
				handleUserId = entity.HandleUserId,
				handleUserName = entity.HandleUserName,
				handleTime = entity.HandleTime,
				fee = entity.Fee,
				expectReturnDate = entity.ExpectReturnDate,
				handleResult = entity.HandleResult,
				remark = entity.Remark,
				createTime = entity.CreateTime,
				updateTime = entity.UpdateTime
			};
		}

		/// <summary>
		/// 当前登录用户主键（JWT sub）
		/// </summary>
		private string? CurrentUserId()
		{
			return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
				?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
		}

		/// <summary>
		/// 当前登录用户姓名（JWT name）
		/// </summary>
		private string? CurrentUserName()
		{
			return User.FindFirst(ClaimTypes.Name)?.Value;
		}
	}
}
