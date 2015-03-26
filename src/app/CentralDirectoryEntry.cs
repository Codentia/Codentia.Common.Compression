using System;
using System.Collections.Generic;
using System.IO;

namespace Codentia.Common.Compression
{
    /// <summary>
    /// CentralDirectory Header Position
    /// </summary>
    internal enum CentralDirectoryHeaderPosition
    {
        /*      Skipped fields                                                                                                                                                      
                extra field (variable size)
                file comment (variable size)
        */
        
        /// <summary>
        /// central file header signature   4 bytes  (0x02014b50) - { 80, 75, 1, 2 }
        /// </summary>
        Signature = 0,

        /// <summary>        
        /// version made by                 2 bytes {10, 0} from local
        /// </summary>
        VersionMadeBy = 4,

        /// <summary>
        /// version needed to extract       2 bytes {10, 0} from local
        /// </summary>
        VersionNeededToExtract = 6,

        /// <summary>
        /// general purpose bit flag        2 bytes - from local
        /// </summary>
        GeneralPurpose = 8,

        /// <summary>
        /// compression method              2 bytes - from local
        /// </summary>
        CompressionMethod = 10,

        /// <summary>
        /// last mod file time              2 bytes - treat as 0 in this ver
        /// </summary>
        LastModFileTime = 12,

        /// <summary>
        /// last mod file date              2 bytes - treat as 0 in this ver
        /// </summary>
        LastModFileDate = 14,

        /// <summary>
        /// crc-32                          4 bytes - from localcrc-32                          4 bytes - from local
        /// </summary>
        CRC32 = 16,

        /// <summary>
        /// compressed size                 4 bytes - from local
        /// </summary>
        CompressedSize = 20,

        /// <summary>
        /// uncompressed size               4 bytes - from local
        /// </summary>
        UnCompressedSize = 24,

        /// <summary>
        /// file name length                2 bytes - from local
        /// </summary>
        FileNameLength = 28,

        /// <summary>
        /// extra field length              2 bytes - 0 in this ver
        /// </summary>
        ExtraFieldLength = 30,

        /// <summary>
        /// file comment length             2 bytes  - 0 in this ver
        /// </summary>
        FileCommentLength = 32,

        /// <summary>
        /// disk number start               2 bytes - 0 in this ver
        /// </summary>
        DiskNumberStart = 34,

        /// <summary>
        /// internal file attributes        2 bytes - treat as 0 (largely unused??)
        /// </summary>
        InternalAttributes = 36,

        /// <summary>
        /// external file attributes        4 bytes - treat as 0 (may need to get ntfs file flags to implemented)
        /// </summary>
        ExternalAttributes = 38,

        /// <summary>
        /// relative offset of local header 4 bytes - from file structure ????
        /// </summary>
        RelativeOffsetOfHeader = 42,

        /// <summary>
        /// file name (variable size) - from local
        /// </summary>
        FileName = 46
    }

    /// <summary>
    /// End Of CentralDirectory Position
    /// </summary>
    internal enum EndOfCentralDirectoryPosition
    {
        // end of central directory
        /*                                                            
            Skipped field
            .ZIP file comment       (variable size)
         */

        /// <summary>
        /// end of central dir signature    4 bytes  (0x06054b50) 06 05 75 80 - skipped at present after hex analysis of windows zip output
        /// </summary>
        Signature = 0,

        /// <summary>
        /// number of this disk             2 bytes
        /// </summary>
        NumberOfDisk = 4,

        /// <summary>
        /// number of the disk with the start of the central directory  2 bytes
        /// </summary>
        NumberOfDirectoryDisk = 6,

        /// <summary>
        /// total number of entries in the central directory on this disk  2 bytes
        /// </summary>
        TotalNumberOfEntriesThisDisk = 8,

        /// <summary>
        /// total number of entries in the central directory           2 bytes
        /// </summary>
        TotalNumberOfEntries = 10,

        /// <summary>
        /// size of the central directory   4 bytes
        /// </summary>
        SizeOfDirectory = 12,

        /// <summary>
        ///   offset of start of central directory with respect to the starting disk number        4 bytes
        /// </summary>
        OffsetOfDirectory = 16,

        /// <summary>
        ///  .ZIP file comment length        2 bytes 
        /// </summary>
        CommentLength = 18
    }

    /// <summary>
    /// This class represents the ZIP format table of contents (CentralDirectoryEntry/CDE)
    /// </summary>
    public class CentralDirectoryEntry
    {
        private List<ZipEntry> _entries = new List<ZipEntry>();
        private List<byte[]> _entryHeaders = new List<byte[]>();
        private byte[] _fileSignature;
        private byte[] _directoryEnd;

        private int _directorySize;

