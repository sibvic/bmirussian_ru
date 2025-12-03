using BMIRussian_ru.Data;
using BMIRussian_ru.Logic;
using BMIRussian_ru.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.Configure<KafkaMessageSenderOptions>(
    builder.Configuration.GetSection("KafkaMessageSender"));
builder.Services.AddSingleton<KafkaMessageSender>();
builder.Services.AddHostedService<LoginService>();

// Register AuthOptions
var jwtKey = builder.Configuration["JWT:KEY"] ?? throw new InvalidOperationException("JWT:KEY environment variable is required.");
var jwtIssuer = builder.Configuration["JWT:ISSUER"];
builder.Services.AddSingleton(new AuthOptions(jwtKey, jwtIssuer));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddRazorPages();

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

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
db.Database.Migrate();

app.Run();
