using GrpcService.Services;
using System;
using GrpcService.HKSDK;
using Serilog;
using GrpcService.Models;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.IO;


var builder = WebApplication.CreateBuilder(args);
// 常量提取
const int DefaultRetainedFileCountLimit = 30;
const int DefaultMaxReceiveMessageSize = 4194304;
const int DefaultMaxSendMessageSize = 4194304;
const int DefaultGrpcPort = 5000;
const int DefaultMaxConcurrentCalls = 100;
const string DefaultHost = "0.0.0.0";
const string LogDir = "logs";
const string LogFilePattern = ".log";

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(LogDir, LogFilePattern),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: DefaultRetainedFileCountLimit,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.Configure<GrpcServerConfiguration>(
    builder.Configuration.GetSection("GrpcServer"));
builder.Services.Configure<HikDeviceConfiguration>(
    builder.Configuration.GetSection("HikDevice"));
builder.Services.AddGrpc(options =>
{
    var grpcConfig = builder.Configuration.GetSection("GrpcServer").Get<GrpcServerConfiguration>();
    options.MaxReceiveMessageSize = grpcConfig?.MaxReceiveMessageSize ?? DefaultMaxReceiveMessageSize;
    options.MaxSendMessageSize = grpcConfig?.MaxSendMessageSize ?? DefaultMaxSendMessageSize;
});

builder.Services.AddSingleton<IDeviceLoggerService, DeviceLoggerService>();
builder.Services.AddSingleton<IGrpcRequestQueueService, GrpcRequestQueueService>();
builder.Services.AddSingleton<DeviceManager>();
builder.Services.AddSingleton<CMSService>();
builder.Services.AddSingleton<SubscribeEvent>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<CMSService>());

builder.Services.AddHostedService(provider => provider.GetService<DeviceManager>()!);
builder.Services.AddHostedService(provider =>
   provider.GetService<IGrpcRequestQueueService>() as GrpcRequestQueueService ??
   throw new InvalidOperationException("Unable to resolve GrpcRequestQueueService"));

builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    var grpcConfig = context.Configuration.GetSection("GrpcServer").Get<GrpcServerConfiguration>();
    var host = grpcConfig?.Host ?? DefaultHost;
    var port = grpcConfig?.Port ?? DefaultGrpcPort;

    serverOptions.Listen(System.Net.IPAddress.Parse(host), port, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
    serverOptions.Limits.MaxConcurrentConnections = grpcConfig?.MaxConcurrentCalls ?? DefaultMaxConcurrentCalls;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = grpcConfig?.MaxConcurrentCalls ?? DefaultMaxConcurrentCalls;
});

var app = builder.Build();

app.UseRouting();

app.MapGrpcService<HkDeviceService>();

try
{
    Log.Information("正在启动海康设备gRPC服务...");
    var grpcConfig = app.Configuration.GetSection("GrpcServer").Get<GrpcServerConfiguration>();
    var host = grpcConfig?.Host ?? DefaultHost;
    var port = grpcConfig?.Port ?? DefaultGrpcPort;
    Log.Information("gRPC服务地址 (HTTP/2, 明文): {Host}:{Port}", host, port);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "海康设备gRPC服务启动失败");
}
finally
{
    Log.CloseAndFlush();
}
