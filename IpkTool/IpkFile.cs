using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Nik
{
    class IpkStream
    {
        readonly Stream stream;
        readonly Encoding enc;
        readonly bool bigendian;

        public IpkStream(Stream s, bool isbigendian = false, Encoding? strenc = null)
        {
            stream = s;
            bigendian = isbigendian;

            if (strenc is null)
                enc = Encoding.UTF8;
            else
                enc = strenc;
        }

        public static int Bswap32(int x)
        {
            //                       C# is weird...
            return unchecked(
                (((x) & -16777216) >> 24) |
                (((x) & 0x00ff0000) >> 8) |
                (((x) & 0x0000ff00) << 8) |
                (((x) & 0x000000ff) << 24)
            );
        }

        public static uint Bswap32(uint x)
        {
            return unchecked(
                (((x) & 0xff000000) >> 24) |
                (((x) & 0x00ff0000) >> 8) |
                (((x) & 0x0000ff00) << 8) |
                (((x) & 0x000000ff) << 24)
            );
        }

        public static ulong Bswap64(ulong x)
        {
            return unchecked(
              ((0x00000000000000FF) & (x >> 56))
            | ((0x000000000000FF00) & (x >> 40))
            | ((0x0000000000FF0000) & (x >> 24))
            | ((0x00000000FF000000) & (x >>  8))
            | ((0x000000FF00000000) & (x <<  8))
            | ((0x0000FF0000000000) & (x << 24))
            | ((0x00FF000000000000) & (x << 40))
            | ((0xFF00000000000000) & (x << 56))
            );
        }

        public static long Bswap64(long x)
        {
            return unchecked(
              ((0x00000000000000FF) & (x >> 56))
            | ((0x000000000000FF00) & (x >> 40))
            | ((0x0000000000FF0000) & (x >> 24))
            | ((0x00000000FF000000) & (x >> 8))
            | ((0x000000FF00000000) & (x << 8))
            | ((0x0000FF0000000000) & (x << 24))
            | ((0x00FF000000000000) & (x << 40))
            | ((-72057594037927936) & (x << 56))
            );
        }

        public async Task<int> ReadInt32()
        {
            var b = new byte[sizeof(int)];
            await stream.ReadAsync(b);
            return bigendian ? Bswap32(BitConverter.ToInt32(b)) : BitConverter.ToInt32(b);
        }

        public async Task<uint> ReadUInt32()
        {
            var b = new byte[sizeof(uint)];
            await stream.ReadAsync(b);
            return bigendian ? Bswap32(BitConverter.ToUInt32(b)) : BitConverter.ToUInt32(b);
        }

        public async Task<long> ReadInt64()
        {
            var b = new byte[sizeof(long)];
            await stream.ReadAsync(b);
            return bigendian ? Bswap64(BitConverter.ToInt64(b)) : BitConverter.ToInt64(b);
        }

        public async Task<ulong> ReadUInt64()
        {
            var b = new byte[sizeof(ulong)];
            await stream.ReadAsync(b);
            return bigendian ? Bswap64(BitConverter.ToUInt64(b)) : BitConverter.ToUInt64(b);
        }

        public async Task<string> ReadPString()
        {
            var v = await ReadUInt32();
            var b = new byte[v];
            await stream.ReadAsync(b);
            return enc.GetString(b);
        }

        public async Task WriteInt32(int v)
        {
            var b = BitConverter.GetBytes(bigendian ? Bswap32(v) : v);
            await stream.WriteAsync(b);
        }

        public async Task WriteUInt32(uint v)
        {
            var b = BitConverter.GetBytes(bigendian ? Bswap32(v) : v);
            await stream.WriteAsync(b);
        }

        public async Task WriteInt64(long v)
        {
            var b = BitConverter.GetBytes(bigendian ? Bswap64(v) : v);
            await stream.WriteAsync(b);
        }

        public async Task WriteUInt64(ulong v)
        {
            var b = BitConverter.GetBytes(bigendian ? Bswap64(v) : v);
            await stream.WriteAsync(b);
        }

        public async Task<byte[]> ReadBytes(uint len)
        {
            var b = new byte[len];
            await stream.ReadAsync(b);
            return b;
        }

        public async Task WriteBytes(byte[] b)
        {
            if (b.Length > 0)
                await stream.WriteAsync(b);
        }

        public async Task WritePString(string v)
        {
            var b = enc.GetBytes(v);
            await WriteInt32(b.Length);
            await WriteBytes(b);
        }

        public async Task WriteAlign(int align, byte padding = 0x0)
        {
            var apad = new byte[1] { padding };
            while ((stream.Position & (align - 1)) != 0)
            {
                await stream.WriteAsync(apad);
            }
        }

        public void Skip(long offset)
        {
            stream.Seek(offset, SeekOrigin.Current);
        }

        public void Rewind(long offset = 0)
        {
            stream.Seek(offset, SeekOrigin.Begin);
        }

        public long Tell()
        {
            return stream.Position;
        }
    }

    public class IpkException : Exception
    {
        public IpkException() : base() { }
        protected IpkException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public IpkException(string? message) : base(message) { }
        public IpkException(string? message, Exception? innerException) : base(message, innerException) { }
    }

    public class IpkItem
    {
        public string FullPath { get; set; }
        public byte[] Contents { get; set; }
        public uint Id { get; set; }

        public IpkItem(string fp, byte[] c, uint i)
        {
            FullPath = fp;
            Contents = c;
            Id = i;
        }
    }

    public static class IpkCrc32
    {
        static uint[]? Table { get; set; }

        public static uint Hash(byte[] input)
        {
            if (Table == null)
            {
                Table = new uint[256];

                var poly = 0xEDB88320u;
                for (uint i = 0u, crc, ii; i < Table.Length; ++i)
                {
                    crc = i;
                    for (ii = 0; ii < 8; ++ii)
                        crc = ((crc & 1) != 0) ? (poly ^ (crc >> 1)) : (crc >> 1);
                    Table[i] = crc;
                }
            }

            // this is kinda wrong but fine for our purposes
            if (input == null)
                return 0;

            var rem = 0xFFFFFFFF;
            for (var i = 0; i < input.Length; ++i)
                rem = Table[(input[i] ^ rem) & 0xff] ^ (rem >> 8);
            // Init() will ensure the table is not null so it's safe

            // complement the thing
            rem ^= 0xFFFFFFFF;
            // we're done here
            return rem;
        }
    }

    public delegate Task IpkLogger(string thing);

    public class IpkFile
    {
        public readonly uint Magic = 0x50ec12ba;
        public readonly int Version = 5;
        public readonly int Version2 = 11;
        // "PATCHIPKHEADER1111111111111\0"
        // the last \0 byte can be used as a format version, but I don't care as of now.
        public readonly byte[] PatchipkHeader = new byte[28] { 0x50, 0x41, 0x54, 0x43, 0x48, 0x49, 0x50, 0x4B, 0x48, 0x45, 0x41, 0x44, 0x45, 0x52, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x00 };
        public byte[] UnknownHeader { get; set; } = new byte[28];
        public List<IpkItem>? Items { get; set; }
        public IpkLogger? Logger { get; set; }

        private async Task Log(string thing)
        {
            
            if (Logger != null)
                await Logger(thing);
        }

        private static async Task<byte[]> UnpackWithZlib(byte[] input)
        {
            using var ms = new MemoryStream(input);
            using var zs = new ZLibStream(ms, CompressionMode.Decompress);
            using var os = new MemoryStream();

            await zs.CopyToAsync(os);
            await zs.FlushAsync();
            return os.ToArray();
        }

        private static async Task<byte[]> CompressWithZlib(byte[] input)
        {
            using var ms = new MemoryStream(input);
            using var os = new MemoryStream();
            using var zs = new ZLibStream(os, CompressionLevel.SmallestSize);

            await ms.CopyToAsync(zs);
            await zs.FlushAsync(); // for some reason it's required for this thing to work...???
            return os.ToArray();
        }

        public async Task ReadFromDirectory(string dirpath)
        {
            await Log("ReadFromDirectory start");

            if (!Directory.Exists(dirpath))
                throw new IpkException($"Ipk directory does not exist. {dirpath}");

            var items = new List<IpkItem>();

            foreach (var f in Directory.EnumerateFiles(dirpath, "*", SearchOption.AllDirectories))
            {
                if (string.IsNullOrEmpty(f))
                    throw new IpkException("Directory item is null???");
                // this will also ignore the output from KtapeTool for good reason
                // (you only want cooked files in your final ipk)
                if (f.EndsWith(".nik"))
                    continue;

                var path = Path.GetRelativePath(dirpath, f).Replace('\\', '/');
                var contents = await File.ReadAllBytesAsync(f);
                var id = uint.Parse(await File.ReadAllTextAsync(f + ".nik"));

                var it = new IpkItem(path, contents, id);
                items.Add(it);
            }

            // TODO bad idea
            var hdr = await File.ReadAllBytesAsync(Path.Combine(dirpath, "meta.nik"));

            Items = items;
            UnknownHeader = hdr;

            await Log("ReadFromDirectory end");
        }

        public async Task WriteToDirectory(string dirpath)
        {
            if (Items is null)
                throw new IpkException("Trying to write an empty ipk file, use Read() first");

            await Log("WriteToDirectory start");

            if (!Directory.Exists(dirpath))
                Directory.CreateDirectory(dirpath);

            foreach (var it in Items)
            {
                var fp = Path.Combine(dirpath, it.FullPath);
                var c = it.Contents;
                var path = Path.GetDirectoryName(fp);
                if (path is null)
                    throw new IpkException($"Unable to concat a path for {it.FullPath}");

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                await File.WriteAllBytesAsync(fp, c);
                await File.WriteAllTextAsync(fp + ".nik", it.Id.ToString());
                await Log($"Wrote {fp} with {c.Length} bytes");
            }

            var infonik = Path.Combine(dirpath, "meta.nik");
            await File.WriteAllBytesAsync(infonik, UnknownHeader);

            await Log("WriteToDirectory end");
        }

        public async Task Write(Stream file)
        {
            if (Items is null)
                throw new IpkException("Trying to write an empty ipk file, use Read() first");

            await Log("Write start");
            // values here are for the latest Switch version of Just Dance 2020
            // may be different per game :(

            var writer = new IpkStream(file, isbigendian: true);
            await writer.WriteUInt32(Magic);
            await writer.WriteInt32(Version);
            await writer.WriteInt32(Version2);
            var base_offset_addr = writer.Tell();
            await writer.WriteUInt32(0); // base offset
            await writer.WriteInt32(Items.Count); // items length

            // ??????????????? unknown
            await writer.WriteBytes(UnknownHeader);
            // ???????????????

            var holes = new List<ulong>();
            var listcontents = new List<byte[]>();

            // in windows utc filetime format, will be used for all files
            // thx ubisoft... <3
            var today = DateTimeOffset.UtcNow.ToFileTime();

            for (int i = 0; i < Items.Count; ++i)
            {
                var it = Items[i];
                if (it == null)
                    throw new IpkException($"Item at index {i} is null o_O");

                var contents = it.Contents;
                var fp = it.FullPath;
                var isckd = fp.EndsWith(".ckd");
                var usezlib =
                    fp.EndsWith(".png.ckd") || // ????????????????????
                    fp.EndsWith(".tga.ckd") || // targa
                    fp.EndsWith(".m3d.ckd") || // ubisoft 3d???
                    fp.EndsWith(".dtape.ckd");
                var path = Path.GetDirectoryName(fp) ?? throw new IpkException("Invalid file path, dirname is null");
                // JD ipk is always using unix path separators
                path = path.Replace('\\', '/');
                if (!path.EndsWith('/')) path += '/';

                await writer.WriteInt32(1);
                // always the raw size here...
                await writer.WriteInt32(contents.Length);

                if (usezlib)
                {
                    // do a zlib pass if needed
                    contents = await CompressWithZlib(contents);
                }

                // will be the raw size again, or zlib if compressed
                await writer.WriteInt32(usezlib ? contents.Length : 0);
                await writer.WriteInt64(today); // in windows FILEINFO, lmao
                var file_offset_addr = writer.Tell();
                await writer.WriteUInt64(0x1337); // file's own offset, dummy for now, see code below
                await writer.WritePString(Path.GetFileName(fp)); // name in JD2020
                await writer.WritePString(path); // path in JD2020, uses unix separators and ends with "/"
                await writer.WriteUInt32(it.Id); // to jedno
                await writer.WriteUInt32(isckd ? 2u : 0u); // to jedno №2

                listcontents.Add(contents); // will be either the zlib or without
                holes.Add((ulong)file_offset_addr); // where to write the file's offset

                await Log($"Adding offset hole for {fp}, {file_offset_addr:X8}");
            }

            // seems to be 4 byte aligned...? makes sense on ARM platforms (Switch)
            await writer.WriteAlign(4);

            // where the files start:
            var base_offset = writer.Tell();
            writer.Rewind(base_offset_addr);
            await writer.WriteUInt32((uint)base_offset);
            writer.Rewind(base_offset);

            // write teh files now, the file is 4byte aligned when this loop starts.
            for (int i = 0; i < listcontents.Count; ++i)
            {
                var here = writer.Tell(); // where the file starts
                await writer.WriteBytes(listcontents[i]);
                await writer.WriteAlign(4); // just in case, Switch is ARM and ARM is sensitive to alignment
                var here_end = writer.Tell(); // where it ends
                writer.Rewind(checked((long)holes[i])); // rewind to where we need to write the offset

                // subtract base offset from file's offset
                await writer.WriteUInt64(checked((ulong)(here - base_offset)));
                writer.Rewind(here_end); // jump back to the end of the current file
                // and so we loop...
                await Log($"Written offset hole {here:X8}");
            }

            // this should end the file 4byte padded.

            await Log("Write end");
        }

        public async Task<IpkFile> DiffWith(IpkFile other)
        {
            // `this` is the original .ipk file
            // `other` is the ipk read from a directory

            // set up null guarantees for the C#'s compiler
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            if (other.Items == null)
                throw new ArgumentException("Trying to pass an empty Ipk into DiffWith", nameof(other));

            if (Items == null)
                throw new IpkException("Trying to operate on an empty Ipk file, use Read() first");

            await Log("DiffWith start");

            var newipk = new IpkFile
            {
                Items = new List<IpkItem>(),
                Logger = Logger,
                // kind of a hack but... who cares... o_o
                UnknownHeader = PatchipkHeader
            };

            foreach (var it in other.Items)
            {
                // C# momento
                if (it == null)
                    throw new IpkException("Item in the ipk seems to be null");

                // I am out of ideas, send help
                if (it.Contents == null)
                    throw new IpkException("Have you eaten today? Don't forget to drink water frequently.");

                // just ignore .nik files, always, they're metadata not intended for the ipk
                if (it.FullPath.EndsWith(".nik"))
                    continue;
                
                var maybeAMatch = Items.Where(x => x.FullPath == it.FullPath).FirstOrDefault();

                // if the file exists and not equal to the folder's one
                if (maybeAMatch != null && !it.Contents.SequenceEqual(maybeAMatch.Contents))
                {
                    // huh? same filename but different contents? add to the patch
                    await Log($"Adding {it.FullPath} to the patchipk...");
                    // as per my arbitrary patchipk spec, that weird "id" in patckipk is the crc32 hash
                    // of the original file's contents.
                    newipk.Items.Add(new IpkItem(it.FullPath, it.Contents, IpkCrc32.Hash(maybeAMatch.Contents)));
                }
            }

            await Log("DiffWith end");

            return newipk;
        }

        public async Task PatchFrom(IpkFile other)
        {
            // `this` is the original .ipk file
            // `other` is the patchipk downloaded from some external source

            // set up null guarantees for the C#'s compiler
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            if (other.Items == null)
                throw new ArgumentException("Trying to pass an empty Ipk into PatchFrom", nameof(other));

            if (Items == null)
                throw new IpkException("Trying to operate on an empty Ipk file, use Read() first");

            await Log("PatchFrom start");

            if (!other.UnknownHeader.SequenceEqual(PatchipkHeader))
                throw new IpkException("The patchipk is not actually a .patchipk file...?");

            foreach (var it in other.Items)
            {
                // C# momento
                if (it == null)
                    throw new IpkException("Item in the ipk seems to be null");

                // ??????????????????????
                if (it.Contents == null)
                    throw new IpkException("rzhaka moment");

                // ignore metadata files
                if (it.FullPath.EndsWith(".nik"))
                    continue;

                var maybeAMatch = Items.Where(x => x.FullPath == it.FullPath).FirstOrDefault();

                if (
                    // if the file is present
                    maybeAMatch != null &&
                        // and the crc32 matches the expected one
                        (it.Id == IpkCrc32.Hash(maybeAMatch.Contents) ||
                        // or both files are just outright equal, in which case it's a no-op
                        it.Contents.SequenceEqual(maybeAMatch.Contents))
                    )
                {
                    await Log($"Patching file {it.FullPath} from patchipk");
                    // as shrimple as that
                    maybeAMatch.Contents = it.Contents;
                }
                else
                {
                    // o_O? oh no
                    throw new IpkException($"Patchipk conflict with file {it.FullPath}, crc32 {it.Id:X}. You're applying a wrong patch or to the wrong ipk.");
                }
            }

            await Log("PatchFrom end");
            // use Write() to write the resulting new patched ipk to disk
            // :p
        }

        public async Task ReadFromStream(Stream file)
        {
            await Log("ReadFromStream start");

            var reader = new IpkStream(file, isbigendian: true);

            int version, version2, files;
            uint magic, base_off;
            if ((magic = await reader.ReadUInt32()) != Magic)
                throw new IpkException($"Invalid magic, expected 0x{Magic:X4} got 0x{magic:X4}");
            if ((version = await reader.ReadInt32()) != Version)
                throw new IpkException($"Invalid version, expected {Version} got {version}");
            if ((version2 = await reader.ReadInt32()) != Version2)
                throw new IpkException($"Invalid version2, expected {Version2} got {version2}");

            base_off = await reader.ReadUInt32();
            files = await reader.ReadInt32();

            // ?????????????????
            UnknownHeader = await reader.ReadBytes((uint)UnknownHeader.Length);

            var items = new List<IpkItem>();

            for (var i = 0; i < files; ++i)
            {
                var fver = await reader.ReadUInt32();
                if (fver != 1)
                    throw new IpkException($"file version is not 1, got {fver}");
                var siz = await reader.ReadUInt32();
                var zsiz = await reader.ReadUInt32();
                var usezlib = zsiz > 0;
                var time = await reader.ReadInt64(); // seems to be in Windows FILEINFO format
                var offs = await reader.ReadUInt64();
                var s1 = await reader.ReadPString(); // name?
                var s2 = await reader.ReadPString(); // path?

                var crc = await reader.ReadUInt32();
                var ftype = await reader.ReadUInt32();

                // # 2014 and 2017 switched the position of NAME and PATH? don't have the old samples
                // paths never contain dots in Ubisoft games
                // names always do no matter what
                var finalname = s2.Contains('.') ? (s1 + s2) : (s2 + s1);
                if (string.IsNullOrEmpty(finalname))
                    throw new IpkException("Invalid full path of item");

                var isckd = finalname.Contains(".ckd");
                
                if ((isckd && ftype != 2) || (!isckd && ftype != 0))
                    throw new IpkException($"file type is invalid, {ftype}");

                var bk = reader.Tell();
                reader.Rewind(checked((long)(base_off + offs)));
                var contents = await reader.ReadBytes(usezlib ? zsiz : siz);
                reader.Rewind(bk);

                if (usezlib)
                {
                    contents = await UnpackWithZlib(contents);
                    
                    // ???????????????? this happened to me a few times and then just... stopped???
                    if (contents.Length != siz)
                        throw new IpkException($"Size mismatch between decompressed and expected. {contents.Length} / {siz}, {finalname}");
                    
                }

                // TODO: figure out the crc of this weird fucking thing...
                // var test = IpkCrc32.Hash(Encoding.UTF8.GetBytes(s2));

                var it = new IpkItem(finalname, contents, crc);
                await Log($"{finalname} = {siz} bytes, zlib = {usezlib}, date = {DateTimeOffset.FromFileTime(time)}");
                items.Add(it);
            }

            // will discard old contents
            Items = items;

            await Log("ReadFromStream end");
        }
    }
}
