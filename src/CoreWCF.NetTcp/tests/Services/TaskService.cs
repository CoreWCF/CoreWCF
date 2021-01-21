// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Contract;

namespace Services
{
    public class TaskService : ITaskService
    {
        public async Task AsynchronousCompletion()
        {
            await Task.Yield();
            return;
        }

        public Task SynchronousCompletion()
        {
            return Task.CompletedTask;
        }
    }
}
