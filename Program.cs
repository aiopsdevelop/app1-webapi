using core6.Controllers;
using core6.Healthy;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// ====================================================================================================
//                                   Add services to DI container
// ----------------------------------------------------------------------------------------------------

var Otlp_Endpoint_Default = "http://hybridlog.io:4344";
var Use_Exporter_Default = "otlp";
var serviceName = Environment.GetEnvironmentVariable("PROJECT_NAME") ?? "application_api1";
string serviceVersion = Environment.GetEnvironmentVariable("PROJECT_VERSION") ?? "1.0.0";

string JIRA_PROJECT_ID = Environment.GetEnvironmentVariable("JIRA_PROJECT_ID") ?? "1";
string IMAGE = Environment.GetEnvironmentVariable("IMAGE") ?? "localhost";
string TEMPLATE_NAME = Environment.GetEnvironmentVariable("TEMPLATE_NAME") ?? "dotnetcore6";
string STAGE = Environment.GetEnvironmentVariable("STAGE") ?? "production";
string TEAM_NAME = Environment.GetEnvironmentVariable("TEAM_NAME") ?? "web_backend"; // or TEAM_NAME=logic,web_front,devops,it,pm,po,mobile,qa,database,creep,...
string ContainerName = Dns.GetHostName();
string HOST_ID = Environment.GetEnvironmentVariable("HOST_ID") ?? "localhostId";
string HOST_NAME = Environment.GetEnvironmentVariable("HOST_NAME") ?? "localhost";
string SUBDOMAIN = Environment.GetEnvironmentVariable("SUBDOMAIN") ?? "localhost";
string HOST_TYPE = Environment.GetEnvironmentVariable("HOST_TYPE") ?? "arm64";
string OS_NAME = Environment.GetEnvironmentVariable("OS_NAME") ?? "windows";
string OS_VERSION = Environment.GetEnvironmentVariable("OS_VERSION") ?? "2010";
string CRM_KEY = Environment.GetEnvironmentVariable("CRM_KEY") ?? "HW-511";
string SERVICE_NAMESPACE = Environment.GetEnvironmentVariable("SERVICE_NAMESPACE") ?? "devops";

// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/semantic_conventions/README.md
Action<ResourceBuilder> configureResource = r =>
{
    r.AddService(serviceName, serviceVersion: serviceVersion, serviceInstanceId: Environment.MachineName);
    //r.AddService("Redis", serviceVersion: "1.0.0", serviceInstanceId: Environment.MachineName);
    r.AddAttributes(new Dictionary<string, object>
    {
        ["environment.name"] = STAGE,
        ["deployment.environment"] = STAGE, // staging
        ["team.name"] = TEAM_NAME,
        ["team.user"] = Environment.UserName,
        ["host.id"] = HOST_ID,
        ["host.name"] = HOST_NAME,
        ["host.hostname"] = SUBDOMAIN,
        ["host.type"] = HOST_TYPE,
        ["os.name"] = OS_NAME,
        ["os.version"] = OS_VERSION,
        ["issue.project.id"] = JIRA_PROJECT_ID,
        ["issue.crm.key"] = CRM_KEY,
        ["service.namespace"] = SERVICE_NAMESPACE,
        ["telemetry.sdk.language"] = "dotnet",
        ["telemetry.sdk.name"] = "opentelemetry",
        ["container.runtime"] = "docker",
        ["container.name"] = ContainerName,
        ["container.image.name"] = IMAGE,
        ["container.image.tag"] = serviceVersion,
        ["service.template"] = TEMPLATE_NAME
    });
};

// -----------------------------------------------
//                     TRACE
// -----------------------------------------------

var tracingExporter = builder.Configuration.GetValue<string>("UseTracingExporter")??Use_Exporter_Default.ToLowerInvariant();
builder.Services.AddHttpClient();

builder.Services.AddOpenTelemetryTracing(options =>
{
    options
        //.AddConsoleExporter()
        .ConfigureResource(configureResource)
        .SetSampler(new AlwaysOnSampler())
        .AddHttpClientInstrumentation()
        .AddSource(nameof(WeatherForecast))
        .AddSource(nameof(PublishMessageController))
        .AddAspNetCoreInstrumentation();

    switch (tracingExporter)
    {
        case "otlp":
            options.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue<string>("Otlp:Endpoint")??Otlp_Endpoint_Default);
            });
            break;

        default:
            options.AddConsoleExporter();
            break;
    }
});

