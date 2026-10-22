using AssetManagementSystem.Attributes;
using AssetManagementSystem.Dtos;
using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;

namespace AssetManagementSystem.Controllers
{
	/// <summary>
	/// 个人消息（用户收件箱）
	/// 所有接口均为"自服务"：登录即可操作自己的消息，无需额外权限。
	/// 消息来源：管理员处理联系请求后自动回发（见 RequestController.Handle）。
	/// </summary>
	[ApiController]
	[Route("message")]
	[Authorize]
	public class MessageController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;

		public MessageController(DbServer dbServer)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>
		/// 我的消息分页（未读优先，再按时间倒序）
		/// </summary>
		[HttpGet("page")]
		public async Task<ApiResult<PageResult<UserMessageDto>>> GetPage([FromQuery] UserMessageQueryDto query)
		{
			query ??= new UserMessageQueryDto();
			(query.pageIndex, query.pageSize) = PageGuard.Normalize(query.pageIndex, query.pageSize);

			// 兼容 JWT 入站声明映射：开启映射时 sub 落在 NameIdentifier，未开启时为 sub
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
				?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
			if (string.IsNullOrWhiteSpace(userId))
				return ApiResult<PageResult<UserMessageDto>>.Fail("401", "登录信息无效，请重新登录");

			var onlyUnread = query.readStatus == "unread";

			// 总数与分页共用查询条件（规避 ToPageListAsync 计数捕获差异）
			var q = _db.Queryable<UserMessage>()
				.Where(m => m.RecipientId == userId)
				.WhereIF(!string.IsNullOrWhiteSpace(query.keyword), m =>
					m.Title.Contains(query.keyword) || m.Content.Contains(query.keyword))
				.WhereIF(onlyUnread, m => m.IsRead == null || m.IsRead == false);

			var total = await q.CountAsync();
			var list = await q
				// 未读(is_read 为空/0)排在已读(1)之前：升序即未读优先
				.OrderBy(m => m.IsRead, OrderByType.Asc)
				.OrderBy(m => m.CreateTime, OrderByType.Desc)
				.ToPageListAsync(query.pageIndex, query.pageSize);

			var result = new PageResult<UserMessageDto>(
				list.Select(MapToDto).ToList(), total, query.pageIndex, query.pageSize);
			return ApiResult<PageResult<UserMessageDto>>.Success(result, "获取成功");
		}

		/// <summary>我的未读消息数量（菜单角标用）</summary>
		[HttpGet("unread-count")]
		public async Task<ApiResult<int>> UnreadCount()
		{
			// 兼容 JWT 入站声明映射：开启映射时 sub 落在 NameIdentifier，未开启时为 sub
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
				?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
			if (string.IsNullOrWhiteSpace(userId))
				return ApiResult<int>.Fail("401", "登录信息无效，请重新登录");

			var count = await _db.Queryable<UserMessage>()
				.Where(m => m.RecipientId == userId && (m.IsRead == null || m.IsRead == false))
				.CountAsync();
			return ApiResult<int>.Success(count, "获取成功");
		}

		/// <summary>标记单条消息为已读（仅限本人消息）</summary>
		[HttpPut("{id}/read")]
		public async Task<ApiResult<object>> MarkRead(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "消息ID不能为空");

			// 兼容 JWT 入站声明映射：开启映射时 sub 落在 NameIdentifier，未开启时为 sub
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
				?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
			if (string.IsNullOrWhiteSpace(userId))
				return ApiResult<object>.Fail("401", "登录信息无效，请重新登录");

			var entity = await _db.Queryable<UserMessage>().FirstAsync(m => m.Id == id && m.RecipientId == userId);
			if (entity == null)
				return ApiResult<object>.Fail("404", "消息不存在");

			if (entity.IsRead == true)
				return ApiResult<object>.Success(null, "已读");

			entity.IsRead = true;
			entity.ReadTime = DateTime.Now;
			entity.UpdateTime = DateTime.Now;

			var count = await _db.Updateable(entity)
				.UpdateColumns(m => new { m.IsRead, m.ReadTime, m.UpdateTime })
				.ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, "已标记为已读")
				: ApiResult<object>.Fail("500", "操作失败");
		}

		/// <summary>一键全部已读</summary>
		[HttpPut("read-all")]
		public async Task<ApiResult<object>> MarkAllRead()
		{
			// 兼容 JWT 入站声明映射：开启映射时 sub 落在 NameIdentifier，未开启时为 sub
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
				?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
			if (string.IsNullOrWhiteSpace(userId))
				return ApiResult<object>.Fail("401", "登录信息无效，请重新登录");

			// 取出未读消息并批量置为已读（个人收件箱数量有限，批量更新按主键）
			var unread = await _db.Queryable<UserMessage>()
				.Where(m => m.RecipientId == userId && (m.IsRead == null || m.IsRead == false))
				.ToListAsync();

			if (unread.Count > 0)
			{
				var now = DateTime.Now;
				foreach (var m in unread)
				{
					m.IsRead = true;
					m.ReadTime = now;
					m.UpdateTime = now;
				}
				await _db.Updateable(unread)
					.UpdateColumns(m => new { m.IsRead, m.ReadTime, m.UpdateTime })
					.ExecuteCommandAsync();
			}
			return ApiResult<object>.Success(null, "已全部标记为已读");
		}

		private UserMessageDto MapToDto(UserMessage e) => new UserMessageDto
		{
			id = e.Id,
			title = e.Title,
			content = e.Content,
			msgType = e.MsgType,
			isRead = e.IsRead,
			readTime = e.ReadTime,
			sourceId = e.SourceId,
			createTime = e.CreateTime
		};
	}
}
