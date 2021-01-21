// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.ServiceContract | CoreWCFAttributeTargets.OperationContract, Inherited = false, AllowMultiple = false)]
    public sealed class DataContractFormatAttribute : Attribute
    {
        OperationFormatStyle style;
        public OperationFormatStyle Style
        {
            get { return style; }
            set
            {
                XmlSerializerFormatAttribute.ValidateOperationFormatStyle(style);
                style = value;
            }
        }

    }
}