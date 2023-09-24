using core6.Models;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace core6.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching",
        };

        private static readonly HttpClient HttpClient = new();

        private readonly ILogger<WeatherForecastController> logger;
        private readonly IConfiguration configuration;

        public WeatherForecastController(ILogger<WeatherForecastController> logger , IConfiguration configuration)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            var tracer = TracerProvider.Default.GetTracer(nameof(WeatherForecast));
            using var parent = tracer.StartSpan("parent");
            using (var child1 = tracer.StartSpan("child1"))
            {
                // do some work that 'child1' tracks
                child1.AddEvent("child1 test");
                using (var child2 = tracer.StartSpan("child2"))
                {
                    // do some work that 'child2' tracks
                    child2.AddEvent("child2 test");
                }
            }
            using var span = tracer.StartActiveSpan("Google");
            span.AddEvent("test");
            span.SetAttribute("test1", "test2");
            var attributeData = new Dictionary<string, object>
            {
                {"foo", 1 },
                { "bar", "Hello, World!" },
                { "baz", new int[] { 1, 2, 3 } }
            };
            span.AddEvent("asdf", DateTimeOffset.Now, new(attributeData));

            try
            {
                var meter = new Meter(nameof(WeatherForecast));
                var MyFruitCounter = meter.CreateCounter<long>(name: "compute_requests",description: "compute_requests for test");
                MyFruitCounter.Add(1, new("name", "apple2"), new("color", "red2"));
                MyFruitCounter.Add(2, new("name", "lemon"), new("color", "yellow"));
                MyFruitCounter.Add(1, new("name", "lemon"), new("color", "yellow"));
            } catch (Exception ex)
            {
                // span.SetStatus(Status.Error);
                span.RecordException(ex);
                span.SetStatus(Status.Error.WithDescription(ex.Message));
            }

            var ctx = span.Context;
            var links = new List<Link>
                {
                    new(ctx)
                };
            using var span2 = tracer.StartActiveSpan("final", links: links);
            span.AddEvent("final");
            span.SetAttribute("test3", "test4");


            // span.SetStatus(Status.Error);

            /*using var span = Tracer.StartActiveSpan("hello-span");
            activity?.SetTag("foo", 1);
            activity?.SetTag("bar", "Hello, World!");
            activity?.SetTag("baz", new int[] { 1, 2, 3 });*/

            /*using var scope = this.logger.BeginScope("{Id}", Guid.NewGuid().ToString("N"));
            var myMeter = new Meter("TestMeter");
            var counter = myMeter.CreateCounter<long>("TestCounter");
            counter.Add(1, new("name", "apple"), new("color", "red"));
            counter.Add(2, new("name", "lemon"), new("color", "yellow"));
            counter.Add(1, new("name", "lemon"), new("color", "yellow"));
            counter.Add(2, new("name", "apple"), new("color", "green"));
            counter.Add(5, new("name", "apple"), new("color", "red"));
            counter.Add(4, new("name", "lemon"), new("color", "yellow"));*/

            // Making an http call here to serve as an example of
            // how dependency calls will be captured and treated
            // automatically as child of incoming request.
            var res = HttpClient.GetStringAsync("http://google.com").Result;
            var rng = new Random();
            var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)],
            })
            .ToArray();

            this.logger.LogInformation(
                "WeatherForecasts generated {count}: {forecasts}",
                forecast.Length,
                forecast);

            this.logger.LogWarning("test send log");

            //throw new AppException("Email or password is incorrect2");
            //throw new KeyNotFoundException("Email or password is incorrect2");

            return forecast;
        }
    }
}