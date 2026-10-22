using AssetManagementSystem.Extensions;
using AssetManagementSystem.Models;
using AssetManagementSystem.Services;
using AssetManagementSystem.Utils;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Cryptography;
using StackExchange.Redis;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

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
// JWT 签名密钥：配置为空时生成一次性随机密钥（仅适合本地开发，重启后旧 token 失效）
var jwtSecret = builder.Configuration["Jwt:SecretKey"];
var jwtSecretGenerated = false;
if (string.IsNullOrWhiteSpace(jwtSecret))
{
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



var app = builder.Build();

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
		// 自动建表 / 结构补齐：受 DatabaseInitialization.Enabled 控制（生产环境建议关闭）
		var dbInit = configuration.GetSection("DatabaseInitialization");
		if (dbInit.GetValue<bool>("Enabled"))
		{
			if (dbInit.GetValue<bool>("CreateTablesOnStartup"))
			{
				dbServer.CreateTablesInDb(DbNames.Asset);
			}
			DbSchemaPatcher.Patch(dbServer.Use(DbNames.Asset));
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

//app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}

// 启用静态文件访问（资料附件存放于 wwwroot/uploads，用于图片预览等）
app.UseStaticFiles();

// 跨域（必须在认证之前）
app.UseCors("AllowFrontend");

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