        /// <summary>
        /// Initializes a new instance of the <see cref="CentralDirectoryEntry"/> class.
        /// </summary>
        public CentralDirectoryEntry()
        {
            _fileSignature = new byte[] { 80, 75, 1, 2 };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CentralDirectoryEntry"/> class from a stream of data (unpacking)
        /// </summary>
        /// <param name="zipStream">The zip stream.</param>
        public CentralDirectoryEntry(MemoryStream zipStream)
        {
            // we have the cde here (whole thing)
            // this is a set of file headers
            // so we need to split it by file signature - (0x02014b50) - { 80, 75, 1, 2 }
            // each position of this sig denotes a new header (and the entry goes on until the next sig begins)

            // once we know how many entries we have
            // NOTE: this should use number of entries in central dir

            // we can read each entry and reconstruct here - this object will need to build the zip entries
            zipStream.Seek(0, SeekOrigin.Begin);
            int endOfCentralDirectory = ZipArchive.FindSignature(zipStream, new byte[] { 80, 75, 5, 6 }, true);

            _directoryEnd = new byte[zipStream.Length - endOfCentralDirectory];
            zipStream.Seek(endOfCentralDirectory, SeekOrigin.Begin);
            zipStream.Read(_directoryEnd, 0, _directoryEnd.Length);

            int numberOfEntries = ZipArchive.GetInt16(zipStream, endOfCentralDirectory + (int)EndOfCentralDirectoryPosition.TotalNumberOfEntries);
            int centralDirectoryOffset = ZipArchive.GetInt32(zipStream, endOfCentralDirectory + (int)EndOfCentralDirectoryPosition.OffsetOfDirectory);
            _directorySize = ZipArchive.GetInt32(zipStream, endOfCentralDirectory + (int)EndOfCentralDirectoryPosition.SizeOfDirectory);

            // find all cd file headers
            List<int> centralDirectoryFileHeaders = new List<int>(numberOfEntries);
            int current = 0;
            while ((current = ZipArchive.FindSignature(zipStream, new byte[] { 80, 75, 1, 2 }, false, current)) != -1)
            {
                centralDirectoryFileHeaders.Add(current);
                current++;
            }

            // now we have a list of directory file headers
            _entryHeaders = new List<byte[]>(centralDirectoryFileHeaders.Count);
            for (int i = 0; i < centralDirectoryFileHeaders.Count; i++)
            {
                BuildCentralFileEntry(zipStream, centralDirectoryFileHeaders[i], i == (centralDirectoryFileHeaders.Count - 1) ? endOfCentralDirectory - 1 : centralDirectoryFileHeaders[i + 1] - 1, centralDirectoryOffset);
            }
        }

        /// <summary>
        /// Gets the byte-array for this central directory
        /// </summary>
        public byte[] CentralDirectoryBytes
        {
            get
            {
                /*                byte[] allBytes = new byte[4 + _directorySize + _directoryEnd.Length];
                                int pointer = 4;

                                byte[] header = new byte[] { 80, 75, 5, 6 };
                                header.CopyTo(allBytes, 0);
                                */

                byte[] allBytes = new byte[_directorySize + _directoryEnd.Length];
                int pointer = 0;

                for (int i = 0; i < _entryHeaders.Count; i++)
                {
                    _entryHeaders[i].CopyTo(allBytes, pointer);
                    pointer += _entryHeaders[i].Length;
                }

                // _eodSignature.CopyTo(allBytes, pointer);
                // pointer += _eodSignature.Length;
                _directoryEnd.CopyTo(allBytes, pointer);

                return allBytes;
            }
        }

        /// <summary>
        /// Gets a list of the ZipEntries included in this central directory
        /// </summary>
        public List<ZipEntry> Entries
        {
            get
            {
                return _entries;
            }
        }

        /// <summary>
        /// Add a ZipEntry to this Central Directory
        /// </summary>
        /// <param name="ze">Entry to be added</param>
        public void AddZipEntry(ZipEntry ze)
        {
            // add a file header to the directory for a zip entry
            byte[] fileHeader = new byte[46 + ze.FileName.Length]; // 46 + variable fields - extra field and comment will be blank in this implementation so just filename

            // set the entry header, this is 0 for the first one, length of first one for second and so forth
            // this is an int32
            int headerOffset = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                headerOffset += _entries[i].TotalEntryLengthBytes;
            }

            BitConverter.GetBytes(headerOffset).CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.RelativeOffsetOfHeader);

