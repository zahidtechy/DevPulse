using DevPulse.Application;
using DevPulse.Infrastructure;
using DevPulse.Shared.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => AppJsonOptions.Configure(options.JsonSerializerOptions));
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(DevPulse.Shared.Constants.AppRoles.Admin));
    options.AddPolicy("CanViewReports", policy =>
        policy.RequireRole(
            DevPulse.Shared.Constants.AppRoles.Admin,
            DevPulse.Shared.Constants.AppRoles.User));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevPulseCors", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["https://localhost:7062", "http://localhost:5080"]);
    });
});

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("DevPulseCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