// -----------------------------------------------
//                     LOG
// -----------------------------------------------
// For options which can be bound from IConfiguration.

builder.Services.Configure<AspNetCoreInstrumentationOptions>(builder.Configuration.GetSection("AspNetCoreInstrumentation"));

builder.Logging.ClearProviders();

builder.Logging.AddOpenTelemetry(options =>
{
    options.ConfigureResource(configureResource);

    // Switch between Console/OTLP by setting UseLogExporter in appsettings.json.
    var logExporter = builder.Configuration.GetValue<string>("UseLogExporter")??Use_Exporter_Default.ToLowerInvariant();
    switch (logExporter)
    {
        case "otlp":
            options.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue<string>("Otlp:Endpoint")??Otlp_Endpoint_Default);
                otlpOptions.Protocol = OtlpExportProtocol.Grpc;
            });
            break;
        default:
            options.AddConsoleExporter();
            break;
    }
});

builder.Services.Configure<OpenTelemetryLoggerOptions>(opt =>
{
    opt.IncludeScopes = true;
    opt.ParseStateValues = true;
    opt.IncludeFormattedMessage = true;
});

// -----------------------------------------------
//                     Metrics
// -----------------------------------------------
// Switch between Prometheus/OTLP/Console by setting UseMetricsExporter in appsettings.json.

var metricsExporter = builder.Configuration.GetValue<string>("UseMetricsExporter")??Otlp_Endpoint_Default.ToLowerInvariant();

var meter = new Meter(nameof(WeatherForecast));
builder.Services.AddOpenTelemetryMetrics(options =>
{
    options.ConfigureResource(configureResource)
        .AddMeter(meter.Name)
        .AddRuntimeInstrumentation()
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation();

    switch (metricsExporter)
    {
        case "otlp":
            options.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue<string>("Otlp:Endpoint")??Otlp_Endpoint_Default);
                otlpOptions.Protocol = OtlpExportProtocol.Grpc;
            });
            break;
        default:
            options.AddConsoleExporter();
            break;
    }
});

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

// -----------------------------------------------
//                 Health Check
// -----------------------------------------------
// https://andrewlock.net/deploying-asp-net-core-applications-to-kubernetes-part-6-adding-health-checks-with-liveness-readiness-and-startup-probes/

string rabbitConnectionString = $"amqp://{builder.Configuration["RabbitMq:Username"]}:{builder.Configuration["RabbitMq:Password"]}@{builder.Configuration["RabbitMq:Host"]}:5672/";
builder.Services
    .AddHealthChecks()
    .AddCheck<App3HealthCheck>("App3 check", tags: new[] { "services", "test", "app1", "app3" })
    .AddApplicationInsightsPublisher()
    .AddRabbitMQ(rabbitConnectionString: rabbitConnectionString, tags: new[] { "services","test" });

// -----------------------------------------------
//                   Swagger
// -----------------------------------------------

builder.Services.AddSwaggerGen();

var app = builder.Build();
var MyActivitySource = new ActivitySource(nameof(PublishMessageController));
// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

// -----------------------------------------------
//             Middleware Config
// -----------------------------------------------

//app.UseOpenTelemetryPrometheusScrapingEndpoint();

//app.UseMiddleware<ErrorHandlerMiddleware>();

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseRouting();

/*app.MapHealthChecks("/health/startup");
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/ready");*/
app.UseEndpoints(config =>
{
    config.MapHealthChecks("/health/startup", new HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
});
app.UseEndpoints(config =>
{
    config.MapHealthChecks("/healthz", new HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
});
app.UseEndpoints(config =>
 {
     config.MapHealthChecks("/ready", new HealthCheckOptions
     {
         Predicate = _ => true,
         ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
     });
 });

app.MapControllers();

/*app.Use(async (context, next) =>
{
    if (context.Request.Path == "/healthz")
    {
        Console.WriteLine($"test 1 ... {context.Request.Path}");
    }

    Console.WriteLine($"test 2 ... {context.Request.Path}");

    await next(context);
});*/

/*app.MapGet("/health", () =>
{
    // Track work inside of the request
    using var activity = MyActivitySource.StartActivity("SayHello");
    activity?.SetTag("foo", 1);
    activity?.SetTag("bar", "Hello, World!");
    activity?.SetTag("baz", new int[] { 1, 2, 3 });

    return "Ok";
});*/

app.Run();
