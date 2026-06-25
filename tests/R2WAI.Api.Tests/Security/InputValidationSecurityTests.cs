using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace R2WAI.Api.Tests.Security;

public class InputValidationSecurityTests : IntegrationTestBase
{
    public InputValidationSecurityTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    // ═══════════════════════════════════════════════════════════════════
    //  XSS PREVENTION
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("'\"><script>alert(document.cookie)</script>")]
    public async Task Login_XssInEmail_DoesNotReflectUnescaped(string xssPayload)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = xssPayload,
            Password = "test"
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script>", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onload=", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("'\"><img src=x onerror=alert(1)>")]
    public async Task Login_XssInPassword_DoesNotReflect(string xssPayload)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "test@example.com",
            Password = xssPayload
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script>", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror=", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public async Task ForgotPassword_XssInEmail_DoesNotReflect(string xssPayload)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            Email = xssPayload
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script>", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public async Task ResetPassword_XssInFields_DoesNotReflect(string xssPayload)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            Email = xssPayload,
            Token = xssPayload,
            NewPassword = "ValidPassword123!"
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script>", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror=", body, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SQL INJECTION PREVENTION
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("' UNION SELECT * FROM users --")]
    [InlineData("1; DELETE FROM users")]
    [InlineData("admin'--")]
    public async Task Login_SqlInjectionInEmail_RejectsGracefully(string sqlPayload)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = sqlPayload,
            Password = "test"
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized,
            $"SQL injection attempt should be rejected, got {(int)response.StatusCode}");
    }

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("' UNION SELECT * FROM users --")]
    public async Task Login_SqlInjectionInPassword_RejectsGracefully(string sqlPayload)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "test@example.com",
            Password = sqlPayload
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized,
            $"SQL injection attempt should be rejected, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PATH TRAVERSAL PREVENTION
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("....//....//....//etc/passwd")]
    [InlineData("%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd")]
    public async Task Documents_PathTraversal_IsRejected(string pathPayload)
    {
        var response = await Client.GetAsync($"/api/v1/documents/{pathPayload}");
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound
                or HttpStatusCode.BadRequest,
            $"Path traversal should be rejected, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HEADER INJECTION PREVENTION
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Login_HeaderInjection_InContentType_HandledSafely()
    {
        // .NET HttpClient rejects CRLF in headers at the framework level,
        // verifying the server also doesn't reflect injected headers via query params
        var response = await Client.GetAsync("/api/v1/auth/me?x=%0d%0aX-Injected:%20true");
        Assert.False(response.Headers.Contains("X-Injected"),
            "Header injection via URL encoding should not be reflected");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  OVERSIZED PAYLOAD PREVENTION
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Login_OversizedPayload_IsRejected()
    {
        var largeString = new string('A', 1_000_000);
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = largeString,
            Password = largeString
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                or HttpStatusCode.RequestEntityTooLarge,
            $"Oversized payload should be rejected, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Api_DeeplyNestedJson_DoesNotCrash()
    {
        var nested = new StringBuilder("{");
        for (int i = 0; i < 100; i++)
            nested.Append("\"a\":{");
        nested.Append("\"v\":1");
        for (int i = 0; i < 100; i++)
            nested.Append('}');
        nested.Append('}');

        var content = new StringContent(nested.ToString(), Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/v1/auth/login", content);

        Assert.True((int)response.StatusCode < 500,
            "Deeply nested JSON should not cause server error");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SPECIAL CHARACTER HANDLING
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("test@例え.com")]
    [InlineData("test@тест.com")]
    [InlineData("test+tag@example.com")]
    [InlineData("\"quoted\"@example.com")]
    public async Task Login_SpecialCharactersInEmail_HandledSafely(string email)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = "test"
        });

        Assert.True((int)response.StatusCode < 500,
            $"Special characters in email should not cause server error, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Login_NullFields_HandledSafely()
    {
        var content = new StringContent(
            "{\"email\":null,\"password\":null}",
            Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/v1/auth/login", content);

        Assert.True((int)response.StatusCode < 500,
            "Null fields should not cause server error");
    }

    [Fact]
    public async Task Login_EmptyStringFields_HandledSafely()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "",
            Password = ""
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized,
            "Empty strings should return client error");
    }

    [Fact]
    public async Task Login_WhitespaceOnlyFields_HandledSafely()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "   ",
            Password = "   "
        });

        Assert.True((int)response.StatusCode < 500,
            "Whitespace-only fields should not cause server error");
    }
}
