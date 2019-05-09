using Microsoft.Runtime;

namespace Microsoft.ServiceModel.Channels
{
    public abstract class BindingElement
    {
        protected BindingElement() { }
#pragma warning disable RECS0154 // Parameter is never used
        protected BindingElement(BindingElement elementToBeCloned) { }
#pragma warning restore RECS0154 // Parameter is never used

        //public virtual Microsoft.ServiceModel.Channels.IChannelFactory<TChannel> BuildChannelFactory<TChannel>(Microsoft.ServiceModel.Channels.BindingContext context) { return default(Microsoft.ServiceModel.Channels.IChannelFactory<TChannel>); } // Client
        //public virtual bool CanBuildChannelFactory<TChannel>(Microsoft.ServiceModel.Channels.BindingContext context) { return default(bool); } // Client

        public abstract BindingElement Clone();

        public virtual IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context) where TChannel : class, IChannel
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");

            return context.BuildInnerChannelListener<TChannel>();
        }

        public virtual bool CanBuildChannelListener<TChannel>(BindingContext context) where TChannel : class, IChannel
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");

            return context.CanBuildInnerChannelListener<TChannel>();
        }

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
    }
}