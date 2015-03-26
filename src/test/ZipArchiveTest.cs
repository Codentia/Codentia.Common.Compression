using System;
using System.IO;
using System.Text;
using Codentia.Test.Helper;
using NUnit.Framework;

namespace Codentia.Common.Compression.Test
{
    /// <summary>
    /// Unit testing framework for ZipArchive class
    /// </summary>
    [TestFixture]
    public class ZipArchiveTest
    {
        /// <summary>
        /// Perform test preparation
        /// </summary>
        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {           
            if (Directory.Exists(@"TestData\Out"))
            {
                Directory.Delete(@"TestData\Out", true);
            }

            Directory.CreateDirectory(@"TestData\Out");
           
            if (Directory.Exists(@"TestData\In"))
            {
                Directory.Delete(@"TestData\In", true);
            }

            Directory.CreateDirectory(@"TestData\In");                      
        }

        /// <summary>
        /// _001_s the add file_ no compression.
        /// </summary>
        [Test]
        public void _001_AddFile_NoCompression()
        {
            // create test data
            Directory.CreateDirectory("TestData/In/Test001/");
            Directory.CreateDirectory("TestData/Out/Test001/");
            FileHelper.CreateTextFile("TestData/In/Test001/Test001_1.txt", "This is the test file for Codentia.Common.Compression.ZipArchive Test 001");

            // build archive
            ZipArchive arc = new ZipArchive();
            arc.AddFile("TestData/In/Test001/Test001_1.txt");
            Assert.That(arc.Entries.Length, Is.EqualTo(1), "Expected 1 entry");

            arc.WriteToFile("TestData/Out/Test001.zip");

            // now open the archive and compare to the source
            ZipArchive arc2 = new ZipArchive("TestData/Out/Test001.zip");
            Assert.That(arc2.Entries.Length, Is.EqualTo(arc.Entries.Length));
            Assert.That(arc2.Entries[0].FileName, Is.EqualTo(arc.Entries[0].FileName));

            // now extract and compare the files
            arc2.Extract("TestData/Out/Test001/");
            Assert.That(FileHelper.CompareDirectories("TestData/In/Test001", "TestData/Out/Test001", true), Is.True);
        }

        /// <summary>
        /// _002_s the add file_ deflate.
        /// </summary>
        [Test]
        public void _002_AddFile_Deflate()
        {
            // create test data
            Directory.CreateDirectory("TestData/In/Test002/");
            Directory.CreateDirectory("TestData/Out/Test002/");
            FileHelper.CreateTextFile("TestData/In/Test002/Test002_1.txt", "This is the test file for Codentia.Common.Compression.ZipArchive Test 002");

            ZipArchive arc = new ZipArchive();
            arc.AddFile("TestData/In/Test002/Test002_1.txt", ZipCompressionType.Deflate);
            Assert.That(arc.Entries.Length, Is.EqualTo(1), "Expected 1 entry");

            arc.WriteToFile("TestData/Out/Test002.zip");

            // now open the archive and compare to the source
            ZipArchive arc2 = new ZipArchive("TestData/Out/Test002.zip");
            Assert.That(arc2.Entries.Length, Is.EqualTo(arc.Entries.Length));
            Assert.That(arc2.Entries[0].FileName, Is.EqualTo(arc.Entries[0].FileName));

            // now extract and compare the files
            arc2.Extract("TestData/Out/Test002/");
            Assert.That(FileHelper.CompareDirectories("TestData/In/Test002", "TestData/Out/Test002", true), Is.True);
        }

        /// <summary>
        /// _003_s the add directory_ no recurse.
        /// </summary>
        [Test]
        public void _003_AddDirectory_NoRecurse()
        {
            Directory.CreateDirectory("TestData/In/Test003");
            Directory.CreateDirectory("TestData/Out/Test003");

            Random r = new Random();

            // create a directory containing a few files
            for (int i = 0; i < 10; i++)
            {
                File.WriteAllText(string.Format("TestData/In/Test003/data_{0}.txt", i), CreateRandomText(r));
            }

            ZipArchive arc = new ZipArchive();
            arc.AddDirectory("TestData/In/Test003");
            arc.WriteToFile("TestData/Out/Test003.zip");

            // now open the archive and compare to the source
            ZipArchive arc2 = new ZipArchive("TestData/Out/Test003.zip");
            Assert.That(arc2.Entries.Length, Is.EqualTo(arc.Entries.Length));

            for (int i = 0; i < arc.Entries.Length; i++)
            {
                Assert.That(arc2.Entries[i].FileName, Is.EqualTo(arc.Entries[i].FileName));
            }

            // now extract and compare the files
            arc2.Extract("TestData/Out/Test003/");
            Assert.That(FileHelper.CompareDirectories("TestData/In/Test003", "TestData/Out/Test003", true), Is.True);
        }

