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

builder.Services.AddAuthorization();
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

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/auth")
    .WithTags("Auth")
    .MapIdentityApi<User>();

await app.RunAsync();
