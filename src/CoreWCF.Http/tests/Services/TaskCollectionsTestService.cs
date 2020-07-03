using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ServiceContract;

namespace Services
{
    public class TaskCollectionsTest : ITaskCollectionsTest
    {
        public Task<LinkedList<int>> GetList()
        {
            Task<LinkedList<int>> task = new Task<LinkedList<int>>(delegate
            {
                LinkedList<int> linkedList = new LinkedList<int>();
                linkedList.AddFirst(100);
                linkedList.AddFirst(40);
                return linkedList;
            });
            task.Start();
            return task;
        }

        public Task<Dictionary<string, int>> GetDictionary()
        {
            Task<Dictionary<string, int>> task = new Task<Dictionary<string, int>>(() => new Dictionary<string, int>
            {
                {
                    "Sam",
                    1
                },
                {
                    "Sara",
                    2
                },
                {
                    "Tom",
                    3
                }
            });
            task.Start();
            return task;
        }

        public Task<HashSet<Book>> GetSet()
        {
            Task<HashSet<Book>> task = new Task<HashSet<Book>>(() => new HashSet<Book>
            {
                new Book
                {
                    Name = "Whoa"
                },
                new Book
                {
                    Name = "Dipper"
                }
            });
            task.Start();
            return task;
        }

        public Task<Stack<byte>> GetStack()
        {
            Task<Stack<byte>> task = new Task<Stack<byte>>(delegate
            {
                Stack<byte> stack = new Stack<byte>();
                stack.Push(45);
                stack.Push(10);
                return stack;
            });
            task.Start();
            return task;
        }

        public Task<Queue<string>> GetQueue()
        {
            Task<Queue<string>> task = new Task<Queue<string>>(delegate
            {
                Queue<string> queue = new Queue<string>();
                queue.Enqueue("Panasonic");
                queue.Enqueue("Sony");
                queue.Enqueue("Kodak");
                return queue;
            });
            task.Start();
            return task;
        }
    }
}
