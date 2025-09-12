using KomReq_WebApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddWebApiServices();

var app = builder.Build();

app.ConfigureMiddleware();

await app.InitializeDatabaseAsync();

app.Run();