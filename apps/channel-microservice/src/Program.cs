using ChannelService.Messaging;
using ChannelService.Data;
using ChannelService.Models;
using ChannelService.Service;
using Microsoft.EntityFrameworkCore;
using SharedModels;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddDbContext<ChannelContext>(opt => opt.UseInMemoryDatabase("ChannelsDb"));
builder.Services.AddScoped<IChannelRepository, ChannelRepository>();
builder.Services.AddTransient<IDbInitializer, DbInitializer>();
builder.Services.AddSingleton<IConverter<Channel, ChannelDto>, ChannelConverter>();
builder.Services.AddScoped<IChannelService, ChannelService.Service.ChannelService>();
builder.Services.AddOpenApi();
builder.Services.AddMessageClient(builder.Configuration);
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ChannelContext>();
    var dbInitializer = services.GetRequiredService<IDbInitializer>();
    dbInitializer.Initialize(dbContext);
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "Bizcord microservice"
}));

app.Run();
