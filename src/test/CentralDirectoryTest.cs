using NUnit.Framework;

namespace Codentia.Common.Compression.Test
{
    /// <summary>
    /// Unit testing framework for CentralDirectory class
    /// </summary>
    [TestFixture]
    public class CentralDirectoryTest
    {
        /// <summary>
        /// _001_s the central directory_ valid filename.
        /// </summary>
        [Test]
        public void _001_CentralDirectory_ValidFilename()
        {
            ZipEntry ze = new ZipEntry(@"TestData\Test1.txt", "Test1.txt", ZipCompressionType.Store);
            CentralDirectoryEntry cd = new CentralDirectoryEntry();
            cd.AddZipEntry(ze);
        }
    }
}
