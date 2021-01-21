// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public class MessagePartDescriptionCollection : System.Collections.ObjectModel.KeyedCollection<System.Xml.XmlQualifiedName, MessagePartDescription>
    {
        internal MessagePartDescriptionCollection() { }
        protected override System.Xml.XmlQualifiedName GetKeyForItem(MessagePartDescription item) { return default(System.Xml.XmlQualifiedName); }
    }
}