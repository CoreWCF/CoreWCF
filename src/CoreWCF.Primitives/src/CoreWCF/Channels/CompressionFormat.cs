namespace CoreWCF.Channels
{
    public enum CompressionFormat
    {
        /// <summary>
        /// Default to compression off
        /// </summary>
        None,

        /// <summary>
        /// GZip compression
        /// </summary>
        GZip,

        /// <summary>
        /// Deflate compression
        /// </summary>
        Deflate,
    }
}