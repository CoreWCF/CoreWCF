// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;

namespace CoreWCF.Description
{
    public class MessageDescriptionCollection : Collection<MessageDescription>
    {
        internal MessageDescriptionCollection() { }

        public MessageDescription Find(string action)
        {
            foreach (MessageDescription description in this)
            {
                if (description != null && action == description.Action)
                    return description;
            }

            return null;
        }

        public Collection<MessageDescription> FindAll(string action)
        {
            Collection<MessageDescription> descriptions = new Collection<MessageDescription>();
            foreach (MessageDescription description in this)
            {
                if (description != null && action == description.Action)
                    descriptions.Add(description);
            }

            return descriptions;
        }
    }
}
