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
using System.Security.Claims;
using System.Threading.Tasks;

namespace AssetManagementSystem.Controllers
{
	/// <summary>
	/// 通知公告
	/// 管理端：通知的增删改、发布/撤回（需 notification:list / notification:manage 权限）
	/// 用户端：查看已发布通知（登录即可，无需权限）
	/// </summary>
	[ApiController]
	[Route("notification")]
	[Authorize]
	public class NotificationController : ControllerBase
	{
		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;

		public NotificationController(DbServer dbServer)
		{
			_dbServer = dbServer;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>
		/// 通知分页（管理端：包含未发布草稿）
		/// </summary>
		[HttpGet("page")]
		[Permission(Permissions.NotificationList)]
		public async Task<ApiResult<PageResult<NotificationDto>>> GetPage(
			[FromQuery] NotificationQueryDto query)
		{
			query ??= new NotificationQueryDto();
			if (query.pageIndex < 1) query.pageIndex = 1;
			if (query.pageSize < 1) query.pageSize = 10;

			var total = 0;
			var list = await _db.Queryable<Notification>()
				.WhereIF(!string.IsNullOrWhiteSpace(query.keyword), n => n.Title.Contains(query.keyword))
				.WhereIF(!string.IsNullOrWhiteSpace(query.noticeType), n => n.NoticeType == query.noticeType)
				.WhereIF(query.isPublished.HasValue, n => n.IsPublished == query.isPublished)
				.OrderBy(n => n.CreateTime, OrderByType.Desc)
				.ToPageListAsync(query.pageIndex, query.pageSize, total);

			var result = new PageResult<NotificationDto>(
				list.Select(MapToDto).ToList(), total, query.pageIndex, query.pageSize);

			return ApiResult<PageResult<NotificationDto>>.Success(result, "获取成功");
		}

		/// <summary>
		/// 已发布通知分页（用户端：登录即可查看，无需权限）
		/// </summary>
		[HttpGet("published")]
		public async Task<ApiResult<PageResult<NotificationDto>>> GetPublished(
			[FromQuery] string? keyword = null,
			[FromQuery] int pageIndex = 1,
			[FromQuery] int pageSize = 10)
		{
			if (pageIndex < 1) pageIndex = 1;
			if (pageSize < 1) pageSize = 10;

			var total = 0;
			var list = await _db.Queryable<Notification>()
				.Where(n => n.IsPublished == true)
				.WhereIF(!string.IsNullOrWhiteSpace(keyword), n => n.Title.Contains(keyword))
				.OrderBy(n => n.PublishTime, OrderByType.Desc)
				.ToPageListAsync(pageIndex, pageSize, total);

			var result = new PageResult<NotificationDto>(
				list.Select(MapToDto).ToList(), total, pageIndex, pageSize);

			return ApiResult<PageResult<NotificationDto>>.Success(result, "获取成功");
		}

		/// <summary>
		/// 新增通知（默认为未发布草稿）
		/// </summary>
		[HttpPost]
		[Permission(Permissions.NotificationManage)]
		public async Task<ApiResult<object>> Add([FromBody] NotificationDto dto)
		{
			if (dto == null)
				return ApiResult<object>.Fail("400", "参数不能为空");
			if (string.IsNullOrWhiteSpace(dto.title))
				return ApiResult<object>.Fail("400", "通知标题不能为空");
			if (string.IsNullOrWhiteSpace(dto.content))
				return ApiResult<object>.Fail("400", "通知内容不能为空");

			var entity = new Notification
			{
				Id = Guid.NewGuid().ToString(),
				Title = dto.title.Trim(),
				Content = dto.content,
				NoticeType = string.IsNullOrWhiteSpace(dto.noticeType)
					? NoticeTypes.Notice
					: dto.noticeType,
				IsPublished = false,
				Publisher = User.Identity?.Name,
				IsEnabled = true,
				StatusCode = 1,
				StatusDesc = "启用",
				CreateTime = DateTime.Now
			};

			var count = await _db.Insertable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(new { id = entity.Id }, "新增成功，可在列表中发布")
				: ApiResult<object>.Fail("500", "新增失败");
		}

		/// <summary>
		/// 修改通知（仅未发布或已发布的均可改，改后需重新确认发布状态）
		/// </summary>
		[HttpPut]
		[Permission(Permissions.NotificationManage)]
		public async Task<ApiResult<object>> Update([FromBody] NotificationDto dto)
		{
			if (dto == null || string.IsNullOrWhiteSpace(dto.id))
				return ApiResult<object>.Fail("400", "通知ID不能为空");
			if (string.IsNullOrWhiteSpace(dto.title))
				return ApiResult<object>.Fail("400", "通知标题不能为空");
			if (string.IsNullOrWhiteSpace(dto.content))
				return ApiResult<object>.Fail("400", "通知内容不能为空");

			var entity = await _db.Queryable<Notification>().FirstAsync(n => n.Id == dto.id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "通知不存在");

			entity.Title = dto.title.Trim();
			entity.Content = dto.content;
			entity.NoticeType = string.IsNullOrWhiteSpace(dto.noticeType)
				? NoticeTypes.Notice
				: dto.noticeType;
			entity.UpdateTime = DateTime.Now;

			var count = await _db.Updateable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, "修改成功")
				: ApiResult<object>.Fail("500", "修改失败");
		}

		/// <summary>
		/// 删除通知
		/// </summary>
		[HttpDelete("{id}")]
		[Permission(Permissions.NotificationManage)]
		public async Task<ApiResult<object>> Delete(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "通知ID不能为空");

			var entity = await _db.Queryable<Notification>().FirstAsync(n => n.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "通知不存在");

			var count = await _db.Deleteable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, "删除成功")
				: ApiResult<object>.Fail("500", "删除失败");
		}

		/// <summary>
		/// 发布 / 撤回通知
		/// </summary>
		[HttpPut("{id}/publish")]
		[Permission(Permissions.NotificationManage)]
		public async Task<ApiResult<object>> Publish(string id, [FromBody] PublishNotificationDto dto)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "通知ID不能为空");
			if (dto == null)
				return ApiResult<object>.Fail("400", "参数不能为空");

			var entity = await _db.Queryable<Notification>().FirstAsync(n => n.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "通知不存在");

			entity.IsPublished = dto.isPublished;
			// 发布时记录发布人与发布时间；撤回时保留原发布时间
			if (dto.isPublished)
			{
				entity.PublishTime = DateTime.Now;
				entity.Publisher = User.Identity?.Name ?? entity.Publisher;
			}
			entity.UpdateTime = DateTime.Now;

			var count = await _db.Updateable(entity)
				.UpdateColumns(n => new { n.IsPublished, n.PublishTime, n.Publisher, n.UpdateTime })
				.ExecuteCommandAsync();

			return count > 0
				? ApiResult<object>.Success(null, dto.isPublished ? "发布成功" : "已撤回")
				: ApiResult<object>.Fail("500", "操作失败");
		}

		private NotificationDto MapToDto(Notification entity)
		{
			return new NotificationDto
			{
				id = entity.Id,
				title = entity.Title ?? "",
				content = entity.Content ?? "",
				noticeType = entity.NoticeType,
				isPublished = entity.IsPublished ?? false,
				publishTime = entity.PublishTime,
				publisher = entity.Publisher,
				createTime = entity.CreateTime,
				updateTime = entity.UpdateTime
			};
		}
	}
}
