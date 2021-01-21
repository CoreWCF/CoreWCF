// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Description
{
    public class FaultDescription
    {
        private string _action;
        private Type _detailType;
        private XmlName _elementName;
        private XmlName _name;
        private string _ns;

        public FaultDescription(string action)
        {
            if (action == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(action));
            }

            _action = action;
        }

        public string Action
        {
            get { return _action; }
            internal set { _action = value; }
        }

        // Not serializable on purpose, metadata import/export cannot
        // produce it, only available when binding to runtime
        public Type DetailType
        {
            get { return _detailType; }
            set { _detailType = value; }
        }

        public string Name
        {
            get { return _name.EncodedName; }
            set { SetNameAndElement(new XmlName(value, true /*isEncoded*/)); }
        }

        public string Namespace
        {
            get { return _ns; }
            set { _ns = value; }
        }

        internal XmlName ElementName
        {
            get { return _elementName; }
            set { _elementName = value; }
        }

        internal bool HasProtectionLevel => false;

        internal void SetNameAndElement(XmlName name)
        {
            _elementName = _name = name;
        }

        internal void SetNameOnly(XmlName name)
        {
            _name = name;
        }
    }
}