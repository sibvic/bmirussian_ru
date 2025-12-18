using BMIRussian_ru.Data;
using BMIRussian_ru.Logic;
using BMIRussian_ru.Services;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Sibvic.AuthLib;
using Sibvic.AuthLib.Logic;
using Sibvic.UserWithBalanceLib;
using Sibvic.UserWithBalanceLib.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<UserDBContext>(serviceProvider => 
    serviceProvider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<UserWithBalanceContext>(serviceProvider =>
    serviceProvider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.Configure<KafkaMessageSenderOptions>(
    builder.Configuration.GetSection("KafkaMessageSender"));
builder.Services.AddSingleton<KafkaMessageSender>();
builder.Services.AddSingleton<MetricsService>();
builder.Services.AddHostedService<LoginService>();

// Register AuthOptions
var jwtKey = builder.Configuration["JWT:KEY"] ?? throw new InvalidOperationException("JWT:KEY environment variable is required.");
var jwtIssuer = builder.Configuration["JWT:ISSUER"];
builder.Services.AddSingleton(new AuthOptions(jwtKey, jwtIssuer));

// Register AuthLogic
builder.Services.AddTransient<IAuthLogicCallback, WelcomeBalanceCallback>();
builder.Services.AddTransient<BalanceManager>();
builder.Services.AddScoped<AuthLogic>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("APIPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Prometheus metrics
app.UseMetricServer();
app.UseHttpMetrics();

app.UseRouting();

app.UseCors("APIPolicy");

app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
db.Database.Migrate();

app.Run();
