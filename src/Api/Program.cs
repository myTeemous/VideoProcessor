using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using VideoProcessor.Api.Common;
using VideoProcessor.Core;
using VideoProcessor.Core.Data;
using VideoProcessor.Core.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCore();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddRequestValidation();

// Every endpoint requires an authenticated User unless it opts out with AllowAnonymous.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services
    .AddIdentityApiEndpoints<User>()
    .AddEntityFrameworkStores<AppDbContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
    }

    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

var auth = app.MapGroup("/auth").WithTags("Auth");
auth.MapIdentityApi<User>();
// Identity's register/login/refresh carry no authorization metadata, so the fallback policy would block them.
// Its /manage endpoints require authorization explicitly; everything else in the group is public.
((IEndpointConventionBuilder)auth).Finally(endpoint =>
{
    if (!endpoint.Metadata.OfType<IAuthorizeData>().Any())
    {
        endpoint.Metadata.Add(new AllowAnonymousAttribute());
    }
});

await app.RunAsync();
