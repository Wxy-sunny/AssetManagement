using AssetManagementSystem.Extensions;
using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using AssetManagementSystem.Middlewares;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Text;
using System.Security.Cryptography;
using StackExchange.Redis;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

// 生产环境不回显 Kestrel 的 Server 头，减少技术栈暴露
builder.Services.Configure<KestrelServerOptions>(o => o.AddServerHeader = false);

// ============ 日志：Serilog（控制台 + 文件双写） ============
// 文件日志：logs/asset-api-YYYYMMDD.log 按天滚动，单文件 50MB 上限、最多保留 31 天。
// 生产环境建议再接入集中式日志（ELK / Loki 等）收集同一目录。
Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Information()
	// 压低微软框架自身的心智负担级日志噪音
	.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
	.MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
	.WriteTo.Console()
	.WriteTo.File(
		path: "logs/asset-api-.log",
		rollingInterval: RollingInterval.Day,
		retainedFileCountLimit: 31,
		rollOnFileSizeLimit: true,
		fileSizeLimitBytes: 50 * 1024 * 1024,
		outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}{NewLine}      {Message:lj}{NewLine}{Exception}")
	.CreateBootstrapLogger();

try
{
	// 用 Serilog 接管主机日志（此后 builder.Logging / ILogger 均写入 Serilog）
	builder.Host.UseSerilog();

	// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//注入 ISqlSugarClient
builder.Services.AddSqlsugarSetup(builder.Configuration);

//注册 DbServer（作用域生命周期，每个请求一个实例）
builder.Services.AddScoped<DbServer>();

// 内存缓存（作为验证码的降级存储）
builder.Services.AddMemoryCache();

// Redis：验证码等临时数据的主存储
// 连接失败不阻塞启动（AbortOnConnectFail=false），验证码服务会自动降级为内存缓存
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
	var options = new ConfigurationOptions
	{
		AbortOnConnectFail = false,
		ConnectTimeout = 3000
	};
	options.EndPoints.Add(builder.Configuration["Redis:Connection"] ?? "localhost:6379");
	return ConnectionMultiplexer.Connect(options);
});

// 通用缓存服务（Redis 优先 + 内存降级，内置防穿透/防雪崩/防击穿）
builder.Services.AddSingleton<AssetManagementSystem.Services.ICacheService, CacheService>();

// 图形验证码服务（生成 SVG 图片并缓存答案，登录与联系请求提交时校验）
builder.Services.AddSingleton<AssetManagementSystem.Services.ICaptchaService, CaptchaService>();

// 登录防爆破守卫（账号失败锁定 + 可选 IP 限流）
builder.Services.AddSingleton<AssetManagementSystem.Services.LoginGuardService>();

// 手机验证码服务（优先 Redis，Redis 不可用时自动降级为内存）
builder.Services.AddScoped<SmsCodeService>();

// 跨域：允许配置的来源访问 API（生产环境通过 Cors:AllowedOrigins 指定前端域名）
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
	?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowFrontend", policy =>
		policy.WithOrigins(corsOrigins)
			  .AllowAnyHeader()
			  .AllowAnyMethod()
			  .AllowCredentials());
});

