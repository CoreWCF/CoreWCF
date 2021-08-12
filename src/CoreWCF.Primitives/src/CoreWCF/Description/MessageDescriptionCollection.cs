// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public class MessageDescriptionCollection : System.Collections.ObjectModel.Collection<MessageDescription>
    {
        internal MessageDescriptionCollection() { }
        public MessageDescription Find(string action) { return default; }
        public System.Collections.ObjectModel.Collection<MessageDescription> FindAll(string action) { return default; }
    }
}