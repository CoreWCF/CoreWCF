// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    [DataContract]
    internal class ActionMessageFilter : MessageFilter
    {
        private Dictionary<string, int> _actions;
        private ReadOnlyCollection<string> _actionSet;

        [DataMember(IsRequired = true)]
        internal string[] DCActions
        {
            get
            {
                string[] act = new string[_actions.Count];
                _actions.Keys.CopyTo(act, 0);
                return act;
            }
            set
            {
                Init(value);
            }
        }

        public ActionMessageFilter(params string[] actions)
        {
            if (actions == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(actions));
            }

            Init(actions);
        }

        private void Init(string[] actions)
        {
            if (actions.Length == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.ActionFilterEmptyList, nameof(actions)));
            }

            _actions = new Dictionary<string, int>();
            for (int i = 0; i < actions.Length; ++i)
            {
                // Duplicates are removed
                if (!_actions.ContainsKey(actions[i]))
                {
                    _actions.Add(actions[i], 0);
                }
            }
        }

        public ReadOnlyCollection<string> Actions
        {
            get
            {
                if (_actionSet == null)
                {
                    _actionSet = new ReadOnlyCollection<string>(new List<string>(_actions.Keys));
                }
                return _actionSet;
            }
        }

        protected internal override IMessageFilterTable<FilterData> CreateFilterTable<FilterData>()
        {
            return new ActionMessageFilterTable<FilterData>();
        }

        private bool InnerMatch(Message message)
        {
            string act = message.Headers.Action;
            if (act == null)
            {
                act = string.Empty;
            }

            return _actions.ContainsKey(act);
        }

        public override bool Match(Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            return InnerMatch(message);
        }

        public override bool Match(MessageBuffer messageBuffer)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
            }

            Message msg = messageBuffer.CreateMessage();
            try
            {
                return InnerMatch(msg);
            }
            finally
            {
                msg.Close();
            }
        }
    }
}