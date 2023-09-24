using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;

namespace core6.Healthy
{
    public class App3HealthCheck : IHealthCheck
    {
        private static readonly HttpClient HttpClient  = new();

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var response = HttpClient.GetStringAsync("http://hybridlog.io:5001/dummy").Result;

            // Console.WriteLine(status);

            var result = response == "Ok"
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Failed app3");

            return Task.FromResult(result);
        }
    }
}
