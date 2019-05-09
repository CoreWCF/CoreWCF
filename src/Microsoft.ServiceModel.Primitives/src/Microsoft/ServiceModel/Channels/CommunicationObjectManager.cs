using System;
using System.Collections.Generic;

namespace Microsoft.ServiceModel.Channels
{
    internal class CommunicationObjectManager<TItemType> : LifetimeManager where TItemType : class, ICommunicationObject
    {
        bool _inputClosed;
        readonly ISet<TItemType> _itemsSet;

        public CommunicationObjectManager(object mutex)
            : base(mutex)
        {
            _itemsSet = new HashSet<TItemType>();
        }

        public void Add(TItemType item)
        {
            bool added = false;

            lock (ThisLock)
            {
                if (State == LifetimeState.Opened && !_inputClosed)
                {
                    if (_itemsSet.Contains(item))
                        return;

                    _itemsSet.Add(item);
                    IncrementBusyCountWithoutLock();
                    item.Closed += OnItemClosed;
                    added = true;
                }
            }

            if (!added)
            {
                item.Abort();
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().ToString()));
            }
        }

        public void CloseInput()
        {
            //Abort can reenter this call as a result of 
            //close timeout, Closing input twice is not a
            //FailFast case.
            _inputClosed = true;
        }

        public void DecrementActivityCount()
        {
            DecrementBusyCount();
        }

        public void IncrementActivityCount()
        {
            IncrementBusyCount();
        }

        void OnItemClosed(object sender, EventArgs args)
        {
            Remove((TItemType)sender);
        }

        public void Remove(TItemType item)
        {
            lock (ThisLock)
            {
                if (!_itemsSet.Contains(item))
                    return;
                _itemsSet.Remove(item);
            }

            item.Closed -= OnItemClosed;
            DecrementBusyCount();
        }

        public TItemType[] ToArray()
        {
            lock (ThisLock)
            {
                int index = 0;
                TItemType[] items = new TItemType[_itemsSet.Count];
                foreach (TItemType item in _itemsSet)
                    items[index++] = item;

                return items;
            }
        }
    }

}