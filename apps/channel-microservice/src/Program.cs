using ChannelService.Application;
using ChannelService.Infrastructure;
using ChannelService.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddMessageClient(builder.Configuration);
builder.Services.AddChannelApplication();
builder.Services.AddChannelInfrastructure();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "Channel Service"
}));

app.MapControllers();

app.Run();
