using GrpcService.Services;
using GrpcService;
using GrpcService.HKSDK.service;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// 配置Kestrel服务器
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // HTTP/2 配置用于gRPC
    serverOptions.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });

    // 配置限制
    serverOptions.Limits.MaxConcurrentConnections = 1000;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 1000;
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});
// 添加gRPC服务
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = 10 * 1024 * 1024; // 10MB
    options.MaxSendMessageSize = 10 * 1024 * 1024;    // 10MB
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.Configure<DeviceManagerOptions>(
    builder.Configuration.GetSection("DeviceManager"));

// 注册服务
builder.Services.AddSingleton<CMSService>();
builder.Services.AddSingleton<OptimizedDeviceManager>();
builder.Services.AddHostedService<OptimizedDeviceManager>(provider =>
    provider.GetRequiredService<OptimizedDeviceManager>());

builder.Services.AddHealthChecks()
    .AddCheck<DeviceManagerHealthCheck>("device_manager")
    .AddCheck<CMSServiceHealthCheck>("cms_service");
// 配置日志
builder.Services.AddLogging(configure =>
{
    configure.AddConsole();
    configure.AddDebug();
    configure.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<HikDeviceService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
