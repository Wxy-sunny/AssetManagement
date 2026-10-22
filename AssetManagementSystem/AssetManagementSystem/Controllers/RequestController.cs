using AssetManagementSystem.Attributes;
using AssetManagementSystem.Dtos;
using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AssetManagementSystem.Controllers
{
	/// <summary>
	/// 联系请求（用户→管理员）
	/// 公开提交：登录页"联系管理员"（通常重置密码申请），需图形验证码防刷
	/// 管理端：分页查看、待处理数量、标记已处理（需 request:list / request:manage 权限）
	/// </summary>
	[ApiController]
	[Route("request")]
	public class RequestController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;
		private readonly ICaptchaService _captcha;

		public RequestController(DbServer dbServer, ICaptchaService captcha)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);
			_captcha = captcha;
		}

		/// <summary>
		/// 公开提交：用户在登录页提交联系管理员 / 重置密码申请
		/// 需图形验证码，且同一手机号 1 分钟内不可重复提交，防止刷单
		/// </summary>
		[HttpPost("submit")]
		public async Task<ApiResult<object>> Submit([FromBody] ContactRequestSubmitDto dto)
		{
			dto ??= new ContactRequestSubmitDto();

			// 图形验证码校验（防刷）
			if (!await _captcha.ValidateAsync(dto.captchaId, dto.captcha))
				return ApiResult<object>.Fail("400", "验证码错误");

			if (string.IsNullOrWhiteSpace(dto.account) && string.IsNullOrWhiteSpace(dto.mobile))
				return ApiResult<object>.Fail("400", "请填写账号或手机号，便于管理员联系您");

			if (string.IsNullOrWhiteSpace(dto.content) || dto.content!.Length < 4)
				return ApiResult<object>.Fail("400", "请填写具体说明（至少 4 个字）");

			// 频率限制：同一手机号 1 分钟内仅允许提交一次
			if (!string.IsNullOrWhiteSpace(dto.mobile))
			{
				var recent = await _db.Queryable<ContactRequest>()
					.Where(r => r.Mobile == dto.mobile && r.CreateTime > DateTime.Now.AddMinutes(-1))
					.AnyAsync();
				if (recent)
					return ApiResult<object>.Fail("400", "提交过于频繁，请稍后再试");
			}

			var entity = new ContactRequest
			{
				Account = dto.account?.Trim(),
				Mobile = dto.mobile?.Trim(),
				RequestType = string.IsNullOrWhiteSpace(dto.requestType)
					? RequestTypes.ResetPassword
					: dto.requestType.Trim(),
				Content = dto.content.Trim(),
				Status = RequestStatus.Pending,
				SenderId = dto.senderId,
				IsEnabled = true,
				StatusCode = 1,
				StatusDesc = "待处理",
				CreateTime = DateTime.Now
			};

			var count = await _db.Insertable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, "提交成功，管理员会尽快处理")
				: ApiResult<object>.Fail("500", "提交失败");
		}

		/// <summary>管理端：分页查询联系请求（待处理优先）</summary>
		[HttpGet("page")]
		[Authorize]
		[Permission(Permissions.RequestList)]
		public async Task<ApiResult<PageResult<ContactRequestDto>>> GetPage([FromQuery] ContactRequestQueryDto query)
		{
			query ??= new ContactRequestQueryDto();
			if (query.pageIndex < 1) query.pageIndex = 1;
			if (query.pageSize < 1) query.pageSize = 10;

			// 构建查询条件（总数与分页共用，避免依赖 ToPageListAsync 的计数捕获差异）
			var q = _db.Queryable<ContactRequest>()
				.WhereIF(!string.IsNullOrWhiteSpace(query.keyword), r =>
					r.Account.Contains(query.keyword) ||
					r.Mobile.Contains(query.keyword) ||
					r.Content.Contains(query.keyword))
				.WhereIF(!string.IsNullOrWhiteSpace(query.status), r => r.Status == query.status);

			var total = await q.CountAsync();
			var list = await q
				// 待处理(pending) 字典序在 handled 之后，降序即 pending 在前；再按时间倒序
				.OrderBy(r => r.Status, OrderByType.Desc)
				.OrderBy(r => r.CreateTime, OrderByType.Desc)
				.ToPageListAsync(query.pageIndex, query.pageSize);

			var result = new PageResult<ContactRequestDto>(
				list.Select(MapToDto).ToList(), total, query.pageIndex, query.pageSize);
			return ApiResult<PageResult<ContactRequestDto>>.Success(result, "获取成功");
		}

		/// <summary>管理端：待处理数量（菜单角标用）</summary>
		[HttpGet("pending-count")]
		[Authorize]
		[Permission(Permissions.RequestList)]
		public async Task<ApiResult<int>> PendingCount()
		{
			var count = await _db.Queryable<ContactRequest>()
				.CountAsync(r => r.Status == RequestStatus.Pending);
			return ApiResult<int>.Success(count, "获取成功");
		}

		/// <summary>管理端：标记已处理（可填备注）</summary>
		[HttpPut("{id}/handle")]
		[Authorize]
		[Permission(Permissions.RequestManage)]
		public async Task<ApiResult<object>> Handle(string id, [FromBody] HandleRequestDto dto)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "请求ID不能为空");

			var entity = await _db.Queryable<ContactRequest>().FirstAsync(r => r.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "请求不存在");

			entity.Status = RequestStatus.Handled;
			entity.HandlerName = User.Identity?.Name;
			entity.HandleTime = DateTime.Now;
			entity.HandleRemark = dto?.remark?.Trim();
			entity.UpdateTime = DateTime.Now;

			var count = await _db.Updateable(entity)
				.UpdateColumns(r => new { r.Status, r.HandlerName, r.HandleTime, r.HandleRemark, r.UpdateTime })
				.ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, "已标记为已处理")
				: ApiResult<object>.Fail("500", "操作失败");
		}

		private ContactRequestDto MapToDto(ContactRequest e) => new ContactRequestDto
		{
			id = e.Id,
			account = e.Account,
			mobile = e.Mobile,
			requestType = e.RequestType,
			content = e.Content,
			status = e.Status,
			senderId = e.SenderId,
			handlerName = e.HandlerName,
			handleTime = e.HandleTime,
			handleRemark = e.HandleRemark,
			createTime = e.CreateTime,
			updateTime = e.UpdateTime
		};
	}
}
