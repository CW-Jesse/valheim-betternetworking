using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using CW_Jesse.BetterNetworking;

namespace CW_Jesse.BetterNetworking_Benchmarks {
    class Program {
        static void Main(string[] args) {
            //BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig()); // uncomment if you want to debug this
            BenchmarkRunner.Run<BN_CompressionTest>();
        }
    }
}
