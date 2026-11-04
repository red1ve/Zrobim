using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Zrobim.Api.Auth;
using Zrobim.Api.Bookings;
using Zrobim.Api.Data;
using Zrobim.Api.Infrastructure;
using Zrobim.Api.Payments;
using Zrobim.Api.Schedule;

// local secrets (connection string, JWT signing key) from C:\Zrobim\.env; real environment variables win
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // enums travel as the same UPPER_SNAKE_CASE labels as in the database: "AWAITING_PAYMENT"
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
        // malformed JSON gets a generic message instead of internal type names
        o.AllowInputFormatterExceptionMessages = false;
    });
builder.Services.AddProblemDetails();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddZrobimDatabase(builder.Configuration);
builder.Services.AddZrobimAuth(builder.Configuration);
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddHostedService<BookingExpirationWorker>();
builder.Services.Configure<DemoPaymentOptions>(builder.Configuration.GetSection("DemoPayment"));
builder.Services.AddHealthChecks().AddDbContextCheck<ZrobimDbContext>("database");

var app = builder.Build();
await app.InitializeDemoAsync();

app.UseExceptionHandler();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
