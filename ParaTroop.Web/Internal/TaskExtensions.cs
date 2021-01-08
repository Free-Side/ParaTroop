using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ParaTroop.Web.Internal {
    public static class TaskExtensions {
        public static async Task Then(
            this Task task,
            Func<Task> onSuccess,
            Func<Exception, Task> onError = null) {

            try {
                await task;
                await onSuccess();
            } catch (Exception err) {
                if (onError != null) {
                    await onError(err);
                } else {
                    throw;
                }
            }
        }

        public static async Task<TOut> Then<TOut>(
            this Task task,
            Func<Task<TOut>> onSuccess,
            Func<Exception, Task<TOut>> onError = null) {

            try {
                await task;
                return await onSuccess();
            } catch (Exception err) {
                if (onError != null) {
                    return await onError(err);
                } else {
                    throw;
                }
            }
        }

        public static async Task<TOut> Then<TOut>(
            this Task task,
            Func<TOut> onSuccess,
            Func<Exception, TOut> onError = null) {

            try {
                await task;
                return onSuccess();
            } catch (Exception err) {
                if (onError != null) {
                    return onError(err);
                } else {
                    throw;
                }
            }
        }

        public static async Task<TOut> Then<TIn, TOut>(
            this Task<TIn> task,
            Func<TIn, Task<TOut>> onSuccess,
            Func<Exception, Task<TOut>> onError = null) {

            try {
                var value = await task;
                return await onSuccess(value);
            } catch (Exception err) {
                if (onError != null) {
                    return await onError(err);
                } else {
                    throw;
                }
            }
        }

        public static async Task<TOut> Then<TIn, TOut>(
            this Task<TIn> task,
            Func<TIn, TOut> onSuccess,
            Func<Exception, TOut> onError = null) {

            try {
                var value = await task;
                return onSuccess(value);
            } catch (Exception err) {
                if (onError != null) {
                    return onError(err);
                } else {
                    throw;
                }
            }
        }

        /// <summary>
        /// The built in WhenAny annoyingly returns a Task&lt;Task&lt;T>>.
        /// </summary>
        public static async Task<TResult> WhenAny<TResult>(params Task<TResult>[] tasks) {
            var completedTask = await Task.WhenAny(tasks);
            return await completedTask;
        }
    }
}
