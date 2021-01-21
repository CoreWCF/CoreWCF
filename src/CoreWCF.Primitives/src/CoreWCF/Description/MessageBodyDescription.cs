// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace CoreWCF.Description
{
    public class MessageBodyDescription
    {
        private XmlName _wrapperName;
        private string _wrapperNs;
        private readonly MessagePartDescriptionCollection _parts;
        private MessagePartDescription _returnValue;

        public MessageBodyDescription()
        {
            _parts = new MessagePartDescriptionCollection();
        }

        public MessagePartDescriptionCollection Parts
        {
            get { return _parts; }
        }

        [DefaultValue(null)]
        public MessagePartDescription ReturnValue
        {
            get { return _returnValue; }
            set { _returnValue = value; }
        }

        [DefaultValue(null)]
        public string WrapperName
        {
            get { return _wrapperName == null ? null : _wrapperName.EncodedName; }
            set { _wrapperName = new XmlName(value, true /*isEncoded*/); }
        }

        [DefaultValue(null)]
        public string WrapperNamespace
        {
            get { return _wrapperNs; }
            set { _wrapperNs = value; }
        }
    }
}