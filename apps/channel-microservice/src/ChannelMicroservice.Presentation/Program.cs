using ChannelService;
using ChannelService.Application;
using ChannelService.Authentication;
using ChannelService.GraphQL;
using ChannelService.Infrastructure;
using ChannelService.Infrastructure.Secrets;
using MessageClient;

var builder = WebApplication.CreateBuilder(args);

await builder.Configuration.AddSecretsFromVaultAsync();

builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ReliabilityExceptionHandler>();

builder.Services.AddServiceAuthentication(builder.Configuration);
builder.Services.AddMessageClient(builder.Configuration);
builder.Services.AddChannelApplication();
builder.Services.AddChannelInfrastructure();
builder.Services.AddChannelGraphQL();

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "Channel Service"
}));

app.MapControllers();
app.MapGraphQL("/graphql").RequireAuthorization();

app.Run();
