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


var builder = WebApplication.CreateBuilder(args);

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
