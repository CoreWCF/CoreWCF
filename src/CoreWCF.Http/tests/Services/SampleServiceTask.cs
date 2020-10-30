using CoreWCF;
using ServiceContract;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    [ServiceBehavior]
    public class SampleServiceTask : ISampleServiceTaskServerside
    {
        public Task<List<Book>> SampleMethodAsync(string name, string publisher)
        {
            Func<List<Book>> action = () =>
            {
                List<Book> books = new List<Book>();
                books.Add(new Book { Name = name, Publisher = publisher });
                return books;
            };
            Task<List<Book>> task = new Task<List<Book>>(action);
            task.Start();
            return task;
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