// Memory Manager Implementation
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Kernel.Memory
{
    /// <summary>
    /// Memory manager implementation for HackerOS kernel
    /// </summary>
    public class MemoryManager : IMemoryManager
    {
        private readonly ILogger<MemoryManager> _logger;
        private readonly ConcurrentDictionary<string, MemoryRegion> _allocatedRegions = new();
        private readonly ConcurrentDictionary<int, List<string>> _processRegions = new();
        private readonly ConcurrentDictionary<int, long> _processMemoryLimits = new();
        
        // Simulated system memory - 8GB total
        private const long TOTAL_SYSTEM_MEMORY = 8L * 1024 * 1024 * 1024;
        private long _currentBaseAddress = 0x10000000; // Start at 256MB

        public event EventHandler<MemoryEvent>? OnMemoryEvent;

        public MemoryManager(ILogger<MemoryManager> logger)
        {
            _logger = logger;
        }

        public async Task<MemoryRegion?> AllocateMemoryAsync(int processId, long size, MemoryProtection protection = MemoryProtection.ReadWrite)
        {
            // Check process memory limit
            if (_processMemoryLimits.TryGetValue(processId, out var limit))
            {
                var currentUsage = GetProcessMemoryUsage(processId);
                if (currentUsage.AllocatedBytes + size > limit)
                {
                    _logger.LogWarning("Memory allocation denied for process {ProcessId}: would exceed limit {Limit}", processId, limit);
                    
                    OnMemoryEvent?.Invoke(this, new MemoryEvent
                    {
                        EventType = MemoryEventType.LimitExceeded,
                        ProcessId = processId,
                        Size = size,
                        Message = $"Process {processId} memory allocation would exceed limit"
                    });
                    
                    return null;
                }
            }

            // Check system memory availability
            var systemInfo = GetSystemMemoryInfo();
            if (systemInfo.AvailableMemory < size)
            {
                _logger.LogError("System out of memory: requested {Size} bytes, available {Available}", size, systemInfo.AvailableMemory);
                
                OnMemoryEvent?.Invoke(this, new MemoryEvent
                {
                    EventType = MemoryEventType.OutOfMemory,
                    ProcessId = processId,
                    Size = size,
                    Message = "System out of memory"
                });
                
                return null;
            }

            var region = new MemoryRegion
            {
                ProcessId = processId,
                BaseAddress = Interlocked.Add(ref _currentBaseAddress, size) - size,
                Size = size,
                Protection = protection,
                Type = DetermineMemoryType(size),
                Description = $"Process {processId} allocation"
            };

            _allocatedRegions[region.Id] = region;
            
            // Track regions per process
            _processRegions.AddOrUpdate(processId, 
                new List<string> { region.Id },
                (key, existing) => { existing.Add(region.Id); return existing; });

            OnMemoryEvent?.Invoke(this, new MemoryEvent
            {
                EventType = MemoryEventType.Allocated,
                ProcessId = processId,
                Size = size,
                Message = $"Allocated {size} bytes for process {processId}"
            });

            _logger.LogDebug("Allocated {Size} bytes at 0x{Address:X} for process {ProcessId}", 
                size, region.BaseAddress, processId);

            return region;
        }

        public async Task<bool> DeallocateMemoryAsync(MemoryRegion region)
        {
            if (!_allocatedRegions.TryRemove(region.Id, out var removedRegion))
            {
                _logger.LogWarning("Attempted to deallocate non-existent memory region {RegionId}", region.Id);
                return false;
            }

            // Remove from process tracking
            if (_processRegions.TryGetValue(region.ProcessId, out var processRegionList))
            {
                processRegionList.Remove(region.Id);
                if (processRegionList.Count == 0)
                {
                    _processRegions.TryRemove(region.ProcessId, out _);
                }
            }

            OnMemoryEvent?.Invoke(this, new MemoryEvent
            {
                EventType = MemoryEventType.Deallocated,
                ProcessId = region.ProcessId,
                Size = region.Size,
                Message = $"Deallocated {region.Size} bytes from process {region.ProcessId}"
            });

            _logger.LogDebug("Deallocated {Size} bytes from 0x{Address:X} for process {ProcessId}", 
                region.Size, region.BaseAddress, region.ProcessId);

            return true;
        }

        public MemoryUsage GetProcessMemoryUsage(int processId)
        {
            var regions = GetProcessMemoryRegions(processId);
            var totalAllocated = regions.Sum(r => r.Size);
            var usedBytes = (long)(totalAllocated * (0.7 + Random.Shared.NextDouble() * 0.3)); // Simulate 70-100% usage

            return new MemoryUsage
            {
                ProcessId = processId,
                AllocatedBytes = totalAllocated,
                UsedBytes = usedBytes,
                PeakUsage = (long)(totalAllocated * 1.2), // Simulate peak usage
                RegionCount = regions.Count
            };
        }

        public SystemMemoryInfo GetSystemMemoryInfo()
        {
            var totalAllocated = _allocatedRegions.Values.Sum(r => r.Size);
            var systemOverhead = TOTAL_SYSTEM_MEMORY / 20; // 5% for system overhead
            var usedMemory = totalAllocated + systemOverhead;

            return new SystemMemoryInfo
            {
                TotalMemory = TOTAL_SYSTEM_MEMORY,
                UsedMemory = usedMemory,
                AvailableMemory = TOTAL_SYSTEM_MEMORY - usedMemory,
                TotalProcesses = _processRegions.Count
            };
        }

        public IReadOnlyList<MemoryRegion> GetProcessMemoryRegions(int processId)
        {
            if (!_processRegions.TryGetValue(processId, out var regionIds))
            {
                return new List<MemoryRegion>().AsReadOnly();
            }

            var regions = regionIds
                .Select(id => _allocatedRegions.TryGetValue(id, out var region) ? region : null)
                .Where(region => region != null)
                .ToList();

            return regions.AsReadOnly()!;
        }

        public async Task<MemoryLeakReport> CheckForMemoryLeaksAsync()
        {
            var report = new MemoryLeakReport();
            var scanStart = DateTime.UtcNow;

            // Simulate leak detection logic
            var longLivedRegions = _allocatedRegions.Values
                .Where(r => DateTime.UtcNow - r.AllocationTime > TimeSpan.FromMinutes(30))
                .GroupBy(r => r.ProcessId)
                .Where(g => g.Sum(r => r.Size) > 100 * 1024 * 1024) // More than 100MB
                .ToList();

            foreach (var processGroup in longLivedRegions)
            {
                var processId = processGroup.Key;
                var leakedBytes = processGroup.Sum(r => r.Size);
                var oldestRegion = processGroup.OrderBy(r => r.AllocationTime).First();

                report.DetectedLeaks.Add(new MemoryLeak
                {
                    ProcessId = processId,
                    ProcessName = $"Process_{processId}",
                    LeakedBytes = leakedBytes,
                    LeakAge = DateTime.UtcNow - oldestRegion.AllocationTime,
                    Description = $"Long-lived memory allocation detected"
                });
            }

            report.HasLeaks = report.DetectedLeaks.Any();
            report.ScanDuration = DateTime.UtcNow - scanStart;

            if (report.HasLeaks)
            {
                _logger.LogWarning("Memory leak scan detected {LeakCount} potential leaks", report.DetectedLeaks.Count);
            }

            return report;
        }

        public async Task ForceGarbageCollectionAsync(int processId)
        {
            _logger.LogInformation("Forcing garbage collection for process {ProcessId}", processId);
            
            // In a real system, this would trigger GC for the specific process
            // For simulation, we'll just log it
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            OnMemoryEvent?.Invoke(this, new MemoryEvent
            {
                EventType = MemoryEventType.Deallocated,
                ProcessId = processId,
                Message = $"Forced garbage collection for process {processId}"
            });
        }

        public bool SetProcessMemoryLimit(int processId, long limitBytes)
        {
            _processMemoryLimits[processId] = limitBytes;
            _logger.LogInformation("Set memory limit for process {ProcessId} to {Limit} bytes", processId, limitBytes);
            return true;
        }

        /// <summary>
        /// Get memory statistics asynchronously
        /// </summary>
        /// <returns>Detailed memory statistics information</returns>
        public async Task<MemoryStatistics> GetMemoryStatisticsAsync()
        {
            // Create memory statistics object
            var memoryStats = new MemoryStatistics
            {
                TotalPhysicalMemory = TOTAL_SYSTEM_MEMORY,
                UsedPhysicalMemory = _allocatedRegions.Values.Sum(r => r.Size),
                AvailablePhysicalMemory = TOTAL_SYSTEM_MEMORY - _allocatedRegions.Values.Sum(r => r.Size),
                TotalVirtualMemory = TOTAL_SYSTEM_MEMORY * 2, // Virtual memory is typically larger than physical
                UsedVirtualMemory = _allocatedRegions.Values.Sum(r => r.Size),
                AvailableVirtualMemory = (TOTAL_SYSTEM_MEMORY * 2) - _allocatedRegions.Values.Sum(r => r.Size),
                AllocationCount = _allocatedRegions.Count,
                DeallocationCount = 0, // We'd need to track this separately
                Timestamp = DateTime.UtcNow
            };

            // Populate process memory usage
            foreach (var processId in _processRegions.Keys)
            {
                var processRegionIds = _processRegions[processId];
                long totalProcessMemory = 0;
                
                foreach (var regionId in processRegionIds)
                {
                    if (_allocatedRegions.TryGetValue(regionId, out var region))
                    {
                        totalProcessMemory += region.Size;
                    }
                }
                
                memoryStats.ProcessMemoryUsage[processId] = totalProcessMemory;
            }

            return memoryStats;
        }

        private static MemoryType DetermineMemoryType(long size)
        {
            return size switch
            {
                < 4096 => MemoryType.Stack,
                < 1024 * 1024 => MemoryType.Data,
                _ => MemoryType.Heap
            };
        }
    }
}
