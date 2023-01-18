using System;

using BenchmarkDotNet.Running;
using CW_Jesse.BetterNetworking;

namespace CW_Jesse.BetterNetworking_Benchmarks {
    class Program {
        static void Main(string[] args) {
            BenchmarkRunner.Run<BN_CompressionTest>();
        }
    }
}
