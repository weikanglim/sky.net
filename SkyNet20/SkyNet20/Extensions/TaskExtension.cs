using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SkyNet20.Extensions
{
    public static class TaskExtension
    {
        public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)))
            {
                return await task;
            }
            throw new TimeoutException();
        }

        public static async Task WithTimeout(this Task task, TimeSpan timeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)))
            {
                return;
            }

            throw new TimeoutException();
        }
    }
}
