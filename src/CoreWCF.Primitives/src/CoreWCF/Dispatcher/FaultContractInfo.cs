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
        private readonly string _ns;
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

            Action = action;
            Detail = detail;
            if (elementName != null)
            {
                ElementName = elementName.EncodedName;
            }

            _ns = ns;
            KnownTypes = knownTypes;
        }

        public string Action { get; }

        public Type Detail { get; }

        internal string ElementName { get; }

        internal string ElementNamespace => _ns;

        internal IList<Type> KnownTypes { get; }

        internal DataContractSerializer Serializer
        {
            get
            {
                if (_serializer == null)
                {
                    if (ElementName == null)
                    {
                        _serializer = DataContractSerializerDefaults.CreateSerializer(Detail, KnownTypes, int.MaxValue /* maxItemsInObjectGraph */);
                    }
                    else
                    {
                        _serializer = DataContractSerializerDefaults.CreateSerializer(Detail, KnownTypes, ElementName, _ns ?? string.Empty, int.MaxValue /* maxItemsInObjectGraph */);
                    }
                }
                return _serializer;
            }
        }
    }
}