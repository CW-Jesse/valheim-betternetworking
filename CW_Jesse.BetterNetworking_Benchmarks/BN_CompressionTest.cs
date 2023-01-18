using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using BenchmarkDotNet.Attributes;

using K4os.Compression.LZ4;
using Ionic.Zlib;
using ZstdNet;

namespace CW_Jesse.BetterNetworking {

    [MemoryDiagnoser]
    public class BN_CompressionTest {

        byte[] bufferToCompress;

        Compressor zstdCompressor;
        Compressor zstdCompressorDict0;
        Compressor zstdCompressorDict1;
        Decompressor zstdDecompressor;
        Decompressor zstdDecompressorDict0;
        Decompressor zstdDecompressorDict1;

        CompressionStream zstdCompressionStream;

        byte[] zstdCompressedBuffer;
        byte[] zstdCompressedBufferDict0;
        byte[] zstdCompressedBufferDict1;

        //[Params(1)]
        public CompressionOptions levelZstd = new CompressionOptions(1); // compression level of 1 is fastest and compresses more than 0
        public CompressionOptions levelZstdDict0;
        public CompressionOptions levelZstdDict1;

        public LZ4Level levelLz4 = LZ4Level.L00_FAST; // Better Networking v1.2.2
        public CompressionLevel levelZlib = CompressionLevel.BestCompression; // Valheim default

        [GlobalSetup]
        public void GlobalSetup() {
            bufferToCompress = File.ReadAllBytes("TestFile");

            byte[] zstdDict0 = DictBuilder.TrainFromBuffer(new[] { bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress });
            byte[] zstdDict1 = File.ReadAllBytes("dict.dict"); // a poorly trained dictionary is worse than no dictionary at all

            zstdCompressor = new Compressor(levelZstd);
            zstdCompressorDict0 = new Compressor(new CompressionOptions(zstdDict0, levelZstd.CompressionLevel));
            zstdCompressorDict1 = new Compressor(new CompressionOptions(zstdDict1, levelZstd.CompressionLevel));
            zstdDecompressor = new Decompressor();
            zstdDecompressorDict0 = new Decompressor(new DecompressionOptions(zstdDict0));
            zstdDecompressorDict1 = new Decompressor(new DecompressionOptions(zstdDict1));

            zstdCompressedBuffer = zstdCompressor.Wrap(bufferToCompress);
            zstdCompressedBufferDict0 = zstdCompressorDict0.Wrap(bufferToCompress);
            zstdCompressedBufferDict1 = zstdCompressorDict1.Wrap(bufferToCompress);
        }

        [GlobalCleanup]
        public void GlobalCleanup() {
            Console.WriteLine("Original size: " + bufferToCompress.Length);
            Console.WriteLine("Size (zstd " + levelZstd.CompressionLevel + "): " + (float)zstdCompressor.Wrap(bufferToCompress).Length / bufferToCompress.Length);
            Console.WriteLine("Size (zstdDict0 " + levelZstd.CompressionLevel + "): " + (float)zstdCompressorDict0.Wrap(bufferToCompress).Length / bufferToCompress.Length);
            Console.WriteLine("Size (zstdDict1 " + levelZstd.CompressionLevel + "): " + (float)zstdCompressorDict1.Wrap(bufferToCompress).Length / bufferToCompress.Length);
            Console.WriteLine("Size (lz4 " + levelLz4 + "): " + (float)LZ4Pickler.Pickle(bufferToCompress, levelLz4).Length / bufferToCompress.Length);
            Console.WriteLine("Size (zlib " + levelZlib + "): " + (float)ZlibStream.CompressBuffer(bufferToCompress, levelZlib).Length / bufferToCompress.Length); // no good
        }

        [Benchmark] public void CompressBN_New() => zstdCompressor.Wrap(bufferToCompress);
        [Benchmark] public void CompressBN_NewDict0() => zstdCompressorDict0.Wrap(bufferToCompress);
        [Benchmark] public void CompressBN_NewDict1() => zstdCompressorDict1.Wrap(bufferToCompress);
        [Benchmark] public void DecompressBN_New() => zstdDecompressor.Unwrap(zstdCompressedBuffer);
        [Benchmark] public void DecompressBN_NewDict0() => zstdDecompressorDict0.Unwrap(zstdCompressedBufferDict0);
        [Benchmark] public void DecompressBN_NewDict1() => zstdDecompressorDict1.Unwrap(zstdCompressedBufferDict1);
        [Benchmark] public void TrainDictBN_New() => DictBuilder.TrainFromBuffer(new[] { bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress, bufferToCompress });
        [Benchmark]
        public void CompressBN_NewStream() {
            using (zstdCompressionStream = new CompressionStream(new MemoryStream())) {
                zstdCompressionStream.Write(bufferToCompress, 0, bufferToCompress.Length);
            }
        }
        [Benchmark] public void CompressBN_Old() => LZ4Pickler.Pickle(bufferToCompress, levelLz4);
        [Benchmark] public void CompressValheimVanilla() => ZlibStream.CompressBuffer(bufferToCompress, levelZlib); // horrible
    }
}
