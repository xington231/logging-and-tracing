using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using Task_Management.Data;

var builder = WebApplication.CreateBuilder(args);

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