            _fileSignature.CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.Signature);
            ze.VersionToExtract.CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.VersionMadeBy);
            ze.VersionToExtract.CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.VersionNeededToExtract);
            ze.GeneralBitFlag.CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.GeneralPurpose);
            ze.CompressionMethod.CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.CompressionMethod);
            
            // skip 4 bytes to leave them as 0 for time + date
            ze.CrC32.CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.CRC32);
            ze.CompressedSize.CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.CompressedSize);
            ze.UnCompressedSize.CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.UnCompressedSize);
            BitConverter.GetBytes(ze.FileName.Length).CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.FileNameLength);
            
            // skip 4 bytes for extra field, command fields
            // write disknumber as 1
            BitConverter.GetBytes(1).CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.DiskNumberStart);
            
            // skip another 6 bytes for attributes
            // now write relative offset of local header - this is the byte position in the entire file of the header's first byte?
            // this is equal to the sum length of all preceeding entries
            // for now we only have a single one so write it as 0
            // skip 4 bytes for offset
            ze.FileName.CopyTo(fileHeader, (int)CentralDirectoryHeaderPosition.FileName);
            
            // suspicious that the above does not quite add up - may look odd in hex
            _entries.Add(ze);
            _entryHeaders.Add(fileHeader);

            BuildEndOfCentralDirectory();

        /*
         * 
         *       internal file attributes: (2 bytes)

      Bits 1 and 2 are reserved for use by PKWARE.

      The lowest bit of this field indicates, if set, that
      the file is apparently an ASCII or text file.  If not
      set, that the file apparently contains binary data.
      The remaining bits are unused in version 1.0.

      The 0x0002 bit of this field indicates, if set, that a 
      4 byte variable record length control field precedes each 
      logical record indicating the length of the record. The 
      record length control field is stored in little-endian byte
      order.  This flag is independent of text control characters, 
      and if used in conjunction with text data, includes any 
      control characters in the total length of the record. This 
      value is provided for mainframe data transfer support.

  external file attributes: (4 bytes)

      The mapping of the external attributes is
      host-system dependent (see 'version made by').  For
      MS-DOS, the low order byte is the MS-DOS directory
      attribute byte.  If input came from standard input, this
      field is set to zero.

  relative offset of local header: (4 bytes)

      This is the offset from the start of the first disk on
      which this file appears, to where the local header should
      be found.  If an archive is in ZIP64 format and the value
      in this field is 0xFFFFFFFF, the size will be in the 
      corresponding 8 byte zip64 extended information extra field.
         * 
         * 
         */
        }

        private void BuildCentralFileEntry(MemoryStream zipStream, int startByte, int endByte, int centralDirectoryOffset)
        {
            byte[] entryHeaderBytes = new byte[endByte - startByte];

            zipStream.Seek(startByte, SeekOrigin.Begin);

            for (int i = 0; i < entryHeaderBytes.Length; i++)
            {
                entryHeaderBytes[i] = (byte)zipStream.ReadByte();
            }

            int headerOffset = BitConverter.ToInt32(entryHeaderBytes, (int)CentralDirectoryHeaderPosition.RelativeOffsetOfHeader);

            _entries.Add(new ZipEntry(zipStream, headerOffset, centralDirectoryOffset));
            _entryHeaders.Add(entryHeaderBytes);
        }

        private void BuildEndOfCentralDirectory()
        {
            // end of central directory
            /*
                end of central dir signature    4 bytes  (0x06054b50) 06 05 75 80 - skipped at present after hex analysis of windows zip output
                number of this disk             2 bytes
                number of the disk with the
                start of the central directory  2 bytes
                total number of entries in the
                central directory on this disk  2 bytes
                total number of entries in
                the central directory           2 bytes
                size of the central directory   4 bytes
                offset of start of central
                directory with respect to
                the starting disk number        4 bytes
                .ZIP file comment length        2 bytes
                .ZIP file comment       (variable size)
             */

            byte[] centralDirectoryEnd = new byte[22]; // assumes no comment

            byte[] signature = new byte[] { 80, 75, 5, 6 };

            signature.CopyTo(centralDirectoryEnd, (int)EndOfCentralDirectoryPosition.Signature);

            // skip 2 bytes for disk stuff
            BitConverter.GetBytes((short)0).CopyTo(centralDirectoryEnd, (int)EndOfCentralDirectoryPosition.NumberOfDirectoryDisk);
            
            // skip 2 for start disk
            BitConverter.GetBytes((short)0).CopyTo(centralDirectoryEnd, (int)EndOfCentralDirectoryPosition.NumberOfDisk);
            
            // number of entries (this disk)
            BitConverter.GetBytes((short)_entries.Count).CopyTo(centralDirectoryEnd, (int)EndOfCentralDirectoryPosition.TotalNumberOfEntriesThisDisk);
            
            // number of entries (total)
            BitConverter.GetBytes((short)_entries.Count).CopyTo(centralDirectoryEnd, (int)EndOfCentralDirectoryPosition.TotalNumberOfEntries);
            
            // central directory size (unclear if this is including this end record or not - i am assuming not)
            // _directorySize = _signature.Length;
            _directorySize = 0;
            for (int i = 0; i < _entryHeaders.Count; i++)
            {
                _directorySize += _entryHeaders[i].Length;
            }

            BitConverter.GetBytes(_directorySize).CopyTo(centralDirectoryEnd, (int)EndOfCentralDirectoryPosition.SizeOfDirectory);
            
            // 14 - offset
            // BitConverter.GetBytes(0).CopyTo(centralDirectoryEnd, 16);
            // this is relative to the start of the "disk"
            // therefore it is..? total size of all entries??
            int offset = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                offset += _entries[i].ZipEntryBytes.Length;
            }

            BitConverter.GetBytes(offset).CopyTo(centralDirectoryEnd, (int)EndOfCentralDirectoryPosition.OffsetOfDirectory);
            
            // zip comment length - 16
            BitConverter.GetBytes((short)0).CopyTo(centralDirectoryEnd, (int)EndOfCentralDirectoryPosition.CommentLength);

            _directoryEnd = centralDirectoryEnd;
        }        
    }
}
