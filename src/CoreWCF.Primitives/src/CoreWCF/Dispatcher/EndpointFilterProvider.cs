// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Dispatcher
{
    class EndpointFilterProvider
    {
        SynchronizedCollection<string> initiatingActions;
        object mutex;

        public EndpointFilterProvider(params string[] initiatingActions)
        {
            mutex = new object();
            this.initiatingActions = new SynchronizedCollection<string>(mutex, initiatingActions);
        }

        public SynchronizedCollection<string> InitiatingActions
        {
            get { return initiatingActions; }
        }

        public MessageFilter CreateFilter(out int priority)
        {
            lock (mutex)
            {
                priority = 1;
                if (initiatingActions.Count == 0)
                    return new MatchNoneMessageFilter();

                string[] actions = new string[initiatingActions.Count];
                int index = 0;
                for (int i = 0; i < initiatingActions.Count; i++)
                {
                    string currentAction = initiatingActions[i];
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