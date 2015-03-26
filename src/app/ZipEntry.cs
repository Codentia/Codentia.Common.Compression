using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Codentia.Common.Compression
{
    /// <summary>
    /// Local Header Position
    /// </summary>
    internal enum LocalHeaderPosition
    {
        /*  Skipped fields
            file name (variable size) - as above
            extra field (variable size) - empty - comment field    
         */

        /// <summary>
        /// local file header signature     4 bytes  (0x04034b50)   { 80, 75, 3, 4 }
        /// </summary>
        Signature = 0,

        /// <summary>
        /// version needed to extract       2 bytes                 { 10, 0 }
        /// </summary>
        VersionNeededToExtract = 4,

        /// <summary>
        /// general purpose bit flag        2 bytes                 { 0, 0 } - normal deflate compression
        /// *           (For Methods 8 and 9 - Deflating)
        /// Bit 2  Bit 1
        /// 0      0    Normal (-en) compression option was used.
        /// 0      1    Maximum (-exx/-ex) compression option was used.
        /// 1      0    Fast (-ef) compression option was used.
        /// 1      1    Super Fast (-es) compression option was used.
        /// </summary>
        GeneralPurpose = 6,

        /// <summary>
        /// compression method              2 bytes                 { 8, 0 } - deflate (standard)
        /// </summary>
        CompressionMethod = 8,

        /// <summary>
        /// last mod file time              2 bytes                 { 0, 0 } 
        /// </summary>
        LastModFileTime = 10,

        /// <summary>
        /// last mod file date              2 bytes                 { 0, 0 }
        /// </summary>
        LastModFileDate = 12,

        /// <summary>
        /// crc-32                          4 bytes                 { x, y, x, y } see below 
        /// </summary>
        CRC32 = 14,

        /// <summary>
        /// compressed size                 4 bytes                 { x, y, x, y } - as file
        /// </summary>
        CompressedSize = 18,

        /// <summary>
        /// uncompressed size               4 bytes                 { x, y, x, y } - as file
        /// </summary>
        UnCompressedSize = 22,

        /// <summary>
        /// file name length                2 bytes                 { x, y } - as file (max 65,535 bytes)
        /// </summary>
        FileNameLength = 26,

        /// <summary>
        /// extra field length              2 bytes                 { x, y } - ignore? comment field
        /// </summary>
        ExtraFieldLength = 28
    }
    
    /// <summary>
    /// This class represents an entry within a zip archive (e.g. a single file record)
    /// </summary>
    public class ZipEntry : IComparable
    {
        /*
            Overall .ZIP file format:

            [local file header 1]
            [file data 1]
            [data descriptor 1]
            . 
            .
            .
            [local file header n]
            [file data n]
            [data descriptor n]
            [archive decryption header] 
            [archive extra data record] 
            [central directory]
            [zip64 end of central directory record]
            [zip64 end of central directory locator] 
            [end of central directory record]
         */

        private byte[] _localFileHeader;
        private byte[] _fileData;
        private byte[] _dataDescriptor;

        private ZipCompressionType _compressionType = ZipCompressionType.Store;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipEntry"/> class from a stream of data (un-packing).
        /// </summary>
        /// <param name="zipStream">Stream to read from</param>
        /// <param name="headerOffset">Offset (in bytes) of the header for the entry to be read, from the start of the stream</param>
        /// <param name="centralDirectoryOffset">Offset (in bytes) of the Central Directory from the start of the stream</param>
        public ZipEntry(MemoryStream zipStream, int headerOffset, int centralDirectoryOffset)
        {
            zipStream.Seek(headerOffset, SeekOrigin.Begin);

            byte[] buffer = new byte[2];

            zipStream.Seek((int)LocalHeaderPosition.FileNameLength, SeekOrigin.Current);

            zipStream.Read(buffer, 0, 2);
            short filenameLength = BitConverter.ToInt16(buffer, 0);

            zipStream.Seek(headerOffset, SeekOrigin.Begin);
            zipStream.Seek((int)LocalHeaderPosition.ExtraFieldLength, SeekOrigin.Current);
            zipStream.Read(buffer, 0, 2);            
            short extraFieldLength = BitConverter.ToInt16(buffer, 0);

            short compression = ZipArchive.GetInt16(zipStream, headerOffset + (int)LocalHeaderPosition.CompressionMethod);

            if (compression == 8)
            {
                _compressionType = ZipCompressionType.Deflate;
            }

            // populate file header
            _localFileHeader = new byte[30 + filenameLength + extraFieldLength];
            zipStream.Seek(headerOffset, SeekOrigin.Begin);
            zipStream.Read(_localFileHeader, 0, _localFileHeader.Length);

            // file data is from the end of the header to the next header or central directory start
            int nextSignature = ZipArchive.FindSignature(zipStream, new byte[] { 80, 75, 3, 4 }, true, headerOffset + 1);
            if (nextSignature == -1)
            {
                nextSignature = centralDirectoryOffset;
            }

            if (_compressionType == ZipCompressionType.Store)
            {
                _fileData = new byte[nextSignature - (headerOffset + _localFileHeader.Length)];
                zipStream.Seek(headerOffset + _localFileHeader.Length, SeekOrigin.Begin);
                zipStream.Read(_fileData, 0, _fileData.Length);
            }
            else
            {
                int uncompressedSizeBytes = ZipArchive.GetInt32(zipStream, headerOffset + (int)LocalHeaderPosition.UnCompressedSize);
                
                // MemoryStream ms = new MemoryStream();
                DeflateStream ds = new DeflateStream(zipStream, CompressionMode.Decompress, true);
                zipStream.Seek(headerOffset + _localFileHeader.Length, SeekOrigin.Begin);
                _fileData = new byte[uncompressedSizeBytes];

                for (int i = 0; i < uncompressedSizeBytes; i++)
                {
                    _fileData[i] = (byte)ds.ReadByte();
                }

                ds.Close();
                ds.Dispose();
            }

            _dataDescriptor = new byte[0];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipEntry"/> class.
        /// </summary>
        /// <param name="sourceFilePath">The source file path.</param>
        /// <param name="rootPath">The root path.</param>
        /// <param name="compressionType">Type of the compression.</param>
        public ZipEntry(string sourceFilePath, string rootPath, ZipCompressionType compressionType)
        {
            // ensure relativeFilePath is tidy
            string relativeFileName = null;

            if (!string.IsNullOrEmpty(rootPath))
            {
                relativeFileName = sourceFilePath.Replace(rootPath, string.Empty).Replace(@"\", @"/");
                if (relativeFileName.StartsWith(@"/"))
                {
                    relativeFileName = relativeFileName.Substring(1, relativeFileName.Length - 1);
                }
            }

            // local header
            /*
                local file header signature     4 bytes  (0x04034b50)   { 80, 75, 3, 4 }
                version needed to extract       2 bytes                 { 10, 0 }
                general purpose bit flag        2 bytes                 { 0, 0 } - normal deflate compression

             *           (For Methods 8 and 9 - Deflating)
          Bit 2  Bit 1
            0      0    Normal (-en) compression option was used.
            0      1    Maximum (-exx/-ex) compression option was used.
            1      0    Fast (-ef) compression option was used.
            1      1    Super Fast (-es) compression option was used.
             * 
                compression method              2 bytes                 { 8, 0 } - deflate (standard)
                last mod file time              2 bytes                 { 0, 0 } 
                last mod file date              2 bytes                 { 0, 0 }
                crc-32                          4 bytes                 { x, y, x, y } see below
             * 
             *           The CRC-32 algorithm was generously contributed by
          David Schwaderer and can be found in his excellent
          book "C Programmers Guide to NetBIOS" published by
          Howard W. Sams & Co. Inc.  The 'magic number' for
          the CRC is 0xdebb20e3.  The proper CRC pre and post
          conditioning is used, meaning that the CRC register
          is pre-conditioned with all ones (a starting value
          of 0xffffffff) and the value is post-conditioned by
          taking the one's complement of the CRC residual.
          If bit 3 of the general purpose flag is set, this
          field is set to zero in the local header and the correct
          value is put in the data descriptor and in the central
          directory. When encrypting the central directory, if the
          local header is not in ZIP64 format and general purpose 
          bit flag 13 is set indicating masking, the value stored 
          in the Local Header will be zero. 
             * 
                compressed size                 4 bytes                 { x, y, x, y } - as file
                uncompressed size               4 bytes                 { x, y, x, y } - as file
                file name length                2 bytes                 { x, y } - as file (max 65,535 bytes)
                extra field length              2 bytes                 { x, y } - ignore? comment field

                file name (variable size) - as above
                extra field (variable size)         - empty - comment field    
             */

            // { 80, 75, 3, 4, 10, 0, 0, 8, 0, 0, 0, 0, 0, 4x crc32 bytes, compressed size in 4 bytes, uncompressed size in 4bytes }
            Crc32 crc = new Crc32();

            FileInfo fi = new FileInfo(sourceFilePath);

            byte[] staticFields;

            if (compressionType == ZipCompressionType.Deflate)
            {
                staticFields = new byte[] { 80, 75, 3, 4, 10, 0, 0, 0, 8, 0 }; // last 2 bytes should be 8 for deflate
            }
            else
            {
                staticFields = new byte[] { 80, 75, 3, 4, 10, 0, 0, 0, 0, 0 }; // last 2 bytes should be 8 for deflate
            }

            // StreamReader sr = new StreamReader(File.OpenRead(sourceFilePath));
            Stream s = File.OpenRead(sourceFilePath);

            byte[] fileData = ReadFully(s, fi.Length);
            s.Close();
            s.Dispose();

            uint crcValue = crc.ComputeChecksum(fileData);

            byte[] crcBytes = BitConverter.GetBytes(crcValue);
            byte[] uncompressedSize = BitConverter.GetBytes(fileData.Length);

            // now we need to compress the file and gets its compressed size
            MemoryStream ms = new MemoryStream();

            if (compressionType == ZipCompressionType.Deflate)
            {
                DeflateStream ds = new DeflateStream(ms, CompressionMode.Compress, true);
                    ds.Write(fileData, 0, fileData.Length);

                // for (int i = 0; i < fileData.Length; i++)
                // {
                //    ds.WriteByte(fileData[i]);
                // }
                ds.Flush();
                ds.Close();
                ds.Dispose();
            }
            else
            {
                for (int i = 0; i < fileData.Length; i++)
                {
                    ms.WriteByte(fileData[i]);
                }
            }

            ms.Seek(0, SeekOrigin.Begin);

            byte[] compressedFileData = new byte[ms.Length];
            for (int i = 0; i < compressedFileData.Length; i++)
            {
                compressedFileData[i] = Convert.ToByte(ms.ReadByte());
            }

            ms.Close();
            ms.Dispose();

            byte[] compressedSize = BitConverter.GetBytes(compressedFileData.Length);

            // byte[] filename = System.Text.Encoding.ASCII.GetBytes(Path.GetFileName(sourceFilePath));
            byte[] filename = string.IsNullOrEmpty(relativeFileName) ? System.Text.Encoding.ASCII.GetBytes(fi.Name) : System.Text.Encoding.ASCII.GetBytes(relativeFileName);
            byte[] fileNameFieldLength = BitConverter.GetBytes((short)filename.Length);
            byte[] comment = new byte[0];
            byte[] commentFieldLength = new byte[] { 0, 0 };

            // copy data into the final structure
            byte[] localFileHeader = new byte[30 + filename.Length + comment.Length];
            int pointer = 0;

            staticFields.CopyTo(localFileHeader, pointer);
            pointer += staticFields.Length;

            DateTime lastMod = fi.LastWriteTime;

            // mod time (2b)
            short lastModTime = short.Parse(string.Format("{0}{1}", lastMod.Hour, lastMod.Minute));
            BitConverter.GetBytes(lastModTime).CopyTo(localFileHeader, pointer);
            pointer += 2;

            // mod date (2b)
            short lastModDate = short.Parse(string.Format("{0}{1}", lastMod.Month, lastMod.Day));
            BitConverter.GetBytes(lastModDate).CopyTo(localFileHeader, pointer);
            pointer += 2;

            crcBytes.CopyTo(localFileHeader, pointer);
            pointer += crcBytes.Length;

            compressedSize.CopyTo(localFileHeader, pointer);
            pointer += compressedSize.Length;

            uncompressedSize.CopyTo(localFileHeader, pointer);
            pointer += uncompressedSize.Length;

            fileNameFieldLength.CopyTo(localFileHeader, pointer);
            pointer += fileNameFieldLength.Length;

            commentFieldLength.CopyTo(localFileHeader, pointer);
            pointer += commentFieldLength.Length;

            filename.CopyTo(localFileHeader, pointer);
            pointer += filename.Length;

            comment.CopyTo(localFileHeader, pointer);
            pointer += comment.Length;

            // store data
            _localFileHeader = localFileHeader;
            _fileData = compressedFileData;

            // this should only exist if bit 3 of general purpose flag is set
            // _dataDescriptor = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            _dataDescriptor = new byte[0];
        }

        /*
        /// <summary>
        /// Get the byte array for the local file header
        /// </summary>
        public byte[] LocalFileHeaderBytes
        {
            get
            {
                return _localFileHeader;
            }
        }
        */

        /// <summary>
        /// Gets the data bytes for the file being stored
        /// </summary>
        public byte[] FileDataBytes
        {
            get
            {
                return _fileData;
            }
        }

        /*
        /// <summary>
        /// Get the byte array for the Data Descriptory
        /// </summary>
        public byte[] DataDescriptorBytes
        {
            get
            {
                return _dataDescriptor;
            }
        }
        */

        /// <summary>
        /// Gets the byte array which represents this ZipEntry
        /// </summary>
        public byte[] ZipEntryBytes
        {
            get
            {
                byte[] allBytes = new byte[_localFileHeader.Length + _fileData.Length + _dataDescriptor.Length];

                _localFileHeader.CopyTo(allBytes, 0);
                _fileData.CopyTo(allBytes, _localFileHeader.Length);
                _dataDescriptor.CopyTo(allBytes, _localFileHeader.Length + _fileData.Length);

                return allBytes;
            }
        }

        /// <summary>
        /// Gets the byte array for the CrC32 computed on the file in this entry
        /// </summary>
        public byte[] CrC32
        {
            get
            {
                byte[] section = new byte[4];
                Array.Copy(_localFileHeader, 14, section, 0, 4); 

                return section;
            }
        }

        /// <summary>
        /// Gets the byte array for the compressed size of this file
        /// </summary>
        public byte[] CompressedSize
        {
            get
            {
                byte[] section = new byte[4];
                Array.Copy(_localFileHeader, 18, section, 0, 4);

                return section;
            }
        }

        /// <summary>
        /// Gets the byte array for the uncompressed size of this file
        /// </summary>
        public byte[] UnCompressedSize
        {
            get
            {
                byte[] section = new byte[4];
                Array.Copy(_localFileHeader, 22, section, 0, 4);

                return section;
            }
        }

        /// <summary>
        /// Gets the byte array for the general bit flag from the file header
        /// </summary>
        public byte[] GeneralBitFlag
        {
            get
            {
                byte[] section = new byte[2];
                Array.Copy(_localFileHeader, 6, section, 0, 2);

                return section;
            }
        }

        /// <summary>
        /// Gets the byte array for the compression method
        /// </summary>
        public byte[] CompressionMethod
        {
            get
            {
                byte[] section = new byte[2];
                Array.Copy(_localFileHeader, 8, section, 0, 2);

                return section;
            }
        }

        /// <summary>
        /// Gets the byte array for the filename
        /// </summary>
        public byte[] FileName
        {
            get
            {
                byte[] section = new byte[_localFileHeader.Length - 30];
                Array.Copy(_localFileHeader, 30, section, 0, _localFileHeader.Length - 30);

                return section;
            }
        }

        /// <summary>
        /// Gets the byte array for the VersionToExtract (minimum version of zip specification required to unpack this entry)
        /// </summary>
        public byte[] VersionToExtract
        {
            get
            {
                byte[] section = new byte[2];
                section[0] = 20;
                section[1] = 11;
                
                // Array.Copy(_localFileHeader, 4, section, 0, 2);
                return section;
            }
        }

        /// <summary>
        /// Gets the total length of this entry (in bytes)
        /// </summary>
        public int TotalEntryLengthBytes
        {
            get
            {
                return _localFileHeader.Length + _fileData.Length + _dataDescriptor.Length;
            }
        }

        /*
        /// <summary>
        /// Get the compression type being used by this entry
        /// </summary>
        public ZipCompressionType CompressionType
        {
            get
            {
                return _compressionType;
            }
        }
        */

        #region IComparable Members

        /// <summary>
        /// Compares to.
        /// </summary>
        /// <param name="other">The other object</param>
        /// <returns>int result</returns>
        public int CompareTo(object other)
        {
            int result = -1;

            if (other is ZipEntry)
            {
                ZipEntry otherZipEntry = (ZipEntry)other;

                result = string.Compare(Encoding.ASCII.GetString(otherZipEntry.FileName), Encoding.ASCII.GetString(this.FileName));
            }

            return result;
        }

        #endregion

        /// <summary>
        /// Reads the fully.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="initialLength">The initial length.</param>
        /// <returns>byte array</returns>
        internal static byte[] ReadFully(Stream stream, long initialLength)
        {
            // If we've been passed an unhelpful initial length, just
            // use 32K.
            if (initialLength < 1)
            {
                initialLength = 32768;
            }

            byte[] buffer = new byte[initialLength];
            int read = 0;

            int chunk;
            while ((chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
            {
                read += chunk;

                // If we've reached the end of our buffer, check to see if there's
                // any more information
                if (read == buffer.Length)
                {
                    int nextByte = stream.ReadByte();

                    // End of stream? If so, we're done
                    if (nextByte == -1)
                    {
                        return buffer;
                    }

                    // Nope. Resize the buffer, put in the byte we've just
                    // read, and continue
                    byte[] newBuffer = new byte[buffer.Length * 2];
                    Array.Copy(buffer, newBuffer, buffer.Length);
                    newBuffer[read] = (byte)nextByte;
                    buffer = newBuffer;
                    read++;
                }
            }

            // Buffer is now too big. Shrink it.
            byte[] ret = new byte[read];
            Array.Copy(buffer, ret, read);
            return ret;
        }
    }
}
