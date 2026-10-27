using Microsoft.AspNetCore.Http;

namespace AssetManagementSystem.Middlewares
{
	/// <summary>
	/// 安全响应头中间件：为所有响应追加通用安全头，缓解 XSS / 点击劫持 / MIME 嗅探 / 信息泄露等风险。
	/// 说明：
	///  - HSTS 仅在 HTTPS 下被浏览器生效（HTTP 下忽略），配合反向代理 TLS 终止使用；
	///  - CSP 采用偏严格但兼容本系统的策略（Element Plus 运行时注入内联 style，故 style-src 放行 unsafe-inline）。
	///    若部署后页面异常，请优先排查 CSP（浏览器控制台会列出被拦截资源），再据此放宽。
	/// </summary>
	public class SecurityHeadersMiddleware
	{
		private readonly RequestDelegate _next;

		// 这些头一旦设置不重复追加，避免代理链路多次写入
		private const string Csp = "default-src 'self'; " +
								   "img-src 'self' data: blob:; " +
								   "font-src 'self' data:; " +
								   "style-src 'self' 'unsafe-inline'; " +
								   "script-src 'self'; " +
								   "connect-src 'self'; " +
								   "frame-ancestors 'none'; " +
								   "object-src 'none'; " +
								   "base-uri 'self'; " +
								   "form-action 'self'";

		public SecurityHeadersMiddleware(RequestDelegate next)
		{
			_next = next;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var headers = context.Response.Headers;

			headers["X-Content-Type-Options"] = "nosniff";
			headers["X-Frame-Options"] = "DENY";
			headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
			headers["Content-Security-Policy"] = Csp;

			// HSTS：仅当已建立 TLS（或处于反向代理后的安全环境）时下发，避免纯 HTTP 部署把用户"锁死"在 https
			var isHttps = context.Request.IsHttps ||
						  string.Equals(context.Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);
			if (isHttps)
			{
				headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
			}

			// 去除可能泄露技术栈的标识头
			headers.Remove("Server");
			headers.Remove("X-Powered-By");
			headers.Remove("X-AspNetCore-Razor");

			await _next(context);
		}
	}
}
