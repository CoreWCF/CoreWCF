// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    public sealed class MessageProperties : IDictionary<string, object>, IDisposable
    {
        private Property[] _properties;
        private int _propertyCount;
        private MessageEncoder _encoder;
        private Uri _via;
        private object _allowOutputBatching;
        private SecurityMessageProperty _security;
        private bool _disposed;
        private const int InitialPropertyCount = 2;
        private const int MaxRecycledArrayLength = 8;
        private const string ViaKey = "Via";
        private const string AllowOutputBatchingKey = "AllowOutputBatching";
        private const string SecurityKey = "Security";
        private const string EncoderKey = "Encoder";
        private const int NotFoundIndex = -1;
        private const int ViaIndex = -2;
        private const int AllowOutputBatchingIndex = -3;
        private const int SecurityIndex = -4;
        private const int EncoderIndex = -5;
        private static readonly object s_trueBool = true;
        private static readonly object s_falseBool = false;

        public MessageProperties()
        {
        }

        public MessageProperties(MessageProperties properties)
        {
            CopyProperties(properties);
        }

        internal MessageProperties(KeyValuePair<string, object>[] array)
        {
            if (array == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(array));
            }

            CopyProperties(array);
        }

        private void ThrowDisposed()
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(string.Empty, SR.Format(SR.ObjectDisposed, GetType().ToString())));
        }

        public object this[string name]
        {
            get
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }


                if (!TryGetValue(name, out object value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.MessagePropertyNotFound, name)));
                }

                return value;
            }
            set
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                UpdateProperty(name, value, false);
            }
        }

        internal bool CanRecycle
        {
            get
            {
                return _properties == null || _properties.Length <= MaxRecycledArrayLength;
            }
        }

        public int Count
        {
            get
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                return _propertyCount;
            }
        }

        public MessageEncoder Encoder
        {
            get
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                return _encoder;
            }
            set
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                AdjustPropertyCount((object)_encoder == null, (object)value == null);
                _encoder = value;
            }
        }

        public bool AllowOutputBatching
        {
            get
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                return (object)_allowOutputBatching == s_trueBool;
            }
            set
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                AdjustPropertyCount((object)_allowOutputBatching == null, false);

                if (value)
                {
                    _allowOutputBatching = s_trueBool;
                }
                else
                {
                    _allowOutputBatching = s_falseBool;
                }
            }
        }

        public bool IsFixedSize
        {
            get
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                return false;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                return false;
            }
        }

        public ICollection<string> Keys
        {
            get
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                List<string> keys = new List<string>();

                if ((object)_via != null)
                {
                    keys.Add(ViaKey);
                }

                if ((object)_allowOutputBatching != null)
                {
                    keys.Add(AllowOutputBatchingKey);
                }

                if ((object)_security != null)
                {
                    keys.Add(SecurityKey);
                }

                if ((object)_encoder != null)
                {
                    keys.Add(EncoderKey);
                }

                if (_properties != null)
                {
                    for (int i = 0; i < _properties.Length; i++)
                    {
                        string propertyName = _properties[i].Name;

                        if (propertyName == null)
                        {
                            break;
                        }

                        keys.Add(propertyName);
                    }
                }

                return keys;
            }
        }

        public SecurityMessageProperty Security
        {
            get
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                return _security;
            }
            set
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                AdjustPropertyCount((object)_security == null, (object)value == null);
                _security = value;
            }
        }

        public ICollection<object> Values
        {
            get
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                List<object> values = new List<object>();

                if ((object)_via != null)
                {
                    values.Add(_via);
                }

                if ((object)_allowOutputBatching != null)
                {
                    values.Add(_allowOutputBatching);
                }

                if ((object)_security != null)
                {
                    values.Add(_security);
                }

                if ((object)_encoder != null)
                {
                    values.Add(_encoder);
                }
                if (_properties != null)
                {
                    for (int i = 0; i < _properties.Length; i++)
                    {
                        if (_properties[i].Name == null)
                        {
                            break;
                        }

                        values.Add(_properties[i].Value);
                    }
                }

                return values;
            }
        }

        public Uri Via
        {
            get
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                return _via;
            }
            set
            {
                if (_disposed)
                {
                    ThrowDisposed();
                }

                AdjustPropertyCount((object)_via == null, (object)value == null);
                _via = value;
            }
        }

        public void Add(string name, object property)
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            if (property == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(property));
            }

            UpdateProperty(name, property, true);
        }

        private void AdjustPropertyCount(bool oldValueIsNull, bool newValueIsNull)
        {
            if (newValueIsNull)
            {
                if (!oldValueIsNull)
                {
                    _propertyCount--;
                }
            }
            else
            {
                if (oldValueIsNull)
                {
                    _propertyCount++;
                }
            }
        }

        public void Clear()
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            if (_properties != null)
            {
                for (int i = 0; i < _properties.Length; i++)
                {
                    if (_properties[i].Name == null)
                    {
                        break;
                    }

                    _properties[i] = new Property();
                }
            }

            _via = null;
            _allowOutputBatching = null;
            _security = null;
            _encoder = null;
            _propertyCount = 0;
        }

        public void CopyProperties(MessageProperties properties)
        {
            // CopyProperties behavior should be equivalent to the behavior
            // of MergeProperties except that Merge supports property values that
            // implement the IMergeEnabledMessageProperty.  Any changes to CopyProperties
            // should be reflected in MergeProperties as well.
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(properties));
            }

            if (_disposed)
            {
                ThrowDisposed();
            }

            if (properties._properties != null)
            {
                for (int i = 0; i < properties._properties.Length; i++)
                {
                    if (properties._properties[i].Name == null)
                    {
                        break;
                    }

                    Property property = properties._properties[i];

                    // this[string] will call CreateCopyOfPropertyValue, so we don't need to repeat that here
                    this[property.Name] = property.Value;
                }
            }

            Via = properties.Via;
            AllowOutputBatching = properties.AllowOutputBatching;
            Security = (properties.Security != null) ? (SecurityMessageProperty)properties.Security.CreateCopy() : null;
            Encoder = properties.Encoder;
        }

        internal void MergeProperties(MessageProperties properties)
        {
            // MergeProperties behavior should be equivalent to the behavior
            // of CopyProperties except that Merge supports property values that
            // implement the IMergeEnabledMessageProperty.  Any changes to CopyProperties
            // should be reflected in MergeProperties as well.
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(properties));
            }

            if (_disposed)
            {
                ThrowDisposed();
            }

            if (properties._properties != null)
            {
                for (int i = 0; i < properties._properties.Length; i++)
                {
                    if (properties._properties[i].Name == null)
                    {
                        break;
                    }

                    Property property = properties._properties[i];

                    // Used with Http transport
                    //IMergeEnabledMessageProperty currentValue;
                    //if (!this.TryGetValue<IMergeEnabledMessageProperty>(property.Name, out currentValue) ||
                    //    !currentValue.TryMergeWithProperty(property.Value))
                    //{
                    // Merge wasn't possible so copy
                    // this[string] will call CreateCopyOfPropertyValue, so we don't need to repeat that here
                    this[property.Name] = property.Value;
                    //}
                }
            }

            Via = properties.Via;
            AllowOutputBatching = properties.AllowOutputBatching;
            Security = (properties.Security != null) ? (SecurityMessageProperty)properties.Security.CreateCopy() : null;
            Encoder = properties.Encoder;
        }

        internal void CopyProperties(KeyValuePair<string, object>[] array)
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            for (int i = 0; i < array.Length; i++)
            {
                KeyValuePair<string, object> property = array[i];

                // this[string] will call CreateCopyOfPropertyValue, so we don't need to repeat that here
                this[property.Key] = property.Value;
            }
        }

        public bool ContainsKey(string name)
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            int index = FindProperty(name);
            switch (index)
            {
                case ViaIndex:
                    return (object)_via != null;
                case AllowOutputBatchingIndex:
                    return (object)_allowOutputBatching != null;
                case SecurityIndex:
                    return (object)_security != null;
                case EncoderIndex:
                    return (object)_encoder != null;
                case NotFoundIndex:
                    return false;
                default:
                    return true;
            }
        }

        private object CreateCopyOfPropertyValue(object propertyValue)
        {
            if (!(propertyValue is IMessageProperty messageProperty))
            {
                return propertyValue;
            }

            object copy = messageProperty.CreateCopy();
            if (copy == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.MessagePropertyReturnedNullCopy));
            }

            return copy;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_properties != null)
            {
                for (int i = 0; i < _properties.Length; i++)
                {
                    if (_properties[i].Name == null)
                    {
                        break;
                    }

                    _properties[i].Dispose();
                }
            }

            if (_security != null)
            {
                _security.Dispose();
            }
        }

        private int FindProperty(string name)
        {
            if (name == ViaKey)
            {
                return ViaIndex;
            }
            else if (name == AllowOutputBatchingKey)
            {
                return AllowOutputBatchingIndex;
            }
            else if (name == EncoderKey)
            {
                return EncoderIndex;
            }
            else if (name == SecurityKey)
            {
                return SecurityIndex;
            }

            if (_properties != null)
            {
                for (int i = 0; i < _properties.Length; i++)
                {
                    string propertyName = _properties[i].Name;

                    if (propertyName == null)
                    {
                        break;
                    }

                    if (propertyName == name)
                    {
                        return i;
                    }
                }
            }

            return NotFoundIndex;
        }

        internal void Recycle()
        {
            _disposed = false;
            Clear();
        }

        public bool Remove(string name)
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            int originalPropertyCount = _propertyCount;
            UpdateProperty(name, null, false);
            return originalPropertyCount != _propertyCount;
        }

        public bool TryGetValue(string name, out object value)
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            int index = FindProperty(name);
            switch (index)
            {
                case ViaIndex:
                    value = _via;
                    break;
                case AllowOutputBatchingIndex:
                    value = _allowOutputBatching;
                    break;
                case SecurityIndex:
                    value = _security;
                    break;
                case EncoderIndex:
                    value = _encoder;
                    break;
                case NotFoundIndex:
                    value = null;
                    break;
                default:
                    value = _properties[index].Value;
                    break;
            }

            return value != null;
        }

        public bool TryGetValue<TProperty>(string name, out TProperty property)
        {
            if (TryGetValue(name, out object o))
            {
                if (!(o is TProperty))
                {
                    property = default;
                    return false;
                }
                    
                property = (TProperty)o;
                return true;
            }
            else
            {
                property = default;
                return false;
            }
        }

        internal TProperty GetValue<TProperty>(string name) where TProperty : class
        {
            return GetValue<TProperty>(name, false);
        }

        internal TProperty GetValue<TProperty>(string name, bool ensureTypeMatch) where TProperty : class
        {
            if (!TryGetValue(name, out object obj))
            {
                return null;
            }

            return ensureTypeMatch ? (TProperty)obj : obj as TProperty;
        }

        private void UpdateProperty(string name, object value, bool mustNotExist)
        {
            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            int index = FindProperty(name);
            if (index != NotFoundIndex)
            {
                if (mustNotExist)
                {
                    bool exists;
                    switch (index)
                    {
                        case ViaIndex:
                            exists = (object)_via != null;
                            break;
                        case AllowOutputBatchingIndex:
                            exists = (object)_allowOutputBatching != null;
                            break;
                        case SecurityIndex:
                            exists = (object)_security != null;
                            break;
                        case EncoderIndex:
                            exists = (object)_encoder != null;
                            break;
                        default:
                            exists = true;
                            break;
                    }
                    if (exists)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.DuplicateMessageProperty, name)));
                    }
                }

                if (index >= 0)
                {
                    if (value == null)
                    {
                        _properties[index].Dispose();
                        int shiftIndex;
                        for (shiftIndex = index + 1; shiftIndex < _properties.Length; shiftIndex++)
                        {
                            if (_properties[shiftIndex].Name == null)
                            {
                                break;
                            }

                            _properties[shiftIndex - 1] = _properties[shiftIndex];
                        }
                        _properties[shiftIndex - 1] = new Property();
                        _propertyCount--;
                    }
                    else
                    {
                        _properties[index].Value = CreateCopyOfPropertyValue(value);
                    }
                }
                else
                {
                    switch (index)
                    {
                        case ViaIndex:
                            Via = (Uri)value;
                            break;
                        case AllowOutputBatchingIndex:
                            AllowOutputBatching = (bool)value;
                            break;
                        case SecurityIndex:
                            if (Security != null)
                                Security.Dispose();
                            Security = (SecurityMessageProperty)CreateCopyOfPropertyValue(value);
                            break;
                        case EncoderIndex:
                            Encoder = (MessageEncoder)value;
                            break;
                        default:
                            Fx.Assert("");
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException());
                    }
                }
            }
            else if (value != null)
            {
                int newIndex;

                if (_properties == null)
                {
                    _properties = new Property[InitialPropertyCount];
                    newIndex = 0;
                }
                else
                {
                    for (newIndex = 0; newIndex < _properties.Length; newIndex++)
                    {
                        if (_properties[newIndex].Name == null)
                        {
                            break;
                        }
                    }

                    if (newIndex == _properties.Length)
                    {
                        Property[] newProperties = new Property[_properties.Length * 2];
                        Array.Copy(_properties, newProperties, _properties.Length);
                        _properties = newProperties;
                    }
                }

                object newValue = CreateCopyOfPropertyValue(value);
                _properties[newIndex] = new Property(name, newValue);
                _propertyCount++;
            }
        }

        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int index)
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            if (array == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(array));
            }

            if (array.Length < _propertyCount)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.MessagePropertiesArraySize0));
            }

            if (index < 0 || index > array.Length - _propertyCount)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index), index,
                                                    SR.Format(SR.ValueMustBeInRange, 0, array.Length - _propertyCount)));
            }

            if (_via != null)
            {
                array[index++] = new KeyValuePair<string, object>(ViaKey, _via);
            }

            if (_allowOutputBatching != null)
            {
                array[index++] = new KeyValuePair<string, object>(AllowOutputBatchingKey, _allowOutputBatching);
            }

            if (_security != null)
                array[index++] = new KeyValuePair<string, object>(SecurityKey, _security.CreateCopy());

            if (_encoder != null)
            {
                array[index++] = new KeyValuePair<string, object>(EncoderKey, _encoder);
            }

            if (_properties != null)
            {
                for (int i = 0; i < _properties.Length; i++)
                {
                    string propertyName = _properties[i].Name;

                    if (propertyName == null)
                    {
                        break;
                    }

                    array[index++] = new KeyValuePair<string, object>(propertyName, CreateCopyOfPropertyValue(_properties[i].Value));
                }
            }
        }

        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> pair)
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            if (pair.Value == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("pair.Value");
            }

            UpdateProperty(pair.Key, pair.Value, true);
        }

        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> pair)
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            if (pair.Value == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("pair.Value");
            }

            if (pair.Key == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("pair.Key");
            }

            if (!TryGetValue(pair.Key, out object value))
            {
                return false;
            }
            return value.Equals(pair.Value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            return ((IEnumerable<KeyValuePair<string, object>>)this).GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            List<KeyValuePair<string, object>> pairs = new List<KeyValuePair<string, object>>(_propertyCount);

            if (_via != null)
            {
                pairs.Add(new KeyValuePair<string, object>(ViaKey, _via));
            }

            if (_allowOutputBatching != null)
            {
                pairs.Add(new KeyValuePair<string, object>(AllowOutputBatchingKey, _allowOutputBatching));
            }

            if (_security != null)
                pairs.Add(new KeyValuePair<string, object>(SecurityKey, _security));

            if (_encoder != null)
            {
                pairs.Add(new KeyValuePair<string, object>(EncoderKey, _encoder));
            }

            if (_properties != null)
            {
                for (int i = 0; i < _properties.Length; i++)
                {
                    string propertyName = _properties[i].Name;

                    if (propertyName == null)
                    {
                        break;
                    }

                    pairs.Add(new KeyValuePair<string, object>(propertyName, _properties[i].Value));
                }
            }

            return pairs.GetEnumerator();
        }

        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> pair)
        {
            if (_disposed)
            {
                ThrowDisposed();
            }

            if (pair.Value == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("pair.Value");
            }

            if (pair.Key == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("pair.Key");
            }

            if (!TryGetValue(pair.Key, out object value))
            {
                return false;
            }
            if (!value.Equals(pair.Value))
            {
                return false;
            }
            Remove(pair.Key);
            return true;
        }

        private struct Property : IDisposable
        {
            public Property(string name, object value)
            {
                Name = name;
                Value = value;
            }

            public string Name { get; }

            public object Value { get; set; }

            public void Dispose()
            {
                if (Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
