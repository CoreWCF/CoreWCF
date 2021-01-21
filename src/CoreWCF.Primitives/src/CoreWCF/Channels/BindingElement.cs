// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Configuration;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public abstract class BindingElement
    {
        protected BindingElement() { }
#pragma warning disable RECS0154 // Parameter is never used
        protected BindingElement(BindingElement elementToBeCloned) { }
#pragma warning restore RECS0154 // Parameter is never used

        //public virtual CoreWCF.Channels.IChannelFactory<TChannel> BuildChannelFactory<TChannel>(CoreWCF.Channels.BindingContext context) { return default(CoreWCF.Channels.IChannelFactory<TChannel>); } // Client
        //public virtual bool CanBuildChannelFactory<TChannel>(CoreWCF.Channels.BindingContext context) { return default(bool); } // Client

        public abstract BindingElement Clone();

        public abstract T GetProperty<T>(BindingContext context) where T : class;

        internal T GetIndividualProperty<T>() where T : class
        {
            return GetProperty<T>(new BindingContext(new CustomBinding(), new BindingParameterCollection()));
        }


        //TODO: Move back to internal
        protected virtual bool IsMatch(BindingElement b)
        {
            Fx.Assert(true, "Should not be called unless this binding element is used in one of the standard bindings. In which case, please re-implement the IsMatch() method.");
            return false;
        }
        public virtual IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher) where TChannel : class, IChannel
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));

            if (innerDispatcher == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(innerDispatcher));

            return context.BuildNextServiceDispatcher<TChannel>(innerDispatcher);
        }

        public virtual bool CanBuildServiceDispatcher<TChannel>(BindingContext context) where TChannel : class, IChannel
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");

            return context.CanBuildNextServiceDispatcher<TChannel>();
        }

    }
}