using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry;
using System.Diagnostics;

namespace core6.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MetricTestController : ControllerBase
    {

        private static readonly ActivitySource activitySource = new(nameof(PublishMessageController));
        // private static ActivitySource activityRedisSource = new ActivitySource("Redis", "1.0.0");

        [HttpGet]
        [Route("getMetrics")]
        public async Task<ActionResult<Metric>> GetMetric()
        {
            var activity = activitySource.StartActivity("Metric Route", ActivityKind.Server);
            activity?.SetTag("http.method", "GET");
            activity?.SetTag("otel.status_code", "ERROR");
            activity?.AddEvent(new ActivityEvent("Start"));
            await StepOne();
            activity?.SetTag("otel.status_description", "Use this text give more information about the error");
            activity?.AddEvent(new ActivityEvent("Done now"));
            Baggage.Current.SetBaggage("ExampleItem", "The information 1");
            await StepTwo();
            activity?.SetTag("http.route", "/metric/:id");
            activity?.AddEvent(
                new ActivityEvent(
                    "log",
                    DateTime.UtcNow,
                    new ActivityTagsCollection(
                        new Dictionary<string, object>
                        {
                            { "log.severity", "error" },
                            { "log.message", "User not found" },
                            { "enduser.id", 123 },
                        }
                    )
                )
            );
            var metrics = new List<Metric>()
            {
                new Metric()
                {
                    Id = 1,
                    Name = "test1"
                },
                new Metric()
                {
                    Id = 2,
                    Name = "test2",
                }
            };

            await DoSomeWork("banana", 8);
            Console.WriteLine("Example work done");
            var activity2 = activitySource.StartActivity(name: "Metric Route Client", kind: ActivityKind.Client);
            var infoFromContext = Baggage.Current.GetBaggage("ExampleItem");
            activity2?.SetTag("InfoServiceBReceived", infoFromContext);
            activity2?.SetTag("http.method", "GET");
            // activity2?.SetTag("otel.status_code", "ERROR");
            activity2?.AddEvent(new ActivityEvent("Done StepThree now"));
            await StepThree();
            activity2?.SetTag("otel.status_description", "Use this text give more information about the error");
            activity2?.SetTag("http.route", "/projects/:id");

            activity?.Stop();
            activity2?.Stop();
            return Ok(metrics);
        }

        static async Task DoSomeWork(string foo, int bar)
        {
            await StepOne();
            await StepTwo();
        }

        static async Task StepOne()
        {
            using (Activity activity = activitySource.StartActivity("StepOne"))
            {
                await Task.Delay(500);
            }
        }

        static async Task StepTwo()
        {
            using (Activity activity = activitySource.StartActivity("StepTwo"))
            {
                await Task.Delay(1000);
            }
        }

        static async Task StepThree()
        {
            using (Activity activity = activitySource.StartActivity("StepThree"))
            {
                await Task.Delay(1000);
            }
        }
    }
}
