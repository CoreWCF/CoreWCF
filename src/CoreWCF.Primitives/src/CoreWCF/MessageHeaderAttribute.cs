// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.MessageMember, AllowMultiple = false, Inherited = false)]
    public class MessageHeaderAttribute : MessageContractMemberAttribute
    {
        private bool _mustUnderstand;
        private bool _isMustUnderstandSet;
        private bool _relay;
        private bool _isRelaySet;
        private string _actor;

        public bool MustUnderstand
        {
            get { return _mustUnderstand; }
            set { _mustUnderstand = value; _isMustUnderstandSet = true; }
        }

        public bool Relay
        {
            get { return _relay; }
            set { _relay = value; _isRelaySet = true; }
        }

        public string Actor
        {
            get { return _actor; }
            set { _actor = value; }
        }

        internal bool IsMustUnderstandSet
        {
            get { return _isMustUnderstandSet; }
        }

        internal bool IsRelaySet
        {
            get { return _isRelaySet; }
        }
    }
}