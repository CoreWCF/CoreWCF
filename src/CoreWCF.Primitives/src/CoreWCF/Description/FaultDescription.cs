// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Description
{
    public class FaultDescription
    {
        private XmlName _name;

        public FaultDescription(string action)
        {
            if (action == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(action));
            }

            Action = action;
        }

        public string Action { get; internal set; }

        // Not serializable on purpose, metadata import/export cannot
        // produce it, only available when binding to runtime
        public Type DetailType { get; set; }

        public string Name
        {
            get { return _name.EncodedName; }
            set { SetNameAndElement(new XmlName(value, true /*isEncoded*/)); }
        }

        public string Namespace { get; set; }

        internal XmlName ElementName { get; set; }

        internal bool HasProtectionLevel => false;

        internal void SetNameAndElement(XmlName name)
        {
            ElementName = _name = name;
        }

        internal void SetNameOnly(XmlName name)
        {
            _name = name;
        }
    }
}