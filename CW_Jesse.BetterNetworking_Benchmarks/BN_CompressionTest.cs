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
        Decompressor zstdDecompressor;

        CompressionStream zstdCompressionStream;
        MemoryStream zStdCompressionStreamStream;
        int compressionStreamCount = 8;


        byte[] zstdCompressedBuffer;

        //[Params(1)]
        public CompressionOptions levelZstd = new CompressionOptions(1); // compression level of 1 is fastest and compresses more than 0
        public LZ4Level levelLz4 = LZ4Level.L00_FAST; // Better Networking v1.2.2
        public CompressionLevel levelZlib = CompressionLevel.BestCompression; // Valheim default

        [GlobalSetup]
        public void GlobalSetup() {
            byte[] dict = File.ReadAllBytes("dict.dict"); // a poorly trained dictionary is worse than no dictionary at all

            bufferToCompress = File.ReadAllBytes("TestFile");

            zstdCompressor = new Compressor(levelZstd);
            zstdDecompressor = new Decompressor();

            zStdCompressionStreamStream = new MemoryStream();
            zstdCompressionStream = new CompressionStream(zStdCompressionStreamStream, levelZstd);

            zstdCompressedBuffer = zstdCompressor.Wrap(bufferToCompress);
        }

        [GlobalCleanup]
        public void GlobalCleanup() {

            Console.WriteLine("Original size: " + bufferToCompress.Length);
            Console.WriteLine("Size (zstd " + levelZstd.CompressionLevel + "): " + (float)zstdCompressor.Wrap(bufferToCompress).Length / bufferToCompress.Length);
            Console.WriteLine("Size (zstdStream " + levelZstd.CompressionLevel + "): " + (float)zStdCompressionStreamStream.Length / compressionStreamCount / bufferToCompress.Length);
            Console.WriteLine("Size (lz4 " + levelLz4 + "): " + (float)LZ4Pickler.Pickle(bufferToCompress, levelLz4).Length / bufferToCompress.Length);
            Console.WriteLine("Size (zlib " + levelZlib + "): " + (float)ZlibStream.CompressBuffer(bufferToCompress, levelZlib).Length / bufferToCompress.Length); // no good

            //zstdCompressionStream.Flush(); // flushed in Dispose()
            zstdCompressionStream.Dispose();
            zStdCompressionStreamStream.Dispose();
        }

        [BenchmarkCategory("New Better Networking compression (v1.3+)")]
        [Benchmark] public void CompressBN_New() => zstdCompressor.Wrap(bufferToCompress);

        [BenchmarkCategory("New Better Networking compression (v1.3+)")]
        [Benchmark]
        public void CompressBN_NewStream() {
            zStdCompressionStreamStream.Seek(0, SeekOrigin.Begin);
            for (int i = 0; i < compressionStreamCount; i++) {
                zstdCompressionStream.Write(bufferToCompress, 0, bufferToCompress.Length);
            }
            //zstdCompressionStream.Flush();
        }

        [BenchmarkCategory("New Better Networking compression (v1.3+)")]
        [Benchmark] public void DecompressZstd() => zstdDecompressor.Unwrap(zstdCompressedBuffer);

        [BenchmarkCategory("Old Better Networking compression (v1.2.2)")]
        [Benchmark] public void CompressBN_Old() => LZ4Pickler.Pickle(bufferToCompress, levelLz4);

        [BenchmarkCategory("Valheim Default")]
        [Benchmark] public void CompressValheimVanilla() => ZlibStream.CompressBuffer(bufferToCompress, levelZlib); // horrible
    }
}
