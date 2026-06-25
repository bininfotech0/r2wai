namespace R2WAI.Api.Tests;

public class StartupDiagnosticTest
{
    [Fact]
    public void Server_Starts_Successfully()
    {
        Exception? startupException = null;
        try
        {
            using var factory = new R2WAIWebApplicationFactory();
            using var client = factory.CreateClient();
        }
        catch (Exception ex)
        {
            startupException = ex;
            // Unwrap the full exception chain
            var current = ex;
            var message = "";
            while (current != null)
            {
                message += $"\n--- {current.GetType().Name}: {current.Message}";
                current = current.InnerException;
            }
            Assert.Fail($"Server failed to start:{message}");
        }

        Assert.Null(startupException);
    }
}
