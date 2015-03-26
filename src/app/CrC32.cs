namespace Codentia.Common.Compression
{
    /// <summary>
    /// CrC32 generation utility class
    /// </summary>
    public class Crc32
    {
        private uint[] table;

        /// <summary>
        /// Initializes a new instance of the <see cref="Crc32"/> class.
        /// </summary>
        public Crc32()
        {
            uint poly = 0xedb88320;
            table = new uint[256];
            uint temp = 0;
            for (uint i = 0; i < table.Length; i++)
            {
                temp = i;
                for (int j = 8; j > 0; j--)
                {
                    if ((temp & 1) == 1)
                    {
                        temp = (uint)((temp >> 1) ^ poly);
                    }
                    else
                    {
                        temp >>= 1;
                    }
                }

                table[i] = temp;
            }
        }

        /// <summary>
        /// Compute a checksum on a given byte array
        /// </summary>
        /// <param name="bytes">byte array to compute checksum on</param>
        /// <returns>uint of checksum</returns>
        public uint ComputeChecksum(byte[] bytes)
        {
            uint crc = 0xffffffff;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte index = (byte)((crc & 0xff) ^ bytes[i]);
                crc = (uint)((crc >> 8) ^ table[index]);
            }

            return ~crc;
        }        
    }
}