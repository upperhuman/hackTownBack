using HackTownBack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5150); // HTTP
    options.Listen(IPAddress.Any, 7150, listenOptions =>
    {
        listenOptions.UseHttps(); // HTTPS /etc/
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      
                          policy
                                                  .AllowAnyHeader()
                                                  .AllowAnyMethod()
                                                  .AllowAnyOrigin()
                      );
});
//Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string dbHost = Environment.GetEnvironmentVariable("DATABASE_HOST") ?? "hacktowndb.flycast";
string dbPort = Environment.GetEnvironmentVariable("DATABASE_PORT") ?? "5432";
string dbName = Environment.GetEnvironmentVariable("DATABASE_NAME") ?? "hacktowndb";
string dbUser = Environment.GetEnvironmentVariable("DATABASE_USER") ?? "postgres";
string dbPassword = Environment.GetEnvironmentVariable("DATABASE_PASSWORD") ?? "4kwOXzyVEvPUcxr";

string connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";

builder.Services.AddDbContext<HackTownDbContext>(option => option.UseNpgsql("Host=hacktowndb.flycast;Port=5432;Database=postgres;Username=postgres;Password=4kwOXzyVEvPUcxr"));

Console.WriteLine($"Using connection string: {connectionString}");

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();
app.UseCors(MyAllowSpecificOrigins);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HackTownDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration error: {ex.Message}");
    }
}


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
