// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public class MessagePropertyDescription : MessagePartDescription
    {
        public MessagePropertyDescription(string name) : base(name, "") { }

        internal MessagePropertyDescription(MessagePropertyDescription other) : base(other) { }

        public override MessagePartDescription Clone()
        {
            return new MessagePropertyDescription(this);
        }
    }
}
