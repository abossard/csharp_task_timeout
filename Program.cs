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
        /// <returns>the answer</returns>
        public static Task<int> CreateInfiniteTask() {
            return Task.Run(async ()=>{
                while (MostlyTrue)
                {
                    await Task.Delay(100);
                }
                return 42;
            });
        }
        
        /// <summary>
        /// A better example of how to implement a long running task
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
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
        
        /// <summary>
        /// Wrap a Task that doesn't support cancellation so that the control flow goes back after a certain timeout.
        /// Be aware: this doesn't cancel or stop the previous task. It just gives back the control flow and allows you to e.g. report on the issue (and maybe abort the process)
        /// </summary>
        /// <param name="taskToWrap">just any Task</param>
        /// <param name="cancellationToken">to decide from outside, if a cancellation has happened</param>
        /// <typeparam name="TResult">Result type</typeparam>
        /// <returns></returns>
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
            // first we create the cancellation token source which we'll use in the next two examples
            var tokenSource = new CancellationTokenSource();
            
            // Example 1: When the job supports cancellation
            // We set a timeout after which the jobs will be cancelled
            tokenSource.CancelAfter(TimeSpan.FromSeconds(1));
            try
            {
                // we create the job and pass the token
                var answer = await CreateInfiniteTaskWithCancellation(tokenSource.Token);
            }
            catch (TaskCanceledException taskCanceledException)
            {
                Console.WriteLine("Task Cancelled after timeout");
            }
            
            // Example 2: With the wrapping
            tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(1));
            try
            {
                var answer = await WrapTaskWithCancellation(CreateInfiniteTask(), tokenSource.Token);
            }
            catch (TaskCanceledException taskCanceledException)
            {
                Console.WriteLine("Task Cancelled after timeout");
                // as the inner long running task can't be cancelled, it's highly suggested to exit the process
                Environment.Exit(99);
            }

        }
    }
}
