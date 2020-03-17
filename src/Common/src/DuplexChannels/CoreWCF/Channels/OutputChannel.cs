using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    internal abstract class OutputChannel : ServiceChannelBase, IOutputChannel
    {
        protected OutputChannel(IDefaultCommunicationTimeouts timeouts) : base(timeouts) { }

        public abstract EndpointAddress RemoteAddress { get; }

        public abstract Uri Via { get; }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IOutputChannel))
            {
                return (T)(object)this;
            }

            T baseProperty = base.GetProperty<T>();
            if (baseProperty != null)
            {
                return baseProperty;
            }

            return default(T);
        }

        protected abstract Task OnSendAsync(Message message, CancellationToken token);

        public Task SendAsync(Message message)
        {
            return SendAsync(message, CancellationToken.None);
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            if (message == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));

            // TODO: Fix exception message as a negative timeout wasn't passed, a cancelled token was
            if (token.IsCancellationRequested)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentException(SR.SFxTimeoutOutOfRange0, nameof(token)));

            ThrowIfDisposedOrNotOpen();

            AddHeadersTo(message);
            EmitTrace(message);
            return OnSendAsync(message, token);
        }

        private void EmitTrace(Message message)
        {
        }

        protected virtual void AddHeadersTo(Message message)
        {
        }
    }
}
