using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using Task_Management.Data;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()                   
            .WriteTo.Console()                      
            .WriteTo.File("logs/taskmanagement.log",         
                rollingInterval: RollingInterval.Day) 
            .CreateLogger();
builder.Host.UseSerilog();


builder.Services.AddControllersWithViews();

string connectionString = builder.Configuration.GetConnectionString("task_management");
builder.Services.AddScoped<IDbConnection>(_ =>
    new NpgsqlConnection(connectionString));

builder.Services.AddDbContext<TaskManagementDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles(); 
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();