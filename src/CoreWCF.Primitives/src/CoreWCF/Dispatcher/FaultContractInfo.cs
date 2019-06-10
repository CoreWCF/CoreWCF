using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using CoreWCF.Description;

namespace CoreWCF.Dispatcher
{
    internal class FaultContractInfo
    {
        readonly string _action;
        readonly Type _detail;
        readonly string _elementName;
        readonly string _ns;
        readonly IList<Type> _knownTypes;
        DataContractSerializer _serializer;

        public FaultContractInfo(string action, Type detail)
            : this(action, detail, null, null, null)
        {
        }
        internal FaultContractInfo(string action, Type detail, XmlName elementName, string ns, IList<Type> knownTypes)
        {
            if (action == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(action));
            }
            if (detail == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(detail));
            }

            _action = action;
            _detail = detail;
            if (elementName != null)
                _elementName = elementName.EncodedName;
            _ns = ns;
            _knownTypes = knownTypes;
        }

        public string Action => _action;

        public Type Detail => _detail;

        internal string ElementName => _elementName;

        internal string ElementNamespace => _ns;

        internal IList<Type> KnownTypes => _knownTypes;

        internal DataContractSerializer Serializer
        {
            get
            {
                if (_serializer == null)
                {
                    if (_elementName == null)
                    {
                        _serializer = DataContractSerializerDefaults.CreateSerializer(_detail, _knownTypes, int.MaxValue /* maxItemsInObjectGraph */);
                    }
                    else
                    {
                        _serializer = DataContractSerializerDefaults.CreateSerializer(_detail, _knownTypes, _elementName, _ns ?? string.Empty, int.MaxValue /* maxItemsInObjectGraph */);
                    }
                }
                return _serializer;
            }
        }
    }

}