using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Security;

public class ApiInputBoundaryTests : IntegrationTestBase
{
    public ApiInputBoundaryTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTH BOUNDARY TESTS
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    public async Task Login_ShortPassword_RejectsGracefully(string shortPassword)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "test@example.com",
            Password = shortPassword
        });
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized,
            $"Short password should be rejected, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Login_ExtremelyLongPassword_HandledSafely()
    {
        var longPassword = new string('A', 10_000);
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = "test@example.com",
            Password = longPassword
        });
        Assert.True((int)response.StatusCode < 500,
            "Extremely long password should not cause server error");
    }

    [Fact]
    public async Task Login_ExtremelyLongEmail_HandledSafely()
    {
        var longEmail = new string('a', 5000) + "@example.com";
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = longEmail,
            Password = "test123"
        });
        Assert.True((int)response.StatusCode < 500,
            "Extremely long email should not cause server error");
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("@missing-local.com")]
    [InlineData("missing-domain@")]
    [InlineData("spaces in@email.com")]
    [InlineData("double@@at.com")]
    public async Task Login_MalformedEmail_RejectsGracefully(string badEmail)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = badEmail,
            Password = "test123"
        });
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized,
            $"Malformed email '{badEmail}' should be rejected, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  FORGOT PASSWORD BOUNDARY TESTS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ForgotPassword_EmptyEmail_HandledSafely()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            Email = ""
        });
        Assert.True((int)response.StatusCode < 500,
            "Empty email should not cause server error");
    }

    [Fact]
    public async Task ForgotPassword_NonExistentEmail_DoesNotRevealExistence()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            Email = "definitely-not-real@nonexistent.com"
        });
        // Should return 200 OK regardless to prevent user enumeration
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RESET PASSWORD BOUNDARY TESTS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ResetPassword_EmptyToken_RejectsGracefully()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            Email = "admin@r2wai.io",
            Token = "",
            NewPassword = "NewPassword123!"
        });
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound,
            $"Empty reset token should be rejected, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_RejectsOrHandles()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            Email = "admin@r2wai.io",
            Token = "VALID_TOKEN",
            NewPassword = "1"
        });
        Assert.True((int)response.StatusCode < 500,
            "Weak password should not cause server error");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DOCUMENT UPLOAD BOUNDARY TESTS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DocumentUpload_EmptyFile_RejectsGracefully()
    {
        await Factory.EnsureSeededAsync();
        var authClient = await GetAuthenticatedClientAsync();
        if (authClient.DefaultRequestHeaders.Authorization == null) return;

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Array.Empty<byte>()), "file", "empty.txt");

        var response = await authClient.PostAsync("/api/v1/documents/upload", content);
        Assert.True((int)response.StatusCode < 500,
            "Empty file upload should not cause server error");
    }

    [Fact]
    public async Task DocumentUpload_DangerousFilename_HandledSafely()
    {
        await Factory.EnsureSeededAsync();
        var authClient = await GetAuthenticatedClientAsync();
        if (authClient.DefaultRequestHeaders.Authorization == null) return;

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("test content"u8.ToArray()), "file", "../../../etc/passwd");

        var response = await authClient.PostAsync("/api/v1/documents/upload", content);
        Assert.True((int)response.StatusCode < 500,
            "Path traversal filename should not cause server error");
    }

    [Fact]
    public async Task DocumentUpload_ExecutableExtension_RejectsOrHandles()
    {
        await Factory.EnsureSeededAsync();
        var authClient = await GetAuthenticatedClientAsync();
        if (authClient.DefaultRequestHeaders.Authorization == null) return;

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("MZ"u8.ToArray()), "file", "malware.exe");

        var response = await authClient.PostAsync("/api/v1/documents/upload", content);
        Assert.True((int)response.StatusCode < 500,
            "Executable file upload should not cause server error");
    }

    [Fact]
    public async Task DocumentUpload_DoubleExtension_HandledSafely()
    {
        await Factory.EnsureSeededAsync();
        var authClient = await GetAuthenticatedClientAsync();
        if (authClient.DefaultRequestHeaders.Authorization == null) return;

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("test"u8.ToArray()), "file", "report.pdf.exe");

        var response = await authClient.PostAsync("/api/v1/documents/upload", content);
        Assert.True((int)response.StatusCode < 500,
            "Double extension file should not cause server error");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GUID PARAMETER BOUNDARY TESTS
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidGuidParameter_HandledSafely(string badGuid)
    {
        var response = await Client.GetAsync($"/api/v1/documents/{badGuid}");
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound
                or HttpStatusCode.Unauthorized,
            $"Invalid GUID '{badGuid}' should be handled safely, got {(int)response.StatusCode}");
    }

    [Theory]
    [InlineData("/api/v1/workflows/not-a-guid")]
    [InlineData("/api/v1/chatbots/12345")]
    [InlineData("/api/v1/assistants/invalid")]
    [InlineData("/api/v1/knowledge-bases/xyz")]
    public async Task InvalidGuidInPath_DoesNotCauseServerError(string path)
    {
        var response = await Client.GetAsync(path);
        Assert.True((int)response.StatusCode < 500,
            $"Invalid GUID in path {path} should not cause server error, got {(int)response.StatusCode}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAGINATION BOUNDARY TESTS
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, -1)]
    [InlineData(0, 0)]
    [InlineData(0, 100000)]
    public async Task InvalidPagination_HandledSafely(int page, int pageSize)
    {
        var response = await Client.GetAsync($"/api/v1/documents?page={page}&pageSize={pageSize}");
        Assert.True((int)response.StatusCode < 500,
            $"Invalid pagination (page={page}, pageSize={pageSize}) should not cause server error");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CONTENT TYPE BOUNDARY TESTS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Login_WrongContentType_HandledSafely()
    {
        var content = new StringContent("email=test&password=test",
            System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await Client.PostAsync("/api/v1/auth/login", content);

        Assert.True((int)response.StatusCode < 500,
            "Wrong content type should not cause server error");
    }

    [Fact]
    public async Task Login_XmlContent_HandledSafely()
    {
        var content = new StringContent(
            "<login><email>test@test.com</email><password>test</password></login>",
            System.Text.Encoding.UTF8, "application/xml");
        var response = await Client.PostAsync("/api/v1/auth/login", content);

        Assert.True((int)response.StatusCode < 500,
            "XML content type should not cause server error");
    }

    [Fact]
    public async Task Login_PlainTextContent_HandledSafely()
    {
        var content = new StringContent("just plain text",
            System.Text.Encoding.UTF8, "text/plain");
        var response = await Client.PostAsync("/api/v1/auth/login", content);

        Assert.True((int)response.StatusCode < 500,
            "Plain text content should not cause server error");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  UNICODE & ENCODING BOUNDARY TESTS
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("test@例え.com", "パスワード")]
    [InlineData("тест@тест.com", "пароль")]
    [InlineData("test@test.com", "🔐🔑🔓")]
    [InlineData("test@test.com", "\0\0\0")]
    public async Task Login_UnicodeInput_HandledSafely(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = password
        });
        Assert.True((int)response.StatusCode < 500,
            $"Unicode input should not cause server error, got {(int)response.StatusCode}");
    }
}
