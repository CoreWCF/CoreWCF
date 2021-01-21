// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Dispatcher
{
    internal class EndpointFilterProvider
    {
        private readonly object mutex;

        public EndpointFilterProvider(params string[] initiatingActions)
        {
            mutex = new object();
            InitiatingActions = new SynchronizedCollection<string>(mutex, initiatingActions);
        }

        public SynchronizedCollection<string> InitiatingActions { get; }

        public MessageFilter CreateFilter(out int priority)
        {
            lock (mutex)
            {
                priority = 1;
                if (InitiatingActions.Count == 0)
                {
                    return new MatchNoneMessageFilter();
                }

                string[] actions = new string[InitiatingActions.Count];
                int index = 0;
                for (int i = 0; i < InitiatingActions.Count; i++)
                {
                    string currentAction = InitiatingActions[i];
                    if (currentAction == MessageHeaders.WildcardAction)
                    {
                        priority = 0;
                        return new MatchAllMessageFilter();
                    }
                    actions[index] = currentAction;
                    ++index;
                }

                return new ActionMessageFilter(actions);
            }
        }
    }

}