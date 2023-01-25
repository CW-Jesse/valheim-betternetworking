using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using BenchmarkDotNet.Attributes;

using ZstdSharp;
using K4os.Compression.LZ4;
using Ionic.Zlib;

namespace CW_Jesse.BetterNetworking {

    [MemoryDiagnoser]
    public class BN_CompressionTest {

        byte[] bufferToCompress;

        Compressor zstdCompressor, zstdCompressorDict;
        Decompressor zstdDecompressor, zstdDecompressorDict;

        byte[] zstdCompressedBuffer;
        byte[] zstdCompressedBufferDict;

        byte[] lz4CompressedBuffer;

        //[Params(1, 2)]
        // zstd.1 comp: 6.176 ms, 3.4 MB mem, 34.30%
        // zstd.2 comp: 7.365 ms, 34.07%
        // zstd.1 decomp: 2,837.2 us, 2.52 MB mem
        public int levelZstd = 1;
        // lz4.0 comp: 4.330 ms, 1.09 MB mem, 43.25%
        // lz4.9 comp: 47.28 ms, 1.03 MB, 40.88%
        // lz4.12 comp: 253.491 ms, 1.03 MB mem, 40,81%
        // lz4.0 decomp: 802.8 us, 2.52 MB mem
        public LZ4Level levelLz4 = LZ4Level.L00_FAST; // Better Networking v1.2.2; considerably faster, but considerably less compression
        // zlib.1 comp: 38.66 ms, 38.33%
        // zlib.9 comp: 324.220 ms, 36.92%
        public CompressionLevel levelZlib = CompressionLevel.BestCompression; // Valheim default, 

        [GlobalSetup]
        public void GlobalSetup() {
            bufferToCompress = File.ReadAllBytes("TestFile");
            bufferToCompress = bufferToCompress.Take(1024*10*4).ToArray();

            byte[] zstdDict = DictBuilder.TrainFromBuffer(new[] { bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress });
            //byte[] zstdDict = File.ReadAllBytes("dict.dict"); // this dictionary is not trained on the data and has no benefit

            zstdCompressor = new Compressor(levelZstd);
            zstdDecompressor = new Decompressor();

            zstdCompressorDict = new Compressor(levelZstd);
            zstdCompressorDict.LoadDictionary(zstdDict);
            zstdDecompressorDict = new Decompressor();
            zstdDecompressorDict.LoadDictionary(zstdDict);

            zstdCompressedBuffer = zstdCompressor.Wrap(bufferToCompress).ToArray();
            zstdCompressedBufferDict = zstdCompressorDict.Wrap(bufferToCompress).ToArray();
            lz4CompressedBuffer = LZ4Pickler.Pickle(bufferToCompress, levelLz4);
        }

        [GlobalCleanup]
        public void GlobalCleanup() {
            Console.WriteLine($"Original size: {bufferToCompress.Length}");
            Console.WriteLine($"Size (zstd {levelZstd}): {(float)zstdCompressor.Wrap(bufferToCompress).Length / bufferToCompress.Length}");
            Console.WriteLine($"Size (zstdDict {levelZstd}): {(float)zstdCompressorDict.Wrap(bufferToCompress).Length / bufferToCompress.Length}");
            Console.WriteLine($"Size (lz4 {levelLz4}): {(float)LZ4Pickler.Pickle(bufferToCompress, levelLz4).Length / bufferToCompress.Length}");
            Console.WriteLine($"Size (zlib {levelZlib}): {(float)ZlibStream.CompressBuffer(bufferToCompress, levelZlib).Length / bufferToCompress.Length}"); // no good
        }

        [Benchmark] public void CompressBN_New() => zstdCompressor.Wrap(bufferToCompress).ToArray();
        [Benchmark] public void DecompressBN_New() => zstdDecompressor.Unwrap(zstdCompressedBuffer).ToArray();
        [Benchmark] public void CompressBN_NewDict() => zstdCompressorDict.Wrap(bufferToCompress).ToArray();
        [Benchmark] public void DecompressBN_NewDict() => zstdDecompressorDict.Unwrap(zstdCompressedBufferDict).ToArray();
        //[Benchmark] public void TrainDictBN_New() => DictBuilder.TrainFromBuffer(new[] { bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress });
        //[Benchmark]
        //public void CompressBN_NewStream() {
        //    using (zstdCompressionStream = new CompressionStream(new MemoryStream())) {
        //        zstdCompressionStream.Write(bufferToCompress, 0, bufferToCompress.Length);
        //    }
        //}
        [Benchmark] public void CompressBN_Old() => LZ4Pickler.Pickle(bufferToCompress, levelLz4);
        [Benchmark] public void DecompressBN_Old() => LZ4Pickler.Unpickle(lz4CompressedBuffer);
        [Benchmark] public void CompressValheimVanilla() => ZlibStream.CompressBuffer(bufferToCompress, levelZlib); // horrible
    }
}
