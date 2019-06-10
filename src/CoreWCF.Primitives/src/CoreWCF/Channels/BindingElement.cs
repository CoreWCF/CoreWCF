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
    }
}