// 注入JWT
// JWT 签名密钥：
//  - 生产环境（ASPNETCORE_ENVIRONMENT=Production）必须显式配置 Jwt__SecretKey（≥32 字节），
//    否则启动即失败（Fail-Fast），避免"每次重启旧 token 全失效 / 多实例互不相认"的隐蔽故障。
//  - 非生产环境：密钥缺失时自动生成一次性随机密钥，方便本地联调（仅当前进程有效）。
var jwtSecret = builder.Configuration["Jwt:SecretKey"];
var jwtSecretGenerated = false;
var isProd = builder.Environment.IsProduction();
if (string.IsNullOrWhiteSpace(jwtSecret))
{
	if (isProd)
	{
		Log.Fatal("生产环境检测到 Jwt:SecretKey 未配置。请通过环境变量 Jwt__SecretKey 设置一个稳定且≥32字节的密钥后再启动。");
		throw new InvalidOperationException("生产环境必须配置 Jwt__SecretKey（≥32字节）");
	}

	jwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
	jwtSecretGenerated = true;
	// 关键修复：把生成的密钥回写到配置，保证 AuthController 签发 Token 时
	// 使用与 Program 校验相同的密钥（否则签发用空密钥会抛 500）。
	builder.Configuration["Jwt:SecretKey"] = jwtSecret;
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AssetManagementSystem";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AssetManagementSystem";

builder.Services.AddAuthentication(options =>
{
	options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
	options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
	options.TokenValidationParameters = new TokenValidationParameters
	{
		ValidateIssuer = true,
		ValidateAudience = true,
		ValidateLifetime = true,
		ValidateIssuerSigningKey = true,
		ValidIssuer = jwtIssuer,
		ValidAudience = jwtAudience,
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
	};
});

// ============ 速率限制（防滥用：短信轰炸 / 批量注册 / 重置密码 / 联系请求刷单） ============
// 限流键优先取真实客户端 IP（X-Forwarded-For 首段，兼容反向代理；与 LoginGuard 取 IP 逻辑一致）。
// 注意：经反向代理部署时若未正确配置 ForwardedHeaders，X-Forwarded-For 可被客户端伪造，
// 因此限流为"尽力而为"的滥用防护而非强认证手段。
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.OnRejected = async (ctx, ct) =>
	{
		ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
		ctx.HttpContext.Response.ContentType = "application/json; charset=utf-8";
		await ctx.HttpContext.Response.WriteAsJsonAsync(
			new { code = "429", message = "请求过于频繁，请稍后再试", data = (object?)null }, ct);
	};

	// 短信发送：代价高、易被轰炸，限流最严（同一 IP 10 分钟 5 次）
	options.AddPolicy("sms", ctx =>
		RateLimitPartition.GetFixedWindowLimiter(
			"sms:" + (LoginGuardService.GetClientIp(ctx) ?? "unknown"),
			_ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 5,
				Window = TimeSpan.FromMinutes(10),
				QueueLimit = 0,
			}));

	// 公开表单类接口（注册 / 找回密码 / 联系请求）：同一 IP 10 分钟 10 次
	options.AddPolicy("public-form", ctx =>
		RateLimitPartition.GetFixedWindowLimiter(
			"pf:" + (LoginGuardService.GetClientIp(ctx) ?? "unknown"),
			_ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 10,
				Window = TimeSpan.FromMinutes(10),
				QueueLimit = 0,
			}));
});



var app = builder.Build();

