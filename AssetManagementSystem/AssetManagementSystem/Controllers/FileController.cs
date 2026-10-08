using AssetManagementSystem.Attributes;
using AssetManagementSystem.Dtos;
using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
	[Route("file")]
	[Authorize]
	public class FileController : ControllerBase
	{
		/// <summary>单个文件上限 20MB</summary>
		private const long MaxFileSize = 20 * 1024 * 1024;

		/// <summary>允许上传的扩展名白名单</summary>
		private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg",
			".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
			".txt", ".csv", ".zip", ".rar", ".7z"
		};

		private readonly DbServer _dbServer;
		private readonly ISqlSugarClient _db;
		private readonly IWebHostEnvironment _env;

		public FileController(DbServer dbServer, IWebHostEnvironment env)
		{
			_dbServer = dbServer;
			_env = env;
			_db = dbServer.Use(DbNames.Asset);
		}

		/// <summary>
		/// 上传文件（支持多文件，字段名任意，走 multipart/form-data）
		/// 附加字段：bizType（业务类型）、assetId（关联资产Id）
		/// </summary>
		[HttpPost("upload")]
		[RequestSizeLimit(100 * 1024 * 1024)] // 单次请求总上限 100MB
		[Permission(Permissions.FileUpload)]
		public async Task<ApiResult<object>> Upload()
		{
			var form = await Request.ReadFormAsync();
			var files = form.Files;
			if (files == null || files.Count == 0)
				return ApiResult<object>.Fail("400", "请选择要上传的文件");

			var bizType = form["bizType"].ToString();
			var assetId = form["assetId"].ToString();
			if (string.IsNullOrWhiteSpace(bizType))
				bizType = FileBizTypes.Common;

			var assetName = !string.IsNullOrWhiteSpace(assetId)
				? (await _db.Queryable<Asset>().FirstAsync(a => a.Id == assetId))?.AssetName
				: null;

			var uploadUser = User.Identity?.Name;
			var saved = new List<object>();

			foreach (var file in files)
			{
				if (file == null || file.Length == 0) continue;

				var ext = Path.GetExtension(file.FileName);
				if (!AllowedExtensions.Contains(ext))
					return ApiResult<object>.Fail("400", $"不支持的文件类型：{ext}");
				if (file.Length > MaxFileSize)
					return ApiResult<object>.Fail("400", $"文件 {file.FileName} 超过 20MB 上限");

				var (relativePath, physicalPath, savedName) = await SaveToDisk(file, ext);

				var entity = new AssetFile
				{
					Id = Guid.NewGuid().ToString(),
					FileName = file.FileName,
					SavedName = savedName,
					FilePath = relativePath,
					PhysicalPath = physicalPath,
					FileExt = ext,
					FileSize = file.Length,
					ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
					BizType = bizType,
					AssetId = string.IsNullOrWhiteSpace(assetId) ? null : assetId,
					AssetName = assetName,
					UploadUserName = uploadUser,
					IsEnabled = true,
					StatusCode = 1,
					StatusDesc = "启用",
					CreateTime = DateTime.Now
				};

				await _db.Insertable(entity).ExecuteCommandAsync();
				saved.Add(new { id = entity.Id, fileName = entity.FileName, filePath = entity.FilePath });
			}

			return ApiResult<object>.Success(saved, $"上传成功，共 {saved.Count} 个文件");
		}

		/// <summary>
		/// 资料列表（支持业务类型 / 关联资产 / 关键字 筛选，分页）
		/// </summary>
		[HttpGet("page")]
		[Permission(Permissions.FileList)]
		public async Task<ApiResult<PageResult<FileDto>>> GetPage(
			[FromQuery] string? keyword = null,
			[FromQuery] string? bizType = null,
			[FromQuery] string? assetId = null,
			[FromQuery] int pageIndex = 1,
			[FromQuery] int pageSize = 10)
		{
			if (pageIndex < 1) pageIndex = 1;
			if (pageSize < 1) pageSize = 10;

			var total = 0;
			var list = await _db.Queryable<AssetFile>()
				.WhereIF(!string.IsNullOrWhiteSpace(keyword), f => f.FileName.Contains(keyword))
				.WhereIF(!string.IsNullOrWhiteSpace(bizType), f => f.BizType == bizType)
				.WhereIF(!string.IsNullOrWhiteSpace(assetId), f => f.AssetId == assetId)
				.OrderBy(f => f.CreateTime, OrderByType.Desc)
				.ToPageListAsync(pageIndex, pageSize, total);

			var dtoList = list.Select(MapToDto).ToList();
			var result = new PageResult<FileDto>(dtoList, total, pageIndex, pageSize);
			return ApiResult<PageResult<FileDto>>.Success(result, "获取成功");
		}

		/// <summary>
		/// 下载文件（以原始文件名下载）
		/// </summary>
		[HttpGet("download/{id}")]
		[Permission(Permissions.FileList)]
		public async Task<IActionResult> Download(string id)
		{
			if (string.IsNullOrWhiteSpace(id)) return BadRequest("文件ID不能为空");

			var entity = await _db.Queryable<AssetFile>().FirstAsync(f => f.Id == id);
			if (entity == null) return NotFound("文件不存在");

			var path = entity.PhysicalPath;
			if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
			{
				// 兼容历史数据：按相对路径重新定位
				var root = GetWebRootPath();
				path = Path.Combine(root, entity.FilePath?.TrimStart('/', '\\') ?? "");
				if (!System.IO.File.Exists(path)) return NotFound("文件已丢失");
			}

			var contentType = string.IsNullOrWhiteSpace(entity.ContentType)
				? "application/octet-stream"
				: entity.ContentType;

			return PhysicalFile(path, contentType, entity.FileName);
		}

		/// <summary>
		/// 在线预览（图片 / PDF 等浏览器可直接渲染的类型）
		/// </summary>
		[HttpGet("preview/{id}")]
		[Permission(Permissions.FileList)]
		public async Task<IActionResult> Preview(string id)
		{
			if (string.IsNullOrWhiteSpace(id)) return BadRequest("文件ID不能为空");

			var entity = await _db.Queryable<AssetFile>().FirstAsync(f => f.Id == id);
			if (entity == null) return NotFound("文件不存在");

			var path = entity.PhysicalPath;
			if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
				return NotFound("文件已丢失");

			return PhysicalFile(path, entity.ContentType ?? "application/octet-stream");
		}

		/// <summary>
		/// 删除文件（同时删除物理文件）
		/// </summary>
		[HttpDelete("{id}")]
		[Permission(Permissions.FileDelete)]
		public async Task<ApiResult<object>> Delete(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return ApiResult<object>.Fail("400", "文件ID不能为空");

			var entity = await _db.Queryable<AssetFile>().FirstAsync(f => f.Id == id);
			if (entity == null)
				return ApiResult<object>.Fail("404", "文件不存在");

			DeletePhysicalFile(entity.PhysicalPath);

			var count = await _db.Deleteable(entity).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, "删除成功")
				: ApiResult<object>.Fail("500", "删除失败");
		}

		/// <summary>
		/// 批量删除文件
		/// </summary>
		[HttpPost("batch-delete")]
		[Permission(Permissions.FileDelete)]
		public async Task<ApiResult<object>> BatchDelete([FromBody] List<string> ids)
		{
			if (ids == null || ids.Count == 0)
				return ApiResult<object>.Fail("400", "请选择要删除的文件");

			var files = await _db.Queryable<AssetFile>().In(f => f.Id, ids).ToListAsync();
			foreach (var file in files)
			{
				DeletePhysicalFile(file.PhysicalPath);
			}

			var count = await _db.Deleteable<AssetFile>().In(f => f.Id, ids).ExecuteCommandAsync();
			return count > 0
				? ApiResult<object>.Success(null, $"已删除 {count} 个文件")
				: ApiResult<object>.Fail("500", "删除失败");
		}

		/// <summary>
		/// 保存文件到 wwwroot/uploads/yyyy/MM/ 下，返回相对路径与物理路径
		/// </summary>
		private async Task<(string relativePath, string physicalPath, string savedName)> SaveToDisk(IFormFile file, string ext)
		{
			var root = GetWebRootPath();
			var datePart = DateTime.Now.ToString("yyyy/MM");
			var dir = Path.Combine(root, "uploads", DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"));

			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			var savedName = Guid.NewGuid().ToString("N") + ext;
			var physicalPath = Path.Combine(dir, savedName);

			await using (var stream = new FileStream(physicalPath, FileMode.Create))
			{
				await file.CopyToAsync(stream);
			}

			var relativePath = $"/uploads/{datePart}/{savedName}";
			return (relativePath, physicalPath, savedName);
		}

		private string GetWebRootPath()
		{
			var root = _env.WebRootPath;
			if (string.IsNullOrWhiteSpace(root))
				root = Path.Combine(_env.ContentRootPath, "wwwroot");
			if (!Directory.Exists(root))
				Directory.CreateDirectory(root);
			return root;
		}

		private void DeletePhysicalFile(string? physicalPath)
		{
			if (string.IsNullOrWhiteSpace(physicalPath)) return;
			try
			{
				if (System.IO.File.Exists(physicalPath))
					System.IO.File.Delete(physicalPath);
			}
			catch
			{
				// 物理文件删除失败不影响数据库记录清理
			}
		}

		private FileDto MapToDto(AssetFile entity)
		{
			return new FileDto
			{
				id = entity.Id,
				fileName = entity.FileName,
				savedName = entity.SavedName,
				filePath = entity.FilePath,
				fileExt = entity.FileExt,
				fileSize = entity.FileSize,
				fileSizeText = FormatFileSize(entity.FileSize ?? 0),
				contentType = entity.ContentType,
				bizType = entity.BizType,
				assetId = entity.AssetId,
				assetName = entity.AssetName,
				uploadUserName = entity.UploadUserName,
				createTime = entity.CreateTime
			};
		}

		/// <summary>
		/// 格式化文件大小为可读文本
		/// </summary>
		private static string FormatFileSize(long bytes)
		{
			if (bytes <= 0) return "0 B";
			string[] units = { "B", "KB", "MB", "GB" };
			var index = 0;
			double size = bytes;
			while (size >= 1024 && index < units.Length - 1)
			{
				size /= 1024;
				index++;
			}
			return $"{size:0.##} {units[index]}";
		}
	}
}
