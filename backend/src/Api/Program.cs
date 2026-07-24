using RecruitOps.Application.Common;
using RecruitOps.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Infrastructure (EF Core + Postgres). Connection string in appsettings ("Default").
builder.Services.AddInfrastructure(builder.Configuration);

// TODO: replace this stub tenant resolver with real resolution from the
// authenticated principal / subdomain / header (Module 1).
builder.Services.AddScoped<ICurrentTenant, StubCurrentTenant>();

// CORS for the Next.js dev server.
const string DevCors = "DevCors";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p =>
    p.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(DevCors);
app.UseAuthorization();
app.MapControllers();

app.Run();

// TODO: remove — placeholder so the app compiles before auth is wired up.
internal sealed class StubCurrentTenant : ICurrentTenant
{
    public Guid TenantId { get; } = Guid.Empty;
}
