// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public sealed class NonDualMessageSecurityOverHttp : MessageSecurityOverHttp
    {
        internal const bool DefaultEstablishSecurityContext = true;
        private bool establishSecurityContext;

        public NonDualMessageSecurityOverHttp()
            : base()
        {
            this.establishSecurityContext = DefaultEstablishSecurityContext;
        }

        public bool EstablishSecurityContext
        {
            get
            {
                return this.establishSecurityContext;
            }
            set
            {
                this.establishSecurityContext = value;
            }
        }

        protected override bool IsSecureConversationEnabled()
        {
            return this.establishSecurityContext;
        }
    }
}
