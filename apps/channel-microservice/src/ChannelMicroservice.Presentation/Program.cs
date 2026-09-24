using ChannelService.Application;
using ChannelService.Infrastructure;
using MessageClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Services.AddMessageClient(builder.Configuration);
builder.Services.AddChannelApplication();
builder.Services.AddChannelInfrastructure();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "Channel Service"
}));

app.MapControllers();

app.Run();
