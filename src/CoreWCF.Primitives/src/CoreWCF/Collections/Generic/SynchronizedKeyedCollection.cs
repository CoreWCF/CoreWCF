using System;
using System.Diagnostics.Contracts;
using CoreWCF.Collections.Generic;
using CoreWCF;
using CoreWCF.Runtime;
using System.Collections.Generic;

namespace CoreWCF.Collections.Generic
{
    public abstract class SynchronizedKeyedCollection<K, T> : SynchronizedCollection<T>
    {
        const int DefaultThreshold = 0;

        IEqualityComparer<K> _comparer;
        Dictionary<K, T> _dictionary;
        int _keyCount;
        int _threshold;

        protected SynchronizedKeyedCollection()
        {
            _comparer = EqualityComparer<K>.Default;
            _threshold = int.MaxValue;
        }

        protected SynchronizedKeyedCollection(object syncRoot)
            : base(syncRoot)
        {
            _comparer = EqualityComparer<K>.Default;
            _threshold = int.MaxValue;
        }

        protected SynchronizedKeyedCollection(object syncRoot, IEqualityComparer<K> comparer)
            : base(syncRoot)
        {
            if (comparer == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(comparer));

            _comparer = comparer;
            _threshold = int.MaxValue;
        }

        protected SynchronizedKeyedCollection(object syncRoot, IEqualityComparer<K> comparer, int dictionaryCreationThreshold)
            : base(syncRoot)
        {
            if (comparer == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(comparer));

            if (dictionaryCreationThreshold < -1)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(dictionaryCreationThreshold), dictionaryCreationThreshold,
                                                    SR.Format(SR.ValueMustBeInRange, -1, int.MaxValue)));
            else if (dictionaryCreationThreshold == -1)
                _threshold = int.MaxValue;
            else
                _threshold = dictionaryCreationThreshold;

            _comparer = comparer;
        }

        public T this[K key]
        {
            get
            {
                if (key == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(key));

                lock (SyncRoot)
                {
                    if (_dictionary != null)
                        return _dictionary[key];

                    for (int i = 0; i < Items.Count; i++)
                    {
                        T item = Items[i];
                        if (_comparer.Equals(key, GetKeyForItem(item)))
                            return item;
                    }

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new KeyNotFoundException());
                }
            }
        }

        protected IDictionary<K, T> Dictionary
        {
            get { return _dictionary; }
        }

        void AddKey(K key, T item)
        {
            if (_dictionary != null)
                _dictionary.Add(key, item);
            else if (_keyCount == _threshold)
            {
                CreateDictionary();
                _dictionary.Add(key, item);
            }
            else
            {
                if (Contains(key))
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.CannotAddTwoItemsWithTheSameKeyToSynchronizedKeyedCollection0);

                _keyCount++;
            }
        }

        protected void ChangeItemKey(T item, K newKey)
        {
            // check if the item exists in the collection
            if (!ContainsItem(item))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.ItemDoesNotExistInSynchronizedKeyedCollection0);

            K oldKey = GetKeyForItem(item);
            if (!_comparer.Equals(newKey, oldKey))
            {
                if (newKey != null)
                    AddKey(newKey, item);

                if (oldKey != null)
                    RemoveKey(oldKey);
            }
        }

        protected override void ClearItems()
        {
            base.ClearItems();

            if (_dictionary != null)
                _dictionary.Clear();

            _keyCount = 0;
        }

        public bool Contains(K key)
        {
            if (key == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(key));

            lock (SyncRoot)
            {
                if (_dictionary != null)
                    return _dictionary.ContainsKey(key);

                if (key != null)
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        T item = Items[i];
                        if (_comparer.Equals(key, GetKeyForItem(item)))
                            return true;
                    }
                }
                return false;
            }
        }

        bool ContainsItem(T item)
        {
            K key = default(K);
            if ((_dictionary == null) || ((key = GetKeyForItem(item)) == null))
                return Items.Contains(item);

            T itemInDict;

            if (_dictionary.TryGetValue(key, out itemInDict))
                return EqualityComparer<T>.Default.Equals(item, itemInDict);

            return false;
        }

        void CreateDictionary()
        {
            _dictionary = new Dictionary<K, T>(_comparer);

            foreach (T item in Items)
            {
                K key = GetKeyForItem(item);
                if (key != null)
                    _dictionary.Add(key, item);
            }
        }

        protected abstract K GetKeyForItem(T item);

        protected override void InsertItem(int index, T item)
        {
            K key = GetKeyForItem(item);

            if (key != null)
                AddKey(key, item);

            base.InsertItem(index, item);
        }

        public bool Remove(K key)
        {
            if (key == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(key));

            lock (SyncRoot)
            {
                if (_dictionary != null)
                {
                    if (_dictionary.ContainsKey(key))
                        return Remove(_dictionary[key]);
                    else
                        return false;
                }
                else
                {
                    for (int i = 0; i < Items.Count; i++)
                    {
                        if (_comparer.Equals(key, GetKeyForItem(Items[i])))
                        {
                            RemoveItem(i);
                            return true;
                        }
                    }
                    return false;
                }
            }
        }

        protected override void RemoveItem(int index)
        {
            K key = GetKeyForItem(Items[index]);

            if (key != null)
                RemoveKey(key);

            base.RemoveItem(index);
        }

        void RemoveKey(K key)
        {
            if (!(key != null))
            {
                Fx.Assert(false, "key shouldn't be null!");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(key));
            }
            if (_dictionary != null)
                _dictionary.Remove(key);
            else
                _keyCount--;
        }

        protected override void SetItem(int index, T item)
        {
            K newKey = GetKeyForItem(item);
            K oldKey = GetKeyForItem(Items[index]);

            if (_comparer.Equals(newKey, oldKey))
            {
                if ((newKey != null) && (_dictionary != null))
                    _dictionary[newKey] = item;
            }
            else
            {
                if (newKey != null)
                    AddKey(newKey, item);

                if (oldKey != null)
                    RemoveKey(oldKey);
            }
            base.SetItem(index, item);
        }
    }

}