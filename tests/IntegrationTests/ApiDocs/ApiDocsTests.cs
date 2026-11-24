using System.Net;
using VideoProcessor.IntegrationTests.Harness;

namespace VideoProcessor.IntegrationTests.ApiDocs;

[Collection(AppCollection.Name)]
public sealed class ApiDocsTests(AppFixture app)
{
    [Fact]
    public async Task OpenApi_document_lists_the_auth_endpoints()
    {
        var document = await app.CreateClient().GetStringAsync("/openapi/v1.json");

        document.Should().Contain("/auth/register").And.Contain("/auth/login").And.Contain("/auth/refresh");
    }

    [Fact]
    public async Task Scalar_ui_is_served_in_development()
    {
        var response = await app.CreateClient().GetAsync("/scalar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }
}
