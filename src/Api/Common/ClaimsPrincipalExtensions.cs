using System.Security.Claims;

namespace VideoProcessor.Api.Common;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The id of the authenticated User. Only call this on endpoints that require authentication.</summary>
    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("The request has no authenticated User.");
}
