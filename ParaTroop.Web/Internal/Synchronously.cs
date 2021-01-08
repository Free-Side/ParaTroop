using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace ParaTroop.Web.Internal {
    /// <summary>
    /// Helper functions for running asynchronous code in a synchronous context
    /// (without deadlocking UI and Web applications).
    /// </summary>
    public static class Synchronously {
        /// <summary>
        /// Given a function that returns a Task, execute that task
        /// synchronously without deadlocking.
        /// </summary>
        /// <remarks>
        /// It is critical that the delegate passed to this function actually
        /// create the task to be executed. If this delegate returns a task created
        /// in another context it may be to late and the original context already
        /// destined for an inevitable deadlock.
        /// </remarks>
        /// <param name="createTask">A function that returns a task.</param>
        public static void Await(Func<Task> createTask) {
            try {
                CreateDetached(createTask).Wait();
            } catch (AggregateException exception) {
                if (exception.InnerExceptions.Count == 1) {
                    ExceptionDispatchInfo.Capture(exception.InnerExceptions[0]).Throw();
                } else {
                    throw;
                }
            }
        }

        /// <summary>
        /// Given a function that returns a Task, execute that task
        /// synchronously without deadlocking.
        /// </summary>
        /// <remarks>
        /// It is critical that the delegate passed to this function actually
        /// create the task to be executed. If this delegate returns a task created
        /// in another context it may be to late and the original context already
        /// destined for an inevitable deadlock.
        /// </remarks>
        /// <param name="createTask">A function that returns a task.</param>
        /// <typeparam name="T">The type of value returned by the task.</typeparam>
        /// <returns>
        /// The result of the task returned by <paramref name="createTask"/>.
        /// </returns>
        public static T Await<T>(Func<Task<T>> createTask) {
            try {
                return CreateDetached(createTask).Result;
            } catch (AggregateException exception) {
                if (exception.InnerExceptions.Count == 1) {
                    ExceptionDispatchInfo.Capture(exception.InnerExceptions[0]).Throw();
                    throw;
                } else {
                    throw;
                }
            }
        }

        private static async Task CreateDetached(Func<Task> createTask) {
            // This first await breaks the Task away from the context it was
            // created in. Thus any subsequent awaits are insulated from the
            // original context.
            await Task.Delay(1).ConfigureAwait(continueOnCapturedContext: false);
            await createTask();
        }

        private static async Task<T> CreateDetached<T>(Func<Task<T>> createTask) {
            await Task.Delay(1).ConfigureAwait(continueOnCapturedContext: false);
            return await createTask();
        }
    }
}
