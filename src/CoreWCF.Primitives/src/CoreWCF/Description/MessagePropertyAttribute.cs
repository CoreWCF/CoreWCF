// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Description
{
    [AttributeUsage(CoreWCFAttributeTargets.MessageMember, Inherited = false)]
    public sealed class MessagePropertyAttribute : Attribute
    {
        private string _name;

        public MessagePropertyAttribute()
        {
        }

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                IsNameSetExplicit = true;
                _name = value;
            }
        }

        internal bool IsNameSetExplicit { get; private set; }
    }
}
