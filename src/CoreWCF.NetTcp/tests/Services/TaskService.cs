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
