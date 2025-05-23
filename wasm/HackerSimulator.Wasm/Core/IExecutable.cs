namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// Represents a runnable entity that can be launched by the shell.
    /// </summary>
    public interface IExecutable
    {
        /// <summary>
        /// Unique name of the executable.
        /// </summary>
        string Name { get; }
    }
}
