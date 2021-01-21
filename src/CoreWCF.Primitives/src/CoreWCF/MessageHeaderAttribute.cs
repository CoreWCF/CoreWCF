// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.MessageMember, AllowMultiple = false, Inherited = false)]
    public class MessageHeaderAttribute : MessageContractMemberAttribute
    {
        private bool _mustUnderstand;
        private bool _relay;
        private string _actor;

        public bool MustUnderstand
        {
            get { return _mustUnderstand; }
            set { _mustUnderstand = value; IsMustUnderstandSet = true; }
        }

        public bool Relay
        {
            get { return _relay; }
            set { _relay = value; IsRelaySet = true; }
        }

        public string Actor
        {
            get { return _actor; }
            set { _actor = value; }
        }

        internal bool IsMustUnderstandSet { get; private set; }

        internal bool IsRelaySet { get; private set; }
    }
}