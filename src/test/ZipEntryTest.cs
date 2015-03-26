using System.IO;
using NUnit.Framework;

namespace Codentia.Common.Compression.Test
{    
    /// <summary>
    /// Unit testing framework for ZipEntry class
    /// </summary>
    [TestFixture]
    public class ZipEntryTest
    {
        /// <summary>
        /// _001_s the length of the read fully_ invalid buffer.
        /// </summary>
        [Test]
        public void _001_ReadFully_InvalidBufferLength()
        {
            FileInfo fi = new FileInfo("TestData/SampleBin/Microsoft.ReportViewer.Common.dll");
            Stream s = File.OpenRead("TestData/SampleBin/Microsoft.ReportViewer.Common.dll");

            byte[] b = ZipEntry.ReadFully(s, -1);

            s.Close();
            s.Dispose();

            Assert.That(b.Length, Is.EqualTo(fi.Length));
        }

        /// <summary>
        /// _002_s the read fully_ buffer too small.
        /// </summary>
        [Test]
        public void _002_ReadFully_BufferTooSmall()
        {
            FileInfo fi = new FileInfo("TestData/SampleBin/Microsoft.ReportViewer.Common.dll");
            Stream s = File.OpenRead("TestData/SampleBin/Microsoft.ReportViewer.Common.dll");

            byte[] b = ZipEntry.ReadFully(s, 50);

            s.Close();
            s.Dispose();

            Assert.That(b.Length, Is.EqualTo(fi.Length));
        }

        /// <summary>
        /// _002_s the read fully_ buffer too big.
        /// </summary>
        [Test]
        public void _002_ReadFully_BufferTooBig()
        {
            FileInfo fi = new FileInfo("TestData/SampleBin/Microsoft.ReportViewer.Common.dll");
            Stream s = File.OpenRead("TestData/SampleBin/Microsoft.ReportViewer.Common.dll");

            byte[] b = ZipEntry.ReadFully(s, fi.Length * 2);

            s.Close();
            s.Dispose();

            Assert.That(b.Length, Is.EqualTo(fi.Length));
        }
    }
}
