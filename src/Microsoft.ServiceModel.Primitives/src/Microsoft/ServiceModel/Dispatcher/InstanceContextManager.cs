using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Channels;
using System.Diagnostics;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal interface IInstanceContextManager
    {
        void Abort();
        void Add(InstanceContext instanceContext);
        Task CloseAsync(CancellationToken token);
        Task CloseInputAsync(CancellationToken token);
        bool Remove(InstanceContext instanceContext);
        InstanceContext[] ToArray();
    }

    internal class InstanceContextManager : LifetimeManager, IInstanceContextManager
    {
        int firstFreeIndex;
        Item[] items;

        public InstanceContextManager(object mutex)
            : base(mutex)
        {
        }

        public void Add(InstanceContext instanceContext)
        {
            bool added = false;

            lock (ThisLock)
            {
                if (State == LifetimeState.Opened)
                {
                    if (instanceContext.InstanceContextManagerIndex != 0)
                        return;
                    if (firstFreeIndex == 0)
                        GrowItems();
                    AddItem(instanceContext);
                    base.IncrementBusyCountWithoutLock();
                    added = true;
                }
            }

            if (!added)
            {
                instanceContext.Abort();
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().ToString()));
            }
        }

        void AddItem(InstanceContext instanceContext)
        {
            int index = firstFreeIndex;
            firstFreeIndex = items[index].nextFreeIndex;
            items[index].instanceContext = instanceContext;
            instanceContext.InstanceContextManagerIndex = index;
        }

        private async Task CloseInitiateAsync(CancellationToken token)
        {
            InstanceContext[] instances = ToArray();
            for (int index = 0; index < instances.Length; index++)
            {
                InstanceContext instance = instances[index];
                try
                {
                    if (instance.State == CommunicationState.Opened)
                    {
                        Task result = instance.CloseAsync(token);
                        if (!result.IsCompleted)
                        {
                            ContinueCloseInstanceContext(result);
                            continue;
                        }

                        await result;
                    }
                    else
                    {
                        instance.Abort();
                    }
                }
                catch (ObjectDisposedException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                catch (InvalidOperationException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                catch (CommunicationException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                catch (TimeoutException e)
                {
                    //if (TD.CloseTimeoutIsEnabled())
                    //{
                    //    TD.CloseTimeout(e.Message);
                    //}
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
            }
        }

        public async Task CloseInputAsync(CancellationToken token)
        {
            InstanceContext[] instances = ToArray();
            for (int index = 0; index < instances.Length; index++)
                await instances[index].CloseInputAsync(token);
        }

        private static async void ContinueCloseInstanceContext(Task result)
        {
            try
            {
                await result;
            }
            catch (ObjectDisposedException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (InvalidOperationException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (CommunicationException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (TimeoutException e)
            {
                //if (TD.CloseTimeoutIsEnabled())
                //{
                //    TD.CloseTimeout(e.Message);
                //}
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
        }

        void GrowItems()
        {
            Item[] existingItems = items;
            if (existingItems != null)
            {
                InitItems(existingItems.Length * 2);
                for (int i = 1; i < existingItems.Length; i++)
                    AddItem(existingItems[i].instanceContext);
            }
            else
            {
                InitItems(4);
            }
        }

        void InitItems(int count)
        {
            items = new Item[count];
            for (int i = count - 2; i > 0; i--)
            {
                items[i].nextFreeIndex = i + 1;
            }
            firstFreeIndex = 1;
        }

        protected override void OnAbort()
        {
            InstanceContext[] instances = ToArray();
            for (int index = 0; index < instances.Length; index++)
            {
                instances[index].Abort();
            }

            base.OnAbort();
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            await CloseInitiateAsync(token);
            await base.OnCloseAsync(token);
        }

        public bool Remove(InstanceContext instanceContext)
        {
            if (instanceContext == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(instanceContext));

            lock (ThisLock)
            {
                int index = instanceContext.InstanceContextManagerIndex;
                if (index == 0)
                    return false;
                instanceContext.InstanceContextManagerIndex = 0;
                items[index].nextFreeIndex = firstFreeIndex;
                items[index].instanceContext = null;
                firstFreeIndex = index;
            }

            base.DecrementBusyCount();
            return true;
        }

        public InstanceContext[] ToArray()
        {
            if (items == null)
            {
                return Array.Empty<InstanceContext>();
            }

            lock (ThisLock)
            {
                int count = 0;
                for (int i = 1; i < items.Length; i++)
                    if (items[i].instanceContext != null)
                        count++;

                if (count == 0)
                    return Array.Empty<InstanceContext>();

                InstanceContext[] array = new InstanceContext[count];
                count = 0;
                for (int i = 1; i < items.Length; i++)
                {
                    InstanceContext instanceContext = items[i].instanceContext;
                    if (instanceContext != null)
                    {
                        array[count++] = instanceContext;
                    }
                }

                return array;
            }
        }

        struct Item
        {
            public int nextFreeIndex;
            public InstanceContext instanceContext;
        }
    }
}