        /// <summary>
        /// _004_s the add directory_ recurse.
        /// </summary>
        [Test]
        public void _004_AddDirectory_Recurse()
        {
            Directory.CreateDirectory("TestData/In/Test004");
            Directory.CreateDirectory("TestData/Out/Test004");

            Random r = new Random();

            // randomly create some child directories
            string basePath = "TestData/In/Test004";

            for (int i = 0; i < 5; i++)
            {
                Directory.CreateDirectory(string.Format("{0}/Dir_{1}", basePath, i));
            }

            for (int i = 0; i < Directory.GetDirectories(basePath).Length; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    FileHelper.CreateTextFile(string.Format("{0}/data_{1}.txt", Directory.GetDirectories(basePath)[i], j), CreateRandomText(r));
                }
            }

            ZipArchive arc = new ZipArchive();
            arc.AddDirectory("TestData/In/Test004", true);
            arc.WriteToFile("TestData/Out/Test004.zip");

            // now open the archive and compare to the source
            ZipArchive arc2 = new ZipArchive("TestData/Out/Test004.zip");
            Assert.That(arc2.Entries.Length, Is.EqualTo(arc.Entries.Length));

            for (int i = 0; i < arc.Entries.Length; i++)
            {
                Assert.That(arc2.Entries[i].FileName, Is.EqualTo(arc.Entries[i].FileName));
            }

            // now extract and compare the files
            arc2.Extract("TestData/Out/Test004/");
            Assert.That(FileHelper.CompareDirectories("TestData/In/Test004", "TestData/Out/Test004", true), Is.True);
        }

        /// <summary>
        /// _005_s the extract_ to non existant path.
        /// </summary>
        [Test]
        public void _005_Extract_ToNonExistantPath()
        {
            Directory.CreateDirectory("TestData/In/Test005");
            FileHelper.CreateTextFile("TestData/In/Test005/1.txt", "This is a test file");

            ZipArchive arc = new ZipArchive();
            Assert.That(arc.Debug, Is.False);
            arc.AddFile("TestData/In/Test005/1.txt");
            arc.Extract("TestData/Out/Test005/");

            Assert.That(FileHelper.CompareDirectories("TestData/In/Test005", "TestData/Out/Test005", true), Is.True);
        }

        /// <summary>
        /// Scenario: ZipArchive object is created, attempting to open a file which is not a zip.
        /// Expected: Exception(Source is not a ZIP archive, or is corrupt)
        /// </summary>
        [Test]
        public void _006_Constructor_NotAZipFile()
        {
            string message = string.Empty;           
            FileHelper.CreateTextFile("TestData/In/Test006.txt", "This is a text file. It is not a zip archive. Hopefully it will not open as a zip!");            
            Assert.That(delegate { ZipArchive arc = new ZipArchive("TestData/In/Test006.txt"); }, Throws.Exception.With.Message.EqualTo("Source is not a ZIP archive, or is corrupt"));
        }

        /// <summary>
        /// _007_s the compress directory which contains complex files.
        /// </summary>
        [Test]
        public void _007_CompressDirectoryWhichContainsComplexFiles()
        {
            ZipArchive za = new ZipArchive();
            za.AddDirectory("TestData/SampleBin", true);
            za.WriteToFile("TestData/In/Test007.zip");

            // now extract and compare
            ZipArchive za2 = new ZipArchive("TestData/In/Test007.zip");
            
            // za2.Debug = true;            
            za2.Extract("TestData/Out/SampleBin");

            Assert.That(FileHelper.CompareDirectories("TestData/SampleBin", "TestData/Out/SampleBin", true), Is.True);
        }

        /// <summary>
        /// _008_s the extract_ with sub folder.
        /// </summary>
        [Test]
        public void _008_Extract_WithSubFolder()
        {
            ZipArchive za = new ZipArchive("TestData/NewFolder.zip");
            za.Debug = true;

            za.Extract("TestData/Out/008");
        }

        /// <summary>
        /// _9999_s the test with file which caused ultra corruption.
        /// </summary>
        [Test]
        public void _9999_TestWithFileWhichCausedUltraCorruption()
        {
            // bad_20_NTT_OXFAM.txt
            ZipArchive za = new ZipArchive();
            za.AddFile("TestData/bad_20_NTT_OXFAM.txt");
            za.WriteToFile("TestData/Out/TestBad.zip");
        }

        private string CreateRandomText(Random rng)
        {
            StringBuilder content = new StringBuilder();

            for (int i = 0; i < rng.Next(1000, 100000); i++)
            {
                content.Append((char)rng.Next(32, 126));
            }

            return content.ToString();
        }
    }
}
