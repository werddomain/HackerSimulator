// System Call Interface - Controlled kernel access
using System;
using System.Threading.Tasks;

namespace HackerOs.OS.Kernel.Core
{
    /// <summary>
    /// System call interface for controlled kernel access
    /// </summary>
    public interface ISystemCall
    {
        /// <summary>
        /// Execute a system call with parameters
        /// </summary>
        Task<SystemCallResult> ExecuteAsync(string callName, params object[] parameters);

        /// <summary>
        /// Register a new system call handler
        /// </summary>
        Task RegisterSystemCallAsync(string callName, Func<object[], Task<object?>> handler);

        /// <summary>
        /// Check if a system call is registered
        /// </summary>
        bool IsSystemCallRegistered(string callName);

        /// <summary>
        /// Get available system call names
        /// </summary>
        string[] GetAvailableSystemCalls();
    }

    /// <summary>
    /// System call service implementation
    /// </summary>
    public class SystemCallService : ISystemCall
    {
        private readonly IKernelCore _kernel;

        public SystemCallService(IKernelCore kernel)
        {
            _kernel = kernel;
        }

        public async Task<SystemCallResult> ExecuteAsync(string callName, params object[] parameters)
        {
            var systemCall = new SystemCall
            {
                Name = callName,
                Parameters = parameters,
                CallerContext = GetCallerContext()
            };

            return await _kernel.ExecuteSystemCallAsync(systemCall);
        }

        public async Task RegisterSystemCallAsync(string callName, Func<object[], Task<object?>> handler)
        {
            // This would typically register with the kernel's system call table
            // For now, we'll just validate the parameters
            if (string.IsNullOrEmpty(callName))
                throw new ArgumentException("System call name cannot be empty", nameof(callName));
            
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            // In a real implementation, this would register with the kernel
            await Task.CompletedTask;
        }

        public bool IsSystemCallRegistered(string callName)
        {
            // This would check the kernel's system call table
            return !string.IsNullOrEmpty(callName);
        }

        public string[] GetAvailableSystemCalls()
        {
            // Return core system calls
            return new string[]
            {
                "create_process",
                "terminate_process", 
                "get_process",
                "allocate_memory",
                "deallocate_memory",
                "get_memory_info"
            };
        }

        private string GetCallerContext()
        {
            // In a real OS, this would determine the calling process/user
            // For simulation, we'll use a simplified approach
            var stackTrace = Environment.StackTrace;
            return stackTrace?.Split('\n')[2]?.Trim() ?? "Unknown";
        }
    }
}
