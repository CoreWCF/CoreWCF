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
        Dictionary<string, int> actions;
        ReadOnlyCollection<string> actionSet;

        [DataMember(IsRequired = true)]
        internal string[] DCActions
        {
            get
            {
                string[] act = new string[actions.Count];
                actions.Keys.CopyTo(act, 0);
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

        void Init(string[] actions)
        {
            if (actions.Length == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.ActionFilterEmptyList, nameof(actions)));
            }

            this.actions = new Dictionary<string, int>();
            for (int i = 0; i < actions.Length; ++i)
            {
                // Duplicates are removed
                if (!this.actions.ContainsKey(actions[i]))
                {
                    this.actions.Add(actions[i], 0);
                }
            }
        }

        public ReadOnlyCollection<string> Actions
        {
            get
            {
                if (actionSet == null)
                {
                    actionSet = new ReadOnlyCollection<string>(new List<string>(actions.Keys));
                }
                return actionSet;
            }
        }

        protected internal override IMessageFilterTable<FilterData> CreateFilterTable<FilterData>()
        {
            return new ActionMessageFilterTable<FilterData>();
        }

        bool InnerMatch(Message message)
        {
            string act = message.Headers.Action;
            if (act == null)
            {
                act = string.Empty;
            }

            return actions.ContainsKey(act);
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