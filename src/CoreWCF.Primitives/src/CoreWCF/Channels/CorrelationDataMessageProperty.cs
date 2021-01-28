// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class CorrelationDataMessageProperty : IMessageProperty
    {
        private const string PropertyName = "CorrelationDataMessageProperty";
        private Dictionary<string, DataProviderEntry> _dataProviders;

        public CorrelationDataMessageProperty()
        {
        }

        private CorrelationDataMessageProperty(IDictionary<string, DataProviderEntry> dataProviders)
        {
            if (dataProviders != null && dataProviders.Count > 0)
            {
                _dataProviders = new Dictionary<string, DataProviderEntry>(dataProviders);
            }
        }

        public static string Name
        {
            get { return PropertyName; }
        }

        public void Add(string name, Func<string> dataProvider)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            if (dataProvider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(dataProvider));
            }

            if (_dataProviders == null)
            {
                _dataProviders = new Dictionary<string, DataProviderEntry>();
            }
            _dataProviders.Add(name, new DataProviderEntry(dataProvider));
        }

        public bool Remove(string name)
        {
            if (_dataProviders != null)
            {
                return _dataProviders.Remove(name);
            }
            else
            {
                return false;
            }
        }

        public bool TryGetValue(string name, out string value)
        {
            if (_dataProviders != null && _dataProviders.TryGetValue(name, out DataProviderEntry entry))
            {
                value = entry.Data;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public static bool TryGet(Message message, out CorrelationDataMessageProperty property)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }
            return TryGet(message.Properties, out property);
        }

        public static bool TryGet(MessageProperties properties, out CorrelationDataMessageProperty property)
        {
            if (properties.TryGetValue(PropertyName, out object value))
            {
                property = value as CorrelationDataMessageProperty;
            }
            else
            {
                property = null;
            }
            return property != null;
        }

        public static void AddData(Message message, string name, Func<string> dataProvider)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            if (dataProvider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(dataProvider));
            }

            CorrelationDataMessageProperty data = null;
            if (message.Properties.TryGetValue(PropertyName, out object value))
            {
                data = value as CorrelationDataMessageProperty;
            }

            bool addNewProperty = false;
            if (data == null)
            {
                data = new CorrelationDataMessageProperty();
                addNewProperty = true;
            }

            data.Add(name, dataProvider);

            if (addNewProperty)
            {
                message.Properties[PropertyName] = data;
            }
        }

        public IMessageProperty CreateCopy()
        {
            return new CorrelationDataMessageProperty(_dataProviders);
        }

        private class DataProviderEntry
        {
            private string _resolvedData;
            private Func<string> _dataProvider;

            public DataProviderEntry(Func<string> dataProvider)
            {
                Fx.Assert(dataProvider != null, "dataProvider required");
                _dataProvider = dataProvider;
                _resolvedData = null;
            }

            public string Data
            {
                get
                {
                    if (_dataProvider != null)
                    {
                        _resolvedData = _dataProvider();
                        _dataProvider = null;
                    }

                    return _resolvedData;
                }
            }
        }
    }
}