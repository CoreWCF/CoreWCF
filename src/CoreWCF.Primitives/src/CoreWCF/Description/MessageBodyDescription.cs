// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace CoreWCF.Description
{
    public class MessageBodyDescription
    {
        private XmlName _wrapperName;

        public MessageBodyDescription()
        {
            Parts = new MessagePartDescriptionCollection();
        }

        internal MessageBodyDescription(MessageBodyDescription other)
        {
            WrapperName = other.WrapperName;
            WrapperNamespace = other.WrapperNamespace;
            Parts = new MessagePartDescriptionCollection();
            foreach (MessagePartDescription mpd in other.Parts)
            {
                Parts.Add(mpd.Clone());
            }

            if (other.ReturnValue != null)
            {
                ReturnValue = other.ReturnValue.Clone();
            }
        }

        internal MessageBodyDescription Clone()
        {
            return new MessageBodyDescription(this);
        }

        public MessagePartDescriptionCollection Parts { get; }

        [DefaultValue(null)]
        public MessagePartDescription ReturnValue { get; set; }

        [DefaultValue(null)]
        public string WrapperName
        {
            get { return _wrapperName?.EncodedName; }
            set { _wrapperName = new XmlName(value, true /*isEncoded*/); }
        }

        [DefaultValue(null)]
        public string WrapperNamespace { get; set; }
    }
}
