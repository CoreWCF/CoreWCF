namespace CoreWCF.Channels
{
    // TODO: Add to contract. This is a feature of an encoder but we don't allow other people writing transports to find out about the
    // encoder implementing compression and having it enabled. There's no way to work around this without reflection if not public so it should be public.
    // BinaryMessageEncoder is the only in-box encoder which supports this.
    public interface ICompressedMessageEncoder
    {
        bool CompressionEnabled { get; }

        void SetSessionContentType(string contentType);

        void AddCompressedMessageProperties(Message message, string supportedCompressionTypes);
    }
}