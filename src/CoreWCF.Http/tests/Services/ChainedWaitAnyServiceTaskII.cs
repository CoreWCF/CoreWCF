using CoreWCF;
using ServiceContract;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    [ServiceBehavior]
    public class ChainedWaitAnyServiceTaskII : ISampleServiceTaskServerside
    {
        
        public Task<List<Book>> SampleMethodAsync(string name, string publisher)
        {
            Task<string>[] tasks = new Task<string>[2];
            tasks[0] = new Task<string>(() =>
            {
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));
                name = String.Format("NewYork Best Seller {0}", name);
                return name;
            });

            tasks[1] = new Task<string>(() =>
            {
                publisher = String.Format("3rd Edition {0}", publisher);
                return publisher;
            });

            var continuation = Task<List<Book>>.Factory.ContinueWhenAny<String>(
                            tasks,
                            (antecedents) =>
                            {
                                List<Book> books = new List<Book>();
                                books.Add(new Book { Name = name, Publisher = publisher });
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
