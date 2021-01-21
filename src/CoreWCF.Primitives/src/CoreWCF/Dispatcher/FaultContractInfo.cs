// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using CoreWCF.Description;

namespace CoreWCF.Dispatcher
{
    internal class FaultContractInfo
    {
        private readonly string _action;
        private readonly Type _detail;
        private readonly string _elementName;
        private readonly string _ns;
        private readonly IList<Type> _knownTypes;
        private DataContractSerializer _serializer;

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