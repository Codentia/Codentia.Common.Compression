using System;
using System.Collections.Generic;
using System.IO;

namespace Codentia.Common.Compression
{
    /// <summary>
    /// This class represents a Zip file or Zip Archive.
    /// </summary>
    public class ZipArchive
    {
        private bool _debug = false;
        private string _zipFilePath;
        private CentralDirectoryEntry _centralDirectory;
        private List<ZipEntry> _entries;
        private string _rootPath;        

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipArchive"/> class.
        /// </summary>
        public ZipArchive()
        {
            _centralDirectory = new CentralDirectoryEntry();
            _entries = new List<ZipEntry>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipArchive"/> class. (modification or unpacking)
        /// </summary>
        /// <param name="zipFilePath">file path of zip</param>
        public ZipArchive(string zipFilePath)
        {
            _zipFilePath = zipFilePath;

            // now load the directory and entries

            // hunt for the cde start (signature) and read until the next signature is detected
            // this is one of these 3
            // [zip64 end of central directory record]
            // [zip64 end of central directory locator] 
            // [end of central directory record]

            // so assuming we ignore zip64 for now, its the end of central directory record

            // cd sig: { 80, 75, 1, 2 }
            // eocd sig: { 80, 75, 5, 6 }

            // if we read backwards through the stream to the eocd and load only that
            // we can extract the offset of the cde and load it

            // NOTE: we should handle read only and open as FileAccess.Read, else r/w?
            FileStream fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read);
            MemoryStream ms = new MemoryStream(Convert.ToInt32(fs.Length));
            
            // byte[] fileBytes = new byte[fs.Length];
            for (int i = 0; i < fs.Length; i++)
            {
                // fileBytes[i] = Convert.ToByte(fs.ReadByte());
                ms.WriteByte((byte)fs.ReadByte());
            }

            fs.Close();
            fs.Dispose();

            int endOfCentralDirectory = FindSignature(ms, new byte[] { 80, 75, 5, 6 }, true);

            int startOfCentralDirectory = FindSignature(ms, new byte[] { 80, 75, 1, 2 }, true);

            if (endOfCentralDirectory == -1 || startOfCentralDirectory == -1)
            {
                throw new Exception("Source is not a ZIP archive, or is corrupt");
            }

            // now extract the central directory
            int centralDirectoryLength = endOfCentralDirectory - startOfCentralDirectory;
            byte[] centralDirectoryBytes = new byte[centralDirectoryLength];
            ms.Seek(startOfCentralDirectory, SeekOrigin.Begin);
            for (int i = 0; i < centralDirectoryLength; i++)
            {
                centralDirectoryBytes[i] = (byte)ms.ReadByte();
            }

            _centralDirectory = new CentralDirectoryEntry(ms);
            _entries = _centralDirectory.Entries;
            ms.Close();
            ms.Dispose();
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ZipArchive"/> is in debug mode
        /// </summary>
        /// <value><c>true</c> if debug; otherwise, <c>false</c>.</value>
        public bool Debug
        {
            get
            {
                return _debug;
            }

            set
            {
                _debug = value;
            }
        }

        /// <summary>
        /// Gets an array of the entries in this archive
        /// </summary>
        public ZipEntry[] Entries
        {
            get
            {
                return _entries.ToArray();
            }
        }

        /// <summary>
        /// Add a ZipEntry to this ZipArchive
        /// </summary>
        /// <param name="entry">Entry to be added</param>
        public void AddEntry(ZipEntry entry)
        {
            _entries.Add(entry);
            _entries.Sort();
        }

        /// <summary>
        /// Add a file to this zip archive. If no compression type is specified, this will be determined automatically.
        /// </summary>
        /// <param name="filename">File to be added</param>
        public void AddFile(string filename)
        {
            ZipCompressionType compressionRoutine = ZipCompressionType.Store;

            FileInfo fi = new FileInfo(filename);

            // clearly in the future we will want to make a more intelligent decision here
            if (fi.Length > 1024)
            {
                compressionRoutine = ZipCompressionType.Deflate;
            }

            AddFile(filename, compressionRoutine);
        }

        /// <summary>
        /// Add a file to this archive, specifying the compression type to be used.
        /// </summary>
        /// <param name="filename">File to be added</param>
        /// <param name="compressionRoutine">Compression type to be used</param>
        public void AddFile(string filename, ZipCompressionType compressionRoutine)
        {
            AddFile(filename, compressionRoutine, false);
        }

        /// <summary>
        /// Add an entire directory to the ZipArchive. Defaults to non-recursive.
        /// </summary>
        /// <param name="path">Directory to be added</param>
        public void AddDirectory(string path)
        {
            AddDirectory(path, false);
        }

        /// <summary>
        /// Add an entire directory to the ZipArchive, (optionally) recursively adding all sub-directories.
        /// </summary>
        /// <param name="path">Directory to be added</param>
        /// <param name="recursive">Should sub-directories be added recursively</param>
        public void AddDirectory(string path, bool recursive)
        {
            if (string.IsNullOrEmpty(_rootPath))
            {
                _rootPath = path;
            }

            string[] files = Directory.GetFiles(path);

            for (int i = 0; i < files.Length; i++)
            {
                AddFile(files[i], ZipCompressionType.Deflate, true);
            }

            if (recursive)
            {
                string[] subDirectories = Directory.GetDirectories(path);

                for (int i = 0; i < subDirectories.Length; i++)
                {
                    AddDirectory(subDirectories[i], true);
                }
            }
        }

        /*
        /// <summary>
        /// Remove a specified ZipEntry from this archive
        /// </summary>
        /// <param name="entry">Entry to be removed</param>
        public void RemoveEntry(ZipEntry entry)
        {
            if (_entries.Contains(entry))
            {
                _entries.Remove(entry);
            }
        }
        */

        /// <summary>
        /// Write this ZipArchive out to file.
        /// </summary>
        /// <param name="outputPath">File to be written to</param>
        public void WriteToFile(string outputPath)
        {
            _zipFilePath = outputPath;
            List<byte[]> entryBytes = new List<byte[]>();

            for (int i = 0; i < _entries.Count; i++)
            {
                _centralDirectory.AddZipEntry(_entries[i]);
                entryBytes.Add(_entries[i].ZipEntryBytes);
            }

            byte[] directoryBytes = _centralDirectory.CentralDirectoryBytes;

            FileStream fs = File.OpenWrite(_zipFilePath);

            for (int i = 0; i < entryBytes.Count; i++)
            {
                for (int j = 0; j < entryBytes[i].Length; j++)
                {
                    fs.WriteByte(entryBytes[i][j]);
                }
            }

            for (int i = 0; i < directoryBytes.Length; i++)
            {
                fs.WriteByte(directoryBytes[i]);
            }

            fs.Flush();
            fs.Close();
            fs.Dispose(); 
        }

        /// <summary>
        /// Extract this ZipArchive to a given path.
        /// </summary>
        /// <param name="path">Path to extract to</param>
        public void Extract(string path)
        {
            if (!Directory.Exists(Path.GetFullPath(path)))
            {
                if (_debug)
                {
                    Console.Out.WriteLine(string.Format("Attempting to create directory: '{0}'", Path.GetFullPath(path)));
                }

                Directory.CreateDirectory(Path.GetFullPath(path));
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                string filename = System.Text.Encoding.ASCII.GetString(_entries[i].FileName);

                if (_debug)
                {
                    Console.Out.WriteLine(string.Format("Attempting to unpack file: '{0}'", filename));
                }

                if (filename.EndsWith("/"))
                {
                    if (!Directory.Exists(filename))
                    {
                        Directory.CreateDirectory(filename);
                    }

                    continue;
                }

                // ensure any directory structure is created as filename could contain local directory info
                string folderName = Path.GetDirectoryName(filename);
                if (folderName != string.Empty)
                {
                    folderName = string.Format("{0}{1}{2}", path, Path.DirectorySeparatorChar, folderName);
                    if (!Directory.Exists(Path.GetFullPath(folderName)))
                    {
                        Directory.CreateDirectory(Path.GetFullPath(folderName));
                    }
                }

                int fileLength = _entries[i].FileDataBytes.Length;
                FileStream fs = File.Create(string.Format("{0}{1}{2}", path, Path.DirectorySeparatorChar, filename), fileLength > 0 ? fileLength : 1, FileOptions.None);

                for (int j = 0; j < _entries[i].FileDataBytes.Length; j++)
                {
                    fs.WriteByte(_entries[i].FileDataBytes[j]);
                }

                fs.Close();
                fs.Dispose();
            }
        }

        /// <summary>
        /// Finds the signature.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="signature">The signature.</param>
        /// <param name="preserveStreamPosition">if set to <c>true</c> [preserve stream position].</param>
        /// <returns>int of signature</returns>
        internal static int FindSignature(MemoryStream stream, byte[] signature, bool preserveStreamPosition)
        {
            return FindSignature(stream, signature, preserveStreamPosition, 0);
        }

        /// <summary>
        /// Finds the signature.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="signature">The signature.</param>
        /// <param name="preserveStreamPosition">if set to <c>true</c> [preserve stream position].</param>
        /// <param name="startIndex">The start index.</param>
        /// <returns>int of signature</returns>
        internal static int FindSignature(MemoryStream stream, byte[] signature, bool preserveStreamPosition, int startIndex)
        {
            int offset = -1;

            // if (signature.Length != 4)
            // {
            //    throw new System.NotImplementedException("Signature must be 4 bytes");
            // }
            if (signature.Length == 4)
            {
                long positionBefore = stream.Position;
                stream.Seek(startIndex, SeekOrigin.Begin);

                for (int i = 0; i < (stream.Length - startIndex); i++)
                {
                    if (Convert.ToByte(stream.ReadByte()) == signature[0] && ((i + 3) < Convert.ToInt32(stream.Length)))
                    {
                        long candidatePositionPlusOne = stream.Position;

                        // candidate start of signature
                        if (Convert.ToByte(stream.ReadByte()) == signature[1] && Convert.ToByte(stream.ReadByte()) == signature[2] && Convert.ToByte(stream.ReadByte()) == signature[3])
                        {
                            // Console.Out.WriteLine("Found EndOfCentralDirectorySignature at offset " + i);
                            offset = Convert.ToInt32(candidatePositionPlusOne - 1);
                            break;
                        }
                        else
                        {
                            stream.Seek(candidatePositionPlusOne, SeekOrigin.Begin);
                        }
                    }
                }

                if (preserveStreamPosition)
                {
                    stream.Seek(positionBefore, SeekOrigin.Begin);
                }
            }

            return offset;
        }

        /// <summary>
        /// Gets the int16.
        /// </summary>
        /// <param name="zipStream">The zip stream.</param>
        /// <param name="offsetFromStart">The offset from start.</param>
        /// <returns>int16 of zipStream</returns>
        internal static short GetInt16(MemoryStream zipStream, int offsetFromStart)
        {
            byte[] buffer = new byte[2];
            zipStream.Seek(offsetFromStart, SeekOrigin.Begin);
            zipStream.Read(buffer, 0, 2);

            return BitConverter.ToInt16(buffer, 0);
        }

        /// <summary>
        /// Gets the int32.
        /// </summary>
        /// <param name="zipStream">The zip stream.</param>
        /// <param name="offsetFromStart">The offset from start.</param>
        /// <returns>int of zipStream</returns>
        internal static int GetInt32(MemoryStream zipStream, int offsetFromStart)
        {
            byte[] buffer = new byte[4];
            zipStream.Seek(offsetFromStart, SeekOrigin.Begin);
            zipStream.Read(buffer, 0, 4);

            return BitConverter.ToInt32(buffer, 0);
        }

        private void AddFile(string filename, ZipCompressionType compressionRoutine, bool useRootPath)
        {
            ZipEntry ze = new ZipEntry(filename, useRootPath ? _rootPath : null, compressionRoutine);
            this.AddEntry(ze);
        }
    }
}
