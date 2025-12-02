namespace BMIRussian_ru.Services
{
    public abstract class BackgroundService : IHostedService, IDisposable
    {
        private Task? executingTask;
        private readonly CancellationTokenSource stoppingCancellationTokenSource = new();

        protected abstract Task ExecuteAsync(CancellationToken stoppingToken);

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            // Store the task we're executing
            executingTask = ExecuteAsync(stoppingCancellationTokenSource.Token);

            // If the task is completed then return it,
            // this will bubble cancellation and failure to the caller
            if (executingTask.IsCompleted)
            {
                return executingTask;
            }

            // Otherwise it's running
            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            // Stop called without start
            if (executingTask == null)
            {
                return;
            }

            try
            {
                // Signal cancellation to the executing method
                stoppingCancellationTokenSource.Cancel();
            }
            finally
            {
                // Wait until the task completes or the stop token triggers
                await Task.WhenAny(executingTask, Task.Delay(Timeout.Infinite,
                                                              cancellationToken));
            }
        }

        public virtual void Dispose()
        {
            stoppingCancellationTokenSource.Cancel();
        }
    }
}
