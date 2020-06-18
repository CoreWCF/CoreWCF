using CoreWCF;
using ServiceContract;
using System;
using System.Threading.Tasks;

namespace Services
{
    public class AggregateExceptionService : IAggregateExceptionService
    {
        public const string FaultId = "101";

        public const string ErrorMessage = "Error has occurred while performing an operation.";

        public Task SimpleOperationThrowingFault()
        {
            Task resultTsk = Task.Factory.StartNew(() =>
            {
                throw new FaultException<SampleServiceFault>(new SampleServiceFault() { ID = FaultId, Message = ErrorMessage }, new FaultReason("SampleServiceFault"));
            });

            return resultTsk;
        }

        public void ServiceOpWithMultipleTasks()
        {
            Task resultTsk1 = Task.Factory.StartNew(() =>
            {
                throw new InvalidOperationException("Invalid operation is being performed");
            });

            Task resultTsk2 = Task.Factory.StartNew(() =>
            {
                throw new ArgumentNullException("Some param");
            });

            Task resultTsk3 = Task.Factory.StartNew(() =>
            {
                throw new NullReferenceException("object reference is null.");
            });

            try
            {
                Task.WaitAll(resultTsk1, resultTsk2, resultTsk3);
            }
            catch (AggregateException)
            {
                throw new FaultException<SampleServiceFault>(
                                                    new SampleServiceFault() { ID = FaultId, Message = ErrorMessage },
                                                    new FaultReason("SampleServiceFault"));
            }
        }

        public Task ServiceOpWithChainedTasks_ThrowFaultExceptionInOneTask()
        {
            Task chainedTask = Task.Factory.StartNew(() =>
            {
                throw new InvalidOperationException("Invalid operation is being performed");
            }).ContinueWith((tsk) =>
            {
                throw new ArgumentNullException("param1");
            }).ContinueWith((tsk) =>
            {
                throw new FaultException<SampleServiceFault>(new SampleServiceFault() { ID = FaultId, Message = ErrorMessage }, new FaultReason("SampleServiceFault"));
            });

            return chainedTask;
        }
    }
}
