using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace w3gReplayEditor
{
    class Program
    {

        static byte[] InjectNewMapChecksumData(string filepath, byte[] NewCheckSum)
        {
            List<byte> newdatabytes = new List<byte>();
            byte[] replaybuf = File.ReadAllBytes(filepath);
            MemoryStream blocksData = new MemoryStream(replaybuf);
            using (BinaryReader reader = new BinaryReader(blocksData))
            {
                Console.WriteLine(reader.ReadNullTerminatedStringWc3());
                int headersize = reader.ReadInt32();
                int compressedfilesize = reader.ReadInt32();
                int replayheaderversion = reader.ReadInt32();
                int sizeofdecompresseddata = reader.ReadInt32();
                int compressedblocks = reader.ReadInt32();
                int w3modid = reader.ReadInt32();
                uint versionnumber = reader.ReadUInt32();
                ushort buildnumber = reader.ReadUInt16();
                ushort flags = reader.ReadUInt16();
                uint replaylength = reader.ReadUInt32();
                uint headercrc32 = reader.ReadUInt32();
                ushort compressedblocksize = reader.ReadUInt16();
                ushort decompressedblocksize = reader.ReadUInt16();
                ushort checksumofblockheader = reader.ReadUInt16();
                ushort checksumofblockdata = reader.ReadUInt16();
                byte[] decompressed = new byte[decompressedblocksize];
                byte[] compressed = reader.ReadBytes(compressedblocksize);

                using (InflaterInputStream zipStream = new InflaterInputStream(new MemoryStream(compressed)))
                {
                    zipStream.Read(decompressed, 0, decompressedblocksize);
                }

                using (BinaryReader decompressedreader = new BinaryReader(new MemoryStream(decompressed)))
                {
                    using (BinaryWriter decompressedwriter = new BinaryWriter(new MemoryStream(decompressed)))
                    {
                        int maxspaces = 0;
                        for (int i = decompressed.Length - 1; i > 0; i--)
                        {
                            if (decompressed[i] == 0)
                            {
                                maxspaces++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (maxspaces == 0)
                        {
                            Console.WriteLine("No reserved space avaiabled.");
                        }
                        else
                        {
                            Console.WriteLine("Reserved freespace:" + maxspaces+ " bytes."); 
                        }

                        decompressedreader.ReadInt32();
                        decompressedreader.ReadByte();
                        decompressedreader.ReadByte();
                        decompressedreader.ReadNullTerminatedString();

                        if (decompressedreader.ReadByte() == 1)
                        {
                            decompressedreader.ReadByte();
                        }
                        else
                        {
                            decompressedreader.ReadInt32();
                            decompressedreader.ReadInt32();
                        }

                        decompressedreader.ReadNullTerminatedString();

                        decompressedreader.ReadByte();

                        long streampos = decompressedreader.BaseStream.Position;



                        byte[] test = decompressedreader.ReadNullTerminatedStringBytes();

                        decompressedreader.BaseStream.Seek(streampos, SeekOrigin.Begin);

                        File.WriteAllBytes("encoded_old.bin", test);

                        test = decompressedreader.GetDecodedBytes();

                        int OldDecodedDataSize = test.Length;

                        int NewDecodedDataSize = NewCheckSum.Length;



                        if (NewDecodedDataSize > OldDecodedDataSize)
                        {
                            Console.WriteLine("Need use reserved space:" + (NewDecodedDataSize - OldDecodedDataSize) + " bytes.");
                            if (NewDecodedDataSize - OldDecodedDataSize > maxspaces)
                            {
                                Console.WriteLine("Error. No reserved space avaiabled for this replay.");
                            }
                            else
                            {

                            }
                        }


                        File.WriteAllBytes("decoded_old.bin", test);

                        for (int i = 13; i > 8; i--)
                        {
                            test[i] = NewCheckSum[i];
                        }
                        

                        for (int i = 0; i < 20; i++)
                        {
                            test[test.Length - 2 - i] = NewCheckSum[NewCheckSum.Length - 2 - i];
                        }

                        File.WriteAllBytes("decoded_new.bin", test);
                        File.WriteAllBytes("encoded_new.bin", BlizzardEncodeData(test));

                        // Console.WriteLine("Decompressed Bytes" + decompressed.Length);

                        // File.WriteAllBytes("test_decoded.bin", test);
                        //  File.WriteAllBytes("test_encoded.bin", BlizzardEncodeData(NewCheckSum));
                        //  File.WriteAllBytes("test_decoded2.bin", GetDecodedBytes(BlizzardEncodeData(NewCheckSum)));

                        long newstreampos = decompressedreader.BaseStream.Position;

                        decompressedreader.BaseStream.Seek(0, SeekOrigin.Begin);
                        newdatabytes.AddRange(decompressedreader.ReadBytes((int)streampos));
                        newdatabytes.AddRange(BlizzardEncodeData(test));
                        decompressedreader.BaseStream.Seek(newstreampos, SeekOrigin.Begin);
                        newdatabytes.AddRange(decompressedreader.ReadBytes((int)(decompressedreader.BaseStream.Length - newstreampos)));


                       /* if (maxspaces == 0 && NewDecodedDataSize > OldDecodedDataSize)
                        {
                            Console.WriteLine("No reserved space avaiable. Replay can be corrupt. ");
                        }
                        */

                      /*  while (NewDecodedDataSize < OldDecodedDataSize)
                        {
                            newdatabytes.Add(0);
                            NewDecodedDataSize++;
                        }

                        while (OldDecodedDataSize > NewDecodedDataSize)
                        {
                            newdatabytes.RemoveAt(newdatabytes.Count - 1);
                            OldDecodedDataSize--;
                        }
                        */


                        File.WriteAllBytes("reserved.bin", newdatabytes.ToArray());
                    }
                }

            }

            return newdatabytes.ToArray();
        }

        static byte[] GetMapChecksummData(string filepath)
        {
            byte[] decodedbytes = new byte[] { 0x00 };
            byte[] replaybuf = File.ReadAllBytes(filepath);
            MemoryStream blocksData = new MemoryStream(replaybuf);
            using (BinaryReader reader = new BinaryReader(blocksData))
            {
                Console.WriteLine(reader.ReadNullTerminatedStringWc3());
                int headersize = reader.ReadInt32();
                int compressedfilesize = reader.ReadInt32();
                int replayheaderversion = reader.ReadInt32();
                int sizeofdecompresseddata = reader.ReadInt32();
                int compressedblocks = reader.ReadInt32();
                int w3modid = reader.ReadInt32();
                uint versionnumber = reader.ReadUInt32();
                ushort buildnumber = reader.ReadUInt16();
                ushort flags = reader.ReadUInt16();
                uint replaylength = reader.ReadUInt32();
                uint headercrc32 = reader.ReadUInt32();
                ushort compressedblocksize = reader.ReadUInt16();
                ushort decompressedblocksize = reader.ReadUInt16();
                ushort checksumofblockheader = reader.ReadUInt16();
                ushort checksumofblockdata = reader.ReadUInt16();
                byte[] decompressed = new byte[decompressedblocksize];
                byte[] compressed = reader.ReadBytes(compressedblocksize);

                using (InflaterInputStream zipStream = new InflaterInputStream(new MemoryStream(compressed)))
                {
                    zipStream.Read(decompressed, 0, decompressedblocksize);
                }

                using (BinaryReader decompressedreader = new BinaryReader(new MemoryStream(decompressed)))
                {
                    using (BinaryWriter decompressedwriter = new BinaryWriter(new MemoryStream(decompressed)))
                    {
                        decompressedreader.ReadInt32();
                        decompressedreader.ReadByte();
                        decompressedreader.ReadByte();
                        decompressedreader.ReadNullTerminatedString();

                        if (decompressedreader.ReadByte() == 1)
                        {
                            decompressedreader.ReadByte();
                        }
                        else
                        {
                            decompressedreader.ReadInt32();
                            decompressedreader.ReadInt32();
                        }

                        decompressedreader.ReadNullTerminatedString();

                        decompressedreader.ReadByte();

                        long streampos = decompressedreader.BaseStream.Position;

                        decodedbytes = decompressedreader.GetDecodedBytes();
                    }
                }

            }

            return decodedbytes;
        }


        static void TestPacker(string ReplayPath)
        {
            //{
            //    Console.WriteLine("Unknown file. Return crc32:");
            //    Console.WriteLine("Crc32 #1:" + Checksums.Crc32.Compute(replaybuf).ToString("x2"));
            //    Console.WriteLine("Crc32 #2:" + WarcraftProtectedCrc16(replaybuf).ToString("x2"));

            //    return;
            //}

            byte[] replaybuf = File.ReadAllBytes(ReplayPath);
            List<byte> newreplaybuf = new List<byte>();
            MemoryStream blocksData = new MemoryStream(replaybuf);
            using (BinaryReader reader = new BinaryReader(blocksData))
            {
                Console.WriteLine(reader.ReadNullTerminatedStringWc3());
                int headersize = reader.ReadInt32();
                int compressedfilesize = reader.ReadInt32();
                int replayheaderversion = reader.ReadInt32();
                int sizeofdecompresseddata = reader.ReadInt32();
                int compressedblocks = reader.ReadInt32();
                int w3modid = reader.ReadInt32();
                uint versionnumber = reader.ReadUInt32();
                ushort buildnumber = reader.ReadUInt16();
                ushort flags = reader.ReadUInt16();
                uint replaylength = reader.ReadUInt32();
                uint headercrc32 = reader.ReadUInt32();

                long curstreampos = reader.BaseStream.Position;

                ushort compressedblocksize = reader.ReadUInt16();
                ushort decompressedblocksize = reader.ReadUInt16();
                ushort checksumofblockheader = reader.ReadUInt16();
                ushort checksumofblockdata = reader.ReadUInt16();
                byte[] decompressed = new byte[decompressedblocksize];
                byte[] compressed = reader.ReadBytes(compressedblocksize);

                long endstreampos = reader.BaseStream.Position;

                using (InflaterInputStream zipStream = new InflaterInputStream(new MemoryStream(compressed)))
                {
                    zipStream.Read(decompressed, 0, decompressedblocksize);
                }

                byte[] outcompresseddate = null;
                long compressedsize = 0;
                using (var ms = new MemoryStream())
                {
                    using (DeflaterOutputStream zipStream = new DeflaterOutputStream(ms))
                    {
                        zipStream.Write(decompressed, 0, decompressed.Length);
                        compressedsize = zipStream.Position;
                    }
                    outcompresseddate = ms.ToArray();
                }

                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                newreplaybuf.AddRange(reader.ReadBytes((int)curstreampos));

                List<byte> newheader1 = new List<byte>();
                List<byte> newheader2 = new List<byte>();

                newheader1.AddRange(BitConverter.GetBytes((ushort)outcompresseddate.Length));
                newheader1.AddRange(BitConverter.GetBytes((ushort)decompressedblocksize));
                newheader2.AddRange(BitConverter.GetBytes((ushort)outcompresseddate.Length));
                newheader2.AddRange(BitConverter.GetBytes((ushort)decompressedblocksize));

                newheader1.Add(0);
                newheader1.Add(0);
                newheader1.Add(0);
                newheader1.Add(0);

                ushort newheadercrc32 = WarcraftProtectedCrc16(newheader1.ToArray());
                ushort newdata32 = WarcraftProtectedCrc16(outcompresseddate);

                newheader2.AddRange(BitConverter.GetBytes(newheadercrc32));
                newheader2.AddRange(BitConverter.GetBytes(newdata32));

                newreplaybuf.AddRange(newheader2.ToArray());
                newreplaybuf.AddRange(outcompresseddate);


                reader.BaseStream.Seek(endstreampos, SeekOrigin.Begin);
                newreplaybuf.AddRange(reader.ReadBytes((int)(reader.BaseStream.Length - endstreampos)));

                while (replaybuf.Length > newreplaybuf.Count)
                {
                    newreplaybuf.Add(0);
                }

                File.WriteAllBytes(ReplayPath + ".new.w3g", newreplaybuf.ToArray());

            }


        }


        static void Main(string[] args)
        {
            Console.WriteLine("Replay CRC32 transfer:");

            Console.WriteLine("Enter replay path:");
            string ReplayPath = Console.ReadLine().Replace("\"", "");
            if (!File.Exists(ReplayPath))
            {
                Console.WriteLine("Bad replay path.");
                return;
            }

            // TestPacker(ReplayPath);
            //return;

            Console.WriteLine("Enter two replay path:");
            string ReplayPath2 = Console.ReadLine().Replace("\"", "");
            if (!File.Exists(ReplayPath2))
            {
                Console.WriteLine("Bad replay path.");
                return;
            }


            //if (Path.GetExtension(ReplayPath).ToLower().IndexOf("w3g") == -1)
            //{
            //    Console.WriteLine("Unknown file. Return crc32:");
            //    Console.WriteLine("Crc32 #1:" + Checksums.Crc32.Compute(replaybuf).ToString("x2"));
            //    Console.WriteLine("Crc32 #2:" + WarcraftProtectedCrc16(replaybuf).ToString("x2"));

            //    return;
            //}

            byte[] replaybuf = File.ReadAllBytes(ReplayPath);
            List<byte> newreplaybuf = new List<byte>();
            MemoryStream blocksData = new MemoryStream(replaybuf);
            using (BinaryReader reader = new BinaryReader(blocksData))
            {
                Console.WriteLine(reader.ReadNullTerminatedStringWc3());
                int headersize = reader.ReadInt32();
                int compressedfilesize = reader.ReadInt32();
                int replayheaderversion = reader.ReadInt32();
                int sizeofdecompresseddata = reader.ReadInt32();
                int compressedblocks = reader.ReadInt32();
                int w3modid = reader.ReadInt32();
                uint versionnumber = reader.ReadUInt32();
                ushort buildnumber = reader.ReadUInt16();
                ushort flags = reader.ReadUInt16();
                uint replaylength = reader.ReadUInt32();
                uint headercrc32 = reader.ReadUInt32();

                long curstreampos = reader.BaseStream.Position;

                ushort compressedblocksize = reader.ReadUInt16();
                ushort decompressedblocksize = reader.ReadUInt16();
                ushort checksumofblockheader = reader.ReadUInt16();
                ushort checksumofblockdata = reader.ReadUInt16();
                byte[] decompressed = new byte[decompressedblocksize];
                byte[] compressed = reader.ReadBytes(compressedblocksize);

                long endstreampos = reader.BaseStream.Position;

                using (InflaterInputStream zipStream = new InflaterInputStream(new MemoryStream(compressed)))
                {
                    zipStream.Read(decompressed, 0, decompressedblocksize);
                }
                /*
                using (BinaryReader decompressedreader = new BinaryReader(new MemoryStream(decompressed)))
                {
                    using (BinaryWriter decompressedwriter = new BinaryWriter(new MemoryStream(decompressed)))
                    {
                        decompressedreader.ReadInt32();
                        decompressedreader.ReadByte();
                        decompressedreader.ReadByte();
                        decompressedreader.ReadNullTerminatedString();

                        if (decompressedreader.ReadByte() == 1)
                        {
                            decompressedreader.ReadByte();
                        }
                        else
                        {
                            decompressedreader.ReadInt32();
                            decompressedreader.ReadInt32();
                        }

                        decompressedreader.ReadNullTerminatedString();

                        decompressedreader.ReadByte();

                        long streampos = decompressedreader.BaseStream.Position;

                        byte[] decodedbytes = decompressedreader.GetDecodedBytes();

                        //BlizzardEncodeData(decodedbytes);


                        using (BinaryReader decodedbytesreader = new BinaryReader(new MemoryStream(decodedbytes)))
                        {

                            decodedbytesreader.ReadInt32();
                            decodedbytesreader.ReadInt32();
                            decodedbytesreader.ReadByte();
                            Console.WriteLine(decodedbytesreader.ReadNullTerminatedStringWc3Encoded());
                        }



                    }
                }*/

               // File.WriteAllBytes("TestReadChecksumDecoded.bin", GetMapChecksummData(ReplayPath2));
              //  File.WriteAllBytes("TestReadChecksumEnoded.bin", BlizzardEncodeData(GetMapChecksummData(ReplayPath2)));
                //File.WriteAllBytes("TestReadChecksumDecoded2.bin", GetMapChecksummData(ReplayPath2));
                //File.WriteAllBytes("TestReadChecksumEnoded2.bin", BlizzardEncodeData(GetMapChecksummData(ReplayPath2)));

                byte[] newdecompresseddata = InjectNewMapChecksumData(ReplayPath, GetMapChecksummData(ReplayPath2));
                byte[] outcompresseddate = null;
                long compressedsize = 0;
                using (var ms = new MemoryStream())
                {
                    using (DeflaterOutputStream zipStream = new DeflaterOutputStream(ms))
                    {
                        zipStream.Write(newdecompresseddata, 0, newdecompresseddata.Length);
                        compressedsize = zipStream.Position;
                    }
                    outcompresseddate = ms.ToArray();
                }
                Console.WriteLine("Decompressed:" + newdecompresseddata.Length + ", Compressed:" + outcompresseddate.Length + "(" + compressedsize + ")");
              //  File.WriteAllBytes("testx.bin", outcompresseddate);
              //  File.WriteAllBytes("testx_afternormal.bin", newdecompresseddata);
              //  File.WriteAllBytes("testx_normal.bin", decompressed);
                Console.WriteLine("Origin compressed:" + compressedblocksize);
                Console.WriteLine("Origin decompressed:" + decompressedblocksize);


                byte[] xdecompressed = new byte[20000];
                int decompressedsize = 0;

                using (InflaterInputStream zipStream = new InflaterInputStream(new MemoryStream(outcompresseddate)))
                {
                    decompressedsize = zipStream.Read(xdecompressed, 0, xdecompressed.Length);
                }
              //  File.WriteAllBytes("testx_bad.bin", xdecompressed);

               // Console.WriteLine("New decompressed:" + decompressedsize);


                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                newreplaybuf.AddRange(reader.ReadBytes((int)curstreampos));

                List<byte> newheader1 = new List<byte>();
                List<byte> newheader2 = new List<byte>();

                newheader1.AddRange(BitConverter.GetBytes((ushort)outcompresseddate.Length));
                newheader1.AddRange(BitConverter.GetBytes((ushort)decompressedsize));
                newheader2.AddRange(BitConverter.GetBytes((ushort)outcompresseddate.Length));
                newheader2.AddRange(BitConverter.GetBytes((ushort)decompressedsize));

                newheader1.Add(0);
                newheader1.Add(0);
                newheader1.Add(0);
                newheader1.Add(0);

                ushort newheadercrc32 = WarcraftProtectedCrc16(newheader1.ToArray());
                ushort newdata32 = WarcraftProtectedCrc16(outcompresseddate);

                newheader2.AddRange(BitConverter.GetBytes(newheadercrc32));
                newheader2.AddRange(BitConverter.GetBytes(newdata32));

                newreplaybuf.AddRange(newheader2.ToArray());
                newreplaybuf.AddRange(outcompresseddate);


                reader.BaseStream.Seek(endstreampos, SeekOrigin.Begin);
                newreplaybuf.AddRange(reader.ReadBytes((int)(reader.BaseStream.Length - endstreampos)));
                if (replaybuf.Length > newreplaybuf.Count)
                {
                    while (replaybuf.Length > newreplaybuf.Count)
                    {
                        newreplaybuf.Add(0);
                    }
                }
                else if (replaybuf.Length < newreplaybuf.Count)
                {
                    Console.WriteLine("Sorry this version can't restore replay header.");
                }
                File.WriteAllBytes(ReplayPath + ".new.w3g", newreplaybuf.ToArray());
            }
            Console.ReadKey();

        }


        public static byte[] GetDecodedBytes(byte[] data)
        {
            List<byte> decoded = new List<byte>();
            using (BinaryReader stream = new BinaryReader(new MemoryStream(data)))
            {
                int pos = 0;
                byte mask = 0;

                byte b = stream.ReadByte();
                while (b != 0)
                {
                    if (pos % 8 == 0)
                    {
                        mask = b;
                    }
                    else
                    {
                        if ((mask & (0x1 << (pos % 8))) == 0)
                            decoded.Add((byte)(b - 1));
                        else
                            decoded.Add(b);
                    }

                    b = stream.ReadByte();
                    pos++;
                }
                decoded.Add(0);
            }
            return decoded.ToArray();
        }

        static ushort WarcraftProtectedCrc16(byte[] data)
        {
            return BitConverter.ToUInt16(BitConverter.GetBytes(BitConverter.ToUInt16(BitConverter.GetBytes(Checksums.Crc32.Compute(data)), 2) ^ Checksums.Crc32.Compute(data)), 0);
        }



        static byte[] BlizzardEncodeData(byte[] data)
        {
            byte Mask = 1;
            List<byte> Result = new List<byte>();

            for (int i = 0; i < data.Length; i++)
            {
                if ((data[i] % 2) == 0)
                    Result.Add((byte)(data[i] + 1));
                else
                {
                    Result.Add(data[i]);
                    Mask |= (byte)(1 << ((i % 7) + 1));
                }

                if (i % 7 == 6 || i == data.Length - 1)
                {
                    Result.Insert(Result.Count - 1 - (i % 7), Mask);
                    Mask = 1;
                }
            }

            Result.RemoveAt(Result.Count - 1);
            Result.Add(0);
            return Result.ToArray();
        }


    }

    static class BinaryReaderImproves
    {
        public static string ReadNullTerminatedString(this System.IO.BinaryReader stream)
        {
            return Encoding.UTF8.GetString(ReadNullTerminatedStringBytes(stream));
        }

        public static byte[] ReadNullTerminatedStringBytes(this System.IO.BinaryReader stream)
        {
            List<byte> str = new List<byte>();

            byte ch;
            while ((ch = stream.ReadByte()) != 0)
            {
                str.Add((byte)ch);
            }

            str.Add(0);
            return str.ToArray();
        }




        public static byte[] GetDecodedBytes(this System.IO.BinaryReader stream)
        {
            List<byte> decoded = new List<byte>();
            int pos = 0;
            byte mask = 0;

            byte b = stream.ReadByte();
            while (b != 0)
            {
                if (pos % 8 == 0)
                {
                    mask = b;
                }
                else
                {
                    if ((mask & (0x1 << (pos % 8))) == 0)
                        decoded.Add((byte)(b - 1));
                    else
                        decoded.Add(b);
                }

                b = stream.ReadByte();
                pos++;
            }
            decoded.Add(0);
            return decoded.ToArray();
        }


        public static string ReadNullTerminatedStringWc3Encoded(this System.IO.BinaryReader stream)
        {
            return Encoding.UTF8.GetString(GetDecodedBytes(stream));
        }

        public static string ReadNullTerminatedStringWc3(this System.IO.BinaryReader stream)
        {
            string str = "";
            char ch;
            while ((ch = stream.ReadChar()) != '\x1A')
                str = str + ch;
            stream.ReadChar();
            return str;
        }

        public static void SkipNullTerminatedString(this System.IO.BinaryReader stream)
        {
            while ((int)(stream.ReadChar()) != 0) ;

        }
    }
}
