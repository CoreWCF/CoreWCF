using CoreWCF;
using ServiceContract;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Services
{
    [ServiceBehavior]
    public class ChainedWaitAnyServiceTaskI : ISampleServiceTaskServerside
    {
        public Task<List<Book>> SampleMethodAsync(string name, string publisher)
        {
            Task<string>[] tasks = new Task<string>[2];
            tasks[0] = new Task<string>(() =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                return String.Format("NewYork Best Seller {0}", name);
            });

            tasks[1] = new Task<string>(() =>
            {
                return String.Format("3rd Edition {0}", publisher);
            });

            var continuation = Task<List<Book>>.Factory.ContinueWhenAny<String>(
                            tasks,
                            (antecedents) =>
                            {
                                List<Book> books = new List<Book>();
                                books.Add(new Book { Name = tasks[0].Result, Publisher = tasks[1].Result });
                                return books;
                            });

            tasks[0].Start();
            tasks[1].Start();
            continuation.Wait();

            return continuation;
        }

        public Task SampleMethodAsync2(string name)
        {
            Action action = () =>
            {
                name = name ?? "Olga";
            };
            Task task = new Task(action);
            task.Start();
            return task;
        }
    }
}
