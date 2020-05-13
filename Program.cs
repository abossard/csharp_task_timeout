using System;
using System.Threading;
using System.Threading.Tasks;

namespace WaitAny
{
    class Program
    {
        private static readonly Random random = new Random();

        private static bool MostlyTrue => random.Next() != 42;
        
        /// <summary>
        /// No a good example of a long running task. The caller has no way to cancel or interrupt it and needs to resort to 
        /// </summary>
        /// <returns></returns>
        public static Task<int> CreateInfiniteTask() {
            return Task.Run(async ()=>{
                while (MostlyTrue)
                {
                    await Task.Delay(100);
                }
                return 42;
            });
        }
        
        public static Task<int> CreateInfiniteTaskWithCancellation(CancellationToken ct) {
            return Task.Run(async ()=>{
                ct.ThrowIfCancellationRequested();
                while (MostlyTrue)
                {
                    // Poll on this property if you have to do
                    // other cleanup before throwing.
                    await Task.Delay(100, ct);
                    if (ct.IsCancellationRequested)
                    {
                        // Clean up here, then...
                        ct.ThrowIfCancellationRequested();
                    }
                }
                return 42;
            }, ct);
        }
        
        public static async Task<TResult> WrapTaskWithCancellation<TResult>(Task<TResult> taskToWrap, CancellationToken cancellationToken)
        {
            // We create a TaskCompletionSource of decimal
            var taskCompletionSource = new TaskCompletionSource<TResult>();

            // Registering a lambda into the cancellationToken
            cancellationToken.Register(() =>
            {
                // We received a cancellation message, cancel the TaskCompletionSource.Task
                taskCompletionSource.TrySetCanceled();
            });

            // Wait for the first task to finish among the two
            var completedTask = await Task.WhenAny(taskToWrap, taskCompletionSource.Task);

            // If the completed task is our long running operation we set its result.
            if (completedTask != taskToWrap)
            {
                return await taskCompletionSource.Task;
            } 
            // Extract the result, the task is finished and the await will return immediately
            var result = await taskToWrap;

            // Set the taskCompletionSource result
            taskCompletionSource.TrySetResult(result);

            // Return the result of the TaskCompletionSource.Task
            return await taskCompletionSource.Task;
        }
        
        private static async Task Main()
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(1));
            try
            {
                var answer = await CreateInfiniteTaskWithCancellation(tokenSource.Token);
            }
            catch (TaskCanceledException taskCanceledException)
            {
                Console.WriteLine("Task Cancelled after timeout");
            }
            
            tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(1));
            try
            {
                var answer = await WrapTaskWithCancellation(CreateInfiniteTask(), tokenSource.Token);
            }
            catch (TaskCanceledException taskCanceledException)
            {
                Console.WriteLine("Task Cancelled after timeout");
            }

        }
    }
}
