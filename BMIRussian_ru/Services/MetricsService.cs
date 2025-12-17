using Prometheus;

namespace BMIRussian_ru.Services
{
    /// <summary>
    /// Service for tracking application metrics for Grafana/Prometheus
    /// </summary>
    public class MetricsService
    {
        /// <summary>
        /// Counter for the total number of registered users
        /// </summary>
        private static readonly Counter RegisteredUsersCounter = Metrics
            .CreateCounter(
                "bmirussian_registered_users_total",
                "Total number of registered users",
                new CounterConfiguration
                {
                    LabelNames = new[] { "source" }
                });

        /// <summary>
        /// Increments the registered users counter
        /// </summary>
        /// <param name="source">The source of registration (e.g., "telegram", "web", etc.)</param>
        public void IncrementRegisteredUsers(string source = "unknown")
        {
            RegisteredUsersCounter.WithLabels(source).Inc();
        }
    }
}

