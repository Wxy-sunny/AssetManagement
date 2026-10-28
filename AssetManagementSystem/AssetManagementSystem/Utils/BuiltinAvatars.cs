using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetManagementSystem.Utils
{
	/// <summary>
	/// 系统内置头像工具：统一枚举 wwwroot/avatars 下的图片资源。
	/// 供「注册/新增用户随机分配头像」与「头像选择器列表」共用，避免各处重复维护文件名清单
	/// （注意：资源目录里存在拼写不规则的文件名，必须真实枚举，不能硬编码）。
	/// </summary>
	public static class BuiltinAvatars
	{
		/// <summary>
		/// 枚举内置头像的可访问相对地址（/avatars/xxx.jpg）；目录不存在时返回空列表
		/// </summary>
		public static List<string> GetUrls(IWebHostEnvironment env)
		{
			if (env == null) return new List<string>();
			var root = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
			if (string.IsNullOrWhiteSpace(root)) return new List<string>();

			var dir = Path.Combine(root, "avatars");
			if (!Directory.Exists(dir)) return new List<string>();

			return Directory.EnumerateFiles(dir)
				.Where(f => IsImageExt(Path.GetExtension(f)))
				.OrderBy(f => f)
				.Select(f => "/avatars/" + Path.GetFileName(f))
				.ToList();
		}

		/// <summary>
		/// 随机取一个内置头像；没有可用头像资源时返回 null（前端会回退为文字头像）
		/// </summary>
		public static string? PickRandom(IWebHostEnvironment env)
		{
			var urls = GetUrls(env);
			if (urls.Count == 0) return null;
			return urls[Random.Shared.Next(urls.Count)];
		}

		private static bool IsImageExt(string ext)
		{
			if (string.IsNullOrWhiteSpace(ext)) return false;
			var e = ext.ToLowerInvariant();
			return e == ".jpg" || e == ".jpeg" || e == ".png" || e == ".gif" || e == ".webp";
		}
	}
}
