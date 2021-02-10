// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.ServiceContract | CoreWCFAttributeTargets.OperationContract, Inherited = false, AllowMultiple = false)]
    public sealed class DataContractFormatAttribute : Attribute
    {
        private OperationFormatStyle _style;
        public OperationFormatStyle Style
        {
            get { return _style; }
            set
            {
                XmlSerializerFormatAttribute.ValidateOperationFormatStyle(_style);
                _style = value;
            }
        }
    }
}