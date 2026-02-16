using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Data;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using Task_Management.Data;
System.Diagnostics.Trace.AutoFlush = true;


var builder = WebApplication.CreateBuilder(args);

Tracer.TaskManagerTrace.Switch.Level = SourceLevels.All;
Tracer.TaskManagerTrace.Listeners.Add(new TextWriterTraceListener("logs/taskmanagementTrace.log"));

Tracer.TaskManagerTrace.Flush();

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                formatter: new JsonFormatter(),
                path: "logs/taskmanagementLogs.log",
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
public static class Tracer
{
    public static TraceSource TaskManagerTrace = new TraceSource("TaskManagerTrace", SourceLevels.Verbose);
}