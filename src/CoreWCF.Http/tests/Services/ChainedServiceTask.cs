using CoreWCF;
using ServiceContract;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    [ServiceBehavior]
    public class ChainedServiceTask : ISampleServiceTaskServerside
    {
        public Task<List<Book>> SampleMethodAsync(string name, string publisher)
        {
            Func<List<Book>> firstAction = () =>
            {
                List<Book> books = new List<Book>();
                books.Add(new Book { Name = name, Publisher = publisher });
                return books;
            };
            Task<List<Book>> task = new Task<List<Book>>(firstAction);
            Task<List<Book>> task2 = task.ContinueWith((antecedent) =>
            {
                foreach (Book book in antecedent.Result)
                {
                    book.ISBN = Guid.NewGuid();
                }
                return antecedent.Result;
            });
            Task<List<Book>> task3 = task2.ContinueWith((antecedent) =>
            {
                foreach (Book book in antecedent.Result)
                {
                    Console.WriteLine(book.Name);
                }
                return antecedent.Result;
            });
            task.Start();
            return task3;
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
