namespace CoreWCF.Channels
{
    internal interface ITransportCompressionSupport
    {
        bool IsCompressionFormatSupported(CompressionFormat compressionFormat);
    }
}
