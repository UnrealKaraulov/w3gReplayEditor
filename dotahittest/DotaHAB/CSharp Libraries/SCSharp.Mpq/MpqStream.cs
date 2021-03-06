#define WITH_ZLIB
//#define WITH_BZIP
#define WITH_PK

//
// MpqHuffman.cs
//
// Authors:
//		Foole (fooleau@gmail.com)
//
// (C) 2006 Foole (fooleau@gmail.com)
// Based on code from StormLib by Ladislav Zezula
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.IO;
#if WITH_ZLIB
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
#endif
#if WITH_BZIP
using ICSharpCode.SharpZipLib.BZip2;
#endif

namespace MpqReader
{
    /// <summary>
    /// A Stream based class for reading a file from an MPQ file
    /// </summary>
    public class MpqStream : Stream
    {
        private Stream mStream;
        private int mBlockSize;

        private MpqBlock mBlock;
        private uint[] mBlockPositions;
        private uint mSeed1;

        private long mPosition;
        private byte[] mCurrentData;
        private int mCurrentBlockIndex = -1;

        private MpqStream()
        { }

        internal MpqStream(MpqArchive File, MpqBlock Block)
        {
            mBlock = Block;

            mStream = File.BaseStream;
            mBlockSize = File.BlockSize;

            if(mBlock.IsCompressed)
                LoadBlockPositions();
        }

        // Compressed files start with an array of offsets to make seeking possible
        private void LoadBlockPositions()
        {
            int blockposcount = (int) ((mBlock.FileSize + mBlockSize - 1) / mBlockSize) + 1;

            mBlockPositions = new uint[blockposcount];

            lock(mStream)
            {
                mStream.Seek(mBlock.FilePos, SeekOrigin.Begin);
                BinaryReader br = new BinaryReader(mStream);
                for(int i = 0; i < blockposcount; i++)
                    mBlockPositions[i] = br.ReadUInt32();
            }

            uint blockpossize = (uint) blockposcount * 4;
#if true
            // OW:

            // If the archive if protected some way, perform additional check
            // Sometimes, the file appears not to be encrypted, but it is.
            // Edit: In WoW 1.10+, there's a new flag. With this flag present,
            // there's one additional entry in the block table. If this flag
            // is present, we skip this test to keep the file readable.

            if((mBlock.Flags & MpqFileFlags.FileHasMetadata) == 0)
            {
                if(mBlockPositions[0] != blockpossize)
                    mBlock.Flags |= MpqFileFlags.Encrypted;
            }

            if(mBlock.IsEncrypted)
            {
                if(mSeed1 == 0)
                {
                    mSeed1 = MpqArchive.DetectFileSeed(mBlockPositions, blockpossize);
                    if (mSeed1 == 0)
                        throw new MpqParserException("Unable to determine encryption seed");
                }
                
                MpqArchive.DecryptBlock(mBlockPositions, mSeed1);
                mSeed1++; // Add 1 because the first block is the offset list
            }
#else
            // StormLib takes this to mean the data is encrypted
            if (mBlockPositions[0] != blockpossize)
            {
                if (mSeed1 == 0)
                {
                    mSeed1 = MpqArchive.DetectFileSeed(mBlockPositions, blockpossize);
                    if (mSeed1 == 0)
                        throw new Exception("Unable to determine encyption seed");
                }
                MpqArchive.DecryptBlock(mBlockPositions, mSeed1);
                mSeed1++; // Add 1 because the first block is the offset list
            }
#endif
        }

        private byte[] LoadBlock(int BlockIndex, int ExpectedLength)
        {
            uint offset;
            int toread;

            if (mBlock.IsCompressed)
            {
                offset = mBlockPositions[BlockIndex];
                toread = (int)(mBlockPositions[BlockIndex + 1] - offset);
            }
            else
            {
                offset = (uint)(BlockIndex * mBlockSize);
                toread = ExpectedLength;
            }
            offset += mBlock.FilePos;

            byte[] data = new byte[toread];
            lock (mStream)
            {
                mStream.Seek(offset, SeekOrigin.Begin);
                mStream.Read(data, 0, toread);
            }

            if (mBlock.IsEncrypted && mBlock.FileSize > 3)
            {
                if (mSeed1 == 0)
                {
                    uint value0 = BitConverter.ToUInt32(data, 0);
                    uint value1 = BitConverter.ToUInt32(data, 4);
					mSeed1 = MpqArchive.DetectFileSeed(value0, value1, 0x2fbfbbef, 0x3d3d3d2f); // .J unicode magic
                    if (mSeed1 == 0)
                    {
                        mSeed1 = MpqArchive.DetectFileSeed(value0, value1, 0x3d3d2f2f, 0x3d3d3d3d); // .J ascii
	                    if (mSeed1 == 0)
	                    {
                            mSeed1 = MpqArchive.DetectFileSeed(value0, value1, 0x46464952, mBlock.FileSize - 8); // RIFF
                            if (mSeed1 == 0) throw new MpqParserException("Unable to determine encryption key");
                    	}
                    }
                }
                MpqArchive.DecryptBlock(data, (uint)(mSeed1 + BlockIndex));
            }

            if (mBlock.IsCompressed && data.Length != ExpectedLength)
            {
                if ((mBlock.Flags & MpqFileFlags.CompressedMulti) != 0)
                    data = DecompressMulti(data, ExpectedLength);
#if WITH_PK
                else
                    data = PKDecompress(new MemoryStream(data), ExpectedLength);
#endif
            }

            return data;
        }

        #region Stream overrides
        public override bool CanRead
        { get { return true; } }

        public override bool CanSeek
        { get { return true; } }

        public override bool CanWrite
        { get { return false; } }

        public override long Length
        { get { return mBlock.FileSize; } }

