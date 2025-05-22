using System;
using System.Threading;
using System.Threading.Tasks;

namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// Holds runtime information about a process executed by the kernel.
    /// </summary>
    public class ProcessInfo
    {
        public ProcessInfo(ProcessBase process, CancellationTokenSource cts)
        {
            Process = process;
            CancellationTokenSource = cts;
            StartTime = DateTime.UtcNow;
            StartMemory = GC.GetTotalMemory(false);
        }

        public ProcessBase Process { get; }
        public CancellationTokenSource CancellationTokenSource { get; }
        public DateTime StartTime { get; }
        public long StartMemory { get; }
        public DateTime? EndTime { get; private set; }
        public long? EndMemory { get; private set; }
        public Task? Task { get; private set; }

        public TimeSpan? Duration => EndTime.HasValue ? EndTime - StartTime : null;
        public long? MemoryUsed => EndMemory.HasValue ? EndMemory - StartMemory : null;

        public void Start(string[] args)
        {
            Task = Process.StartAsync(args, CancellationTokenSource.Token);
            Task.ContinueWith(_ =>
            {
                EndTime = DateTime.UtcNow;
                EndMemory = GC.GetTotalMemory(false);
            });
        }
    }
}
