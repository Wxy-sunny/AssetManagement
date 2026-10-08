using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AssetManagementSystem.Middlewares;

/// <summary>
/// 全局请求日志与异常捕获中间件：
/// 1. 记录每个请求的方法、路径、状态码与耗时；
/// 2. 捕获后续管道未处理的异常，避免将堆栈信息泄露给客户端，并记录日志。
/// </summary>
public class ApiPipelineMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<ApiPipelineMiddleware> _logger;

	public ApiPipelineMiddleware(RequestDelegate next, ILogger<ApiPipelineMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		var stopwatch = Stopwatch.StartNew();
		try
		{
			await _next(context);
			stopwatch.Stop();

			var status = context.Response.StatusCode;
			if (status >= 500)
				_logger.LogError("HTTP {Method} {Path} => {StatusCode} 耗时 {Elapsed}ms",
					context.Request.Method, context.Request.Path, status, stopwatch.ElapsedMilliseconds);
			else if (status >= 400)
				_logger.LogWarning("HTTP {Method} {Path} => {StatusCode} 耗时 {Elapsed}ms",
					context.Request.Method, context.Request.Path, status, stopwatch.ElapsedMilliseconds);
			else
				_logger.LogInformation("HTTP {Method} {Path} => {StatusCode} 耗时 {Elapsed}ms",
					context.Request.Method, context.Request.Path, status, stopwatch.ElapsedMilliseconds);
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			_logger.LogError(ex, "未处理的异常：HTTP {Method} {Path} 耗时 {Elapsed}ms",
				context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);

			if (!context.Response.HasStarted)
			{
				context.Response.Clear();
				context.Response.StatusCode = StatusCodes.Status500InternalServerError;
				context.Response.ContentType = "application/json; charset=utf-8";
				await context.Response.WriteAsJsonAsync(new
				{
					code = 500,
					message = "服务器内部错误，请稍后重试",
					data = (object?)null
				}, new System.Text.Json.JsonSerializerOptions
				{
					PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
				});
			}
		}
	}
}
