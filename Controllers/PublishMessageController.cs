using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace core6.Controllers
{
    [ApiController]
    [Route("publish-message")]
    public class PublishMessageController : ControllerBase
    {
        private static readonly ActivitySource Activity = new(nameof(PublishMessageController));
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

        private readonly ILogger<PublishMessageController> _logger;
        private readonly IConfiguration _configuration;

        public PublishMessageController(
            ILogger<PublishMessageController> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet]
        public void Get()
        {
            try
            {
                using (var activity = Activity.StartActivity("RabbitMq Publish", ActivityKind.Producer))
                {

                    var factory = new ConnectionFactory { HostName = _configuration["RabbitMq:Host"], UserName = _configuration["RabbitMq:Username"], Password = _configuration["RabbitMq:Password"] };
                    using (var connection = factory.CreateConnection())
                    using (var channel = connection.CreateModel())
                    {
                        var props = channel.CreateBasicProperties();

                        AddActivityToHeader(activity, props);

                        channel.QueueDeclare(queue: "sample",
                            durable: false,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);

                        var body = Encoding.UTF8.GetBytes("I am app1");

                        _logger.LogInformation("Publishing message to queue");

                        channel.BasicPublish(exchange: "",
                            routingKey: "sample",
                            basicProperties: props,
                            body: body);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error trying to publish a message", e);
                throw;
            }
        }

        private void AddActivityToHeader(Activity activity, IBasicProperties props)
        {
            Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props, InjectContextIntoHeader);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination_kind", "queue");
            activity?.SetTag("messaging.rabbitmq.queue", "sample");

            // var serviceName = "App1";
            var serviceName = Environment.GetEnvironmentVariable("PROJECT_NAME") ?? "application_api1";
            var meter = new Meter(serviceName);
            var MyFruitCounter = meter.CreateCounter<long>("compute_requests");
            MyFruitCounter.Add(1, new("name", "apple2"), new("color", "red2"));
            MyFruitCounter.Add(2, new("name", "lemon2"), new("color", "yellow2"));
            MyFruitCounter.Add(1, new("name", "lemon2"), new("color", "yellow2"));
        }

        private void InjectContextIntoHeader(IBasicProperties props, string key, string value)
        {
            try
            {
                props.Headers ??= new Dictionary<string, object>();
                props.Headers[key] = value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inject trace context.");
            }
        }
    }
}