        public override long Position
        {
            get
            {
                return mPosition;
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override void Flush()
        {
            // NOP
        }

        public override long Seek(long Offset, SeekOrigin Origin)
        {
            long target;

            switch (Origin)
            {
                case SeekOrigin.Begin:
                    target = Offset;
                    break;
                case SeekOrigin.Current:
                    target = Position + Offset;
                    break;
                case SeekOrigin.End:
                    target = Length + Offset;
                    break;
                default:
                    throw new ArgumentException("Origin", "Invalid SeekOrigin");
            }

            if (target < 0)
                throw new ArgumentOutOfRangeException("Attmpted to Seek before the beginning of the stream");
            if (target >= Length)
                throw new ArgumentOutOfRangeException("Attmpted to Seek beyond the end of the stream");

            mPosition = target;

            return mPosition;
        }

        public override void SetLength(long Value)
        {
            throw new NotSupportedException("SetLength is not supported");
        }

        public override int Read(byte[] Buffer, int Offset, int Count)
        {
            int toread = Count;
            int readtotal = 0;

            while (toread > 0)
            {
                int read = ReadInternal(Buffer, Offset, toread);
                if (read == 0) break;
                readtotal += read;
                Offset += read;
                toread -= read;
            }
            return readtotal;
        }

        private int ReadInternal(byte[] Buffer, int Offset, int Count)
        {
            // OW: avoid reading past the contents of the file
            if (mPosition >= Length)
                return 0;
            
            BufferData();

            int localposition = (int)(mPosition % mBlockSize);
            int bytestocopy = Math.Min(mCurrentData.Length - localposition, Count);
            if (bytestocopy <= 0) return 0;

            Array.Copy(mCurrentData, localposition, Buffer, Offset, bytestocopy);

            mPosition += bytestocopy;
            return bytestocopy;
        }

        public override int ReadByte()
        {
            if (mPosition >= Length) return -1;

            BufferData();

            int localposition = (int)(mPosition % mBlockSize);
            mPosition++;
            return mCurrentData[localposition];
        }

        private void BufferData()
        {
            int requiredblock = (int)(mPosition / mBlockSize);
            if (requiredblock != mCurrentBlockIndex)
            {
                int expectedlength = (int)Math.Min(Length - (requiredblock * mBlockSize), mBlockSize);
                mCurrentData = LoadBlock(requiredblock, expectedlength);
                mCurrentBlockIndex = requiredblock;
            }
        }

        public override void Write(byte[] Buffer, int Offset, int Count)
        {
            throw new NotSupportedException("Writing is not supported");
        }
        #endregion Strem overrides

        /* Compression types in order:
		 *  10 = BZip2
		 *   8 = PKLib
		 *   2 = ZLib
		 *   1 = Huffman
		 *  80 = IMA ADPCM Stereo
		 *  40 = IMA ADPCM Mono
		 */
        private static byte[] DecompressMulti(byte[] Input, int OutputLength)
        {
            Stream sinput = new MemoryStream(Input);

            byte comptype = (byte)sinput.ReadByte();

#if WITH_BZIP
            // BZip2
            if ((comptype & 0x10) != 0)
            {
                byte[] result = BZip2Decompress(sinput, OutputLength);
                comptype &= 0xEF;
                if (comptype == 0) return result;
                sinput = new MemoryStream(result);
            }
#endif

#if WITH_PK

            // PKLib
            if ((comptype & 8) != 0)
            {
                byte[] result = PKDecompress(sinput, OutputLength);
                comptype &= 0xF7;
                if (comptype == 0) return result;
                sinput = new MemoryStream(result);
            }
#endif

#if WITH_ZLIB
            // ZLib
            if ((comptype & 2) != 0)
            {
                byte[] result = ZlibDecompress(sinput, OutputLength);
                comptype &= 0xFD;
                if (comptype == 0) return result;
                sinput = new MemoryStream(result);
            }
#endif

            if ((comptype & 1) != 0)
            {
                byte[] result = MpqHuffman.Decompress(sinput);
                comptype &= 0xfe;
                if (comptype == 0) return result;
                sinput = new MemoryStream(result);
            }

            if ((comptype & 0x80) != 0)
            {
                byte[] result = MpqWavCompression.Decompress(sinput, 2);
                comptype &= 0x7f;
                if (comptype == 0) return result;
                sinput = new MemoryStream(result);
            }

            if ((comptype & 0x40) != 0)
            {
                byte[] result = MpqWavCompression.Decompress(sinput, 1);
                comptype &= 0xbf;
                if (comptype == 0) return result;
                sinput = new MemoryStream(result);
            }
            throw new MpqParserException(String.Format("Unhandled compression flags: 0x{0:X}", comptype));
        }

#if WITH_BZIP
        private static byte[] BZip2Decompress(Stream Data, int ExpectedLength)
        {
            MemoryStream output = new MemoryStream();
            BZip2.Decompress(Data, output);
            return output.ToArray();
        }
#endif

#if WITH_PK
        private static byte[] PKDecompress(Stream Data, int ExpectedLength)
        {
            PKLibDecompress pk = new PKLibDecompress(Data);
            return pk.Explode(ExpectedLength);
        }
#endif

#if WITH_ZLIB
        private static byte[] ZlibDecompress(Stream Data, int ExpectedLength)
        {
            // This assumes that Zlib won't be used in combination with another compression type
            byte[] Output = new byte[ExpectedLength];
            Stream s = new InflaterInputStream(Data);
            int Offset = 0;
            while (true)
            {
                if (ExpectedLength == 0) break;
                int size = s.Read(Output, Offset, ExpectedLength);
                if (size == 0) break;
                Offset += size;
                ExpectedLength -= size;
            }
            return Output;
        }
#endif
    }
}
