namespace Codentia.Common.Compression
{
    /// <summary>
    /// Enumerated type expressing the different sorts of compression supported by zip components
    /// </summary>
    public enum ZipCompressionType
    {
        /// <summary>
        /// Store - place the file, uncompressed, in the archive
        /// </summary>
        Store,

        /// <summary>
        /// Deflate - compress the file using the deflate alogorithm
        /// </summary>
        Deflate
    }
}