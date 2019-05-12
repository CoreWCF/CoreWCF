using System;
using System.Collections.Generic;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class CorrelationDataMessageProperty : IMessageProperty
    {
        const string PropertyName = "CorrelationDataMessageProperty";
        Dictionary<string, DataProviderEntry> dataProviders;

        public CorrelationDataMessageProperty()
        {
        }

        CorrelationDataMessageProperty(IDictionary<string, DataProviderEntry> dataProviders)
        {
            if (dataProviders != null && dataProviders.Count > 0)
            {
                this.dataProviders = new Dictionary<string, DataProviderEntry>(dataProviders);
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("name");
            }

            if (dataProvider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("dataProvider");
            }

            if (dataProviders == null)
            {
                dataProviders = new Dictionary<string, DataProviderEntry>();
            }
            dataProviders.Add(name, new DataProviderEntry(dataProvider));
        }

        public bool Remove(string name)
        {
            if (dataProviders != null)
            {
                return dataProviders.Remove(name);
            }
            else
            {
                return false;
            }
        }

        public bool TryGetValue(string name, out string value)
        {
            DataProviderEntry entry;
            if (dataProviders != null && dataProviders.TryGetValue(name, out entry))
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }
            return TryGet(message.Properties, out property);
        }

        public static bool TryGet(MessageProperties properties, out CorrelationDataMessageProperty property)
        {
            object value = null;
            if (properties.TryGetValue(PropertyName, out value))
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("name");
            }

            if (dataProvider == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("dataProvider");
            }

            CorrelationDataMessageProperty data = null;
            object value = null;
            if (message.Properties.TryGetValue(PropertyName, out value))
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
            return new CorrelationDataMessageProperty(dataProviders);
        }

        class DataProviderEntry
        {
            string resolvedData;
            Func<string> dataProvider;

            public DataProviderEntry(Func<string> dataProvider)
            {
                Fx.Assert(dataProvider != null, "dataProvider required");
                this.dataProvider = dataProvider;
                resolvedData = null;
            }

            public string Data
            {
                get
                {
                    if (dataProvider != null)
                    {
                        resolvedData = dataProvider();
                        dataProvider = null;
                    }

                    return resolvedData;
                }
            }
        }
    }

}