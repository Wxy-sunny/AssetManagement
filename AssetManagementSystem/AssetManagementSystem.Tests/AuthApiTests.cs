using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AssetManagementSystem.Tests;

/// <summary>
/// 免数据库的接口级集成测试：
/// 这些用例都刻意停在"访问数据库之前"的校验环节（验证码 / 登录态），
/// 因此不需要真实数据库也能运行（CI 友好）。
/// 注意：服务端路由不带 /api 前缀（/api 是前端开发代理约定）。
/// </summary>
public class AuthApiTests : IClassFixture<WebApplicationFactory<Program>>
{
	private readonly HttpClient _client;

	public AuthApiTests(WebApplicationFactory<Program> factory)
	{
		_client = factory.CreateClient();
	}

	[Fact]
	public async Task GetCaptcha_ReturnsIdAndSvg()
	{
		var response = await _client.GetAsync("/auth/captcha");

		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		Assert.Equal(200, body.GetProperty("code").GetInt32());

		var data = body.GetProperty("data");
		Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("captchaId").GetString()));
		Assert.StartsWith("data:image/svg+xml;base64,", data.GetProperty("imageBase64").GetString());
	}

	[Fact]
	public async Task Login_WithoutCaptcha_IsRejected_BeforeTouchingDatabase()
	{
		var response = await _client.PostAsJsonAsync("/auth/login", new
		{
			username = "admin",
			password = "whatever"
		});

		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		// 验证码校验发生在查询用户之前：返回业务 400 即证明防爆破第一层生效
		Assert.Equal(400, body.GetProperty("code").GetInt32());
	}

	[Fact]
	public async Task Login_WithEmptyCredentials_Returns400()
	{
		var response = await _client.PostAsJsonAsync("/auth/login", new { username = "", password = "" });

		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		Assert.Equal(400, body.GetProperty("code").GetInt32());
	}

	[Fact]
	public async Task SubmitRequest_WithoutCaptcha_IsRejected()
	{
		var response = await _client.PostAsJsonAsync("/request/submit", new
		{
			account = "someone",
			content = "我忘记了登录密码，需要重置"
		});

		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		Assert.Equal(400, body.GetProperty("code").GetInt32());
	}

	[Fact]
	public async Task ProtectedEndpoints_WithoutToken_Return401()
	{
		var messagePage = await _client.GetAsync("/message/page");
		var requestPage = await _client.GetAsync("/request/page");

		Assert.Equal(HttpStatusCode.Unauthorized, messagePage.StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, requestPage.StatusCode);
	}

	[Fact]
	public async Task Refresh_WithoutToken_ReturnsBusinessError()
	{
		var response = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = "" });

		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		Assert.Equal(400, body.GetProperty("code").GetInt32());
	}
}