// ============ 主机白名单（Host Filtering） ============
// 说明：.NET 10 的 WebApplication 不会自动按逗号拆分 AllowedHosts 字符串；
// 且当 AllowedHosts 写成 JSON 数组时，顶层标量键为 null，框架不会注入 HostFiltering。
// 这里显式读取（兼容“逗号字符串”与“JSON 数组”两种写法）并注册 HostFilteringOptions，
// 以支持多主机白名单（本地 localhost + cloudflared 隧道 *.trycloudflare.com 等）。
var allowedHosts = new List<string>();
// 优先读取 JSON 数组形式：数组在配置中会被展开为 AllowedHosts:0 / :1 ... 子节点，
// 且不会覆盖同键的标量（如 appsettings.json 的 AllowedHosts:"*"），故必须优先遍历子节点。
foreach (var child in builder.Configuration.GetSection("AllowedHosts").GetChildren())
{
	if (!string.IsNullOrWhiteSpace(child.Value))
		allowedHosts.Add(child.Value);
}
// 数组为空时（例如未写成数组的环境），回退到“逗号字符串”写法
if (allowedHosts.Count == 0)
{
	var allowedHostsRaw = builder.Configuration["AllowedHosts"];
	if (!string.IsNullOrWhiteSpace(allowedHostsRaw))
	{
		allowedHosts.AddRange(allowedHostsRaw.Split(',',
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
	}
}
// ============ 反向代理头（X-Forwarded-For / X-Forwarded-Proto） ============
// 经 nginx/Caddy 等代理部署时，必须信任代理才能让 RemoteIpAddress 与 HTTPS 判断正确
// （LoginGuard 的 IP 限流、SecurityHeaders 的 HSTS 判断、速率限制的客户端 IP 都依赖它）。
// 默认信任本机回环；生产请把反向代理 IP 写入 ReverseProxy:TrustedProxies（逗号分隔）。
var fwdOptions = new ForwardedHeadersOptions
{
	ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
	KnownProxies = { IPAddress.Loopback, IPAddress.IPv6Loopback },
};
foreach (var ip in (builder.Configuration["ReverseProxy:TrustedProxies"] ?? "")
			 .Split(',', StringSplitOptions.RemoveEmptyEntries))
{
	if (IPAddress.TryParse(ip.Trim(), out var addr)) fwdOptions.KnownProxies.Add(addr);
}
foreach (var cidr in (builder.Configuration["ReverseProxy:TrustedNetworks"] ?? "")
			   .Split(',', StringSplitOptions.RemoveEmptyEntries))
{
	if (Microsoft.AspNetCore.HttpOverrides.IPNetwork.TryParse(cidr.Trim(), out Microsoft.AspNetCore.HttpOverrides.IPNetwork net))
		fwdOptions.KnownNetworks.Add(net);
}
app.UseForwardedHeaders(fwdOptions);

// 主机白名单校验（必须在路由 / 认证之前执行）
// 兼容“逗号字符串”与“JSON 数组”两种 AllowedHosts 写法，并支持 *.domain 子域通配符。
if (allowedHosts.Count > 0)
{
	var allowed = allowedHosts;
	app.Use(async (ctx, next) =>
	{
		var host = ctx.Request.Host.Host; // 不含端口
		var ok = !string.IsNullOrEmpty(host) && allowed.Any(rule =>
		{
			if (rule == "*") return true;
			if (rule.StartsWith("*."))
				return host.EndsWith(rule.Substring(1), StringComparison.OrdinalIgnoreCase);
			return string.Equals(rule, host, StringComparison.OrdinalIgnoreCase);
		});
		if (!ok)
		{
			ctx.Response.StatusCode = 400;
			await ctx.Response.WriteAsync("Bad Request - Invalid Hostname");
			return;
		}
		await next();
	});
}

// ============ 安全响应头（HSTS/CSP/X-Frame-Options 等） ============
app.UseMiddleware<SecurityHeadersMiddleware>();

// 提示：若使用运行时生成的临时 JWT 密钥，提醒生产环境配置稳定密钥
if (jwtSecretGenerated)
{
	app.Logger.LogWarning("Jwt:SecretKey 未配置，已使用运行时生成的临时密钥（重启后失效）。生产环境请通过环境变量 Jwt__SecretKey 设置稳定密钥。");
}

using (var scope = app.Services.CreateScope())
{
	var dbServer = scope.ServiceProvider.GetRequiredService<DbServer>();
	var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

	// 数据库连接串未配置时：跳过初始化与播种，避免启动即崩溃（生产环境请通过环境变量配置）
	var assetConnection = configuration.GetConnectionString("AssetConnection");
	if (string.IsNullOrWhiteSpace(assetConnection))
	{
		Log.Warning("ConnectionStrings:AssetConnection 未配置，已跳过数据库初始化与管理员播种。生产环境请通过环境变量 ConnectionStrings__AssetConnection 注入连接字符串。");
	}
	else
	{
		// 结构补齐（新增列等）必须始终执行：保证代码引用的新列存在，
		// 不受 DatabaseInitialization:Enabled 控制（该开关仅控制是否建表 / 播种）
		DbSchemaPatcher.Patch(dbServer.Use(DbNames.Asset));

		// 自动建表：受 DatabaseInitialization.Enabled 控制（生产环境建议关闭）
		var dbInit = configuration.GetSection("DatabaseInitialization");
		if (dbInit.GetValue<bool>("Enabled") && dbInit.GetValue<bool>("CreateTablesOnStartup"))
		{
			dbServer.CreateTablesInDb(DbNames.Asset);
		}

		// 初始化系统管理员账号（不存在才创建，不覆盖已有密码）
		DataSeeder.SeedAdmin(dbServer.Use(DbNames.Asset), configuration);

		// 生成演示数据（仅在 DemoData:Enabled 且相关表为空时执行）
		DataSeeder.SeedDemoData(dbServer.Use(DbNames.Asset), configuration);
	}
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 纯 HTTP 部署（无外层 TLS）时禁用 HTTPS 重定向，否则浏览器会被 307 重定向到
// 不存在的 https 端口而拒绝访问。部署 TLS（外层 Caddy/证书）时请取消下面注释启用。
// if (!app.Environment.IsDevelopment())
// {
// 	app.UseHttpsRedirection();
// }

// 启用静态文件访问（资料附件存放于 wwwroot/uploads，用于图片预览等）
app.UseStaticFiles();

// 跨域（必须在认证之前）
app.UseCors("AllowFrontend");

// 速率限制（在认证之前生效，拦截匿名滥用请求）
app.UseRateLimiter();

// 全局异常捕获与请求日志（包裹后续管道）
app.UseMiddleware<AssetManagementSystem.Middlewares.ApiPipelineMiddleware>();

// 确保上传目录存在
var uploadRoot = Path.Combine(
	app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
	"uploads");
if (!Directory.Exists(uploadRoot))
{
	Directory.CreateDirectory(uploadRoot);
}

// 启用认证中间
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
}
catch (Exception ex)
{
	// 启动/运行期致命错误：写入文件日志后抛出（便于事后排查）
	Log.Fatal(ex, "应用发生致命错误并终止");
	throw;
}
finally
{
	// 确保缓冲日志全部落盘
	Log.CloseAndFlush();
}

// 显式声明为 public：供集成测试项目通过 WebApplicationFactory<Program> 引用
//（Top-level Statements 生成的 Program 默认为 internal）
public partial class Program { }
