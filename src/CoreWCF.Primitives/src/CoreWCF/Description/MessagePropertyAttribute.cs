// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Description
{
    // TODO: Make public and add to contract
    [AttributeUsage(CoreWCFAttributeTargets.MessageMember, Inherited = false)]
    internal sealed class MessagePropertyAttribute : Attribute
    {
        string _name;
        bool _isNameSetExplicit;

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
                _isNameSetExplicit = true;
                _name = value;
            }
        }
        internal bool IsNameSetExplicit
        {
            get
            {
                return _isNameSetExplicit;
            }
        }
    }
}