﻿using System;

namespace CoreWCF
{
    [AttributeUsage(ServiceModelAttributeTargets.MessageMember, AllowMultiple = false, Inherited = false)]
    public class MessageHeaderAttribute : MessageContractMemberAttribute
    {
        bool _mustUnderstand;
        bool _isMustUnderstandSet;
        bool _relay;
        bool _isRelaySet;
        string _actor;

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