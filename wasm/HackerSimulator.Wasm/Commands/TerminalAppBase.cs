using System.Threading.Tasks;

namespace HackerSimulator.Wasm.Commands
{
    /// <summary>
    /// Base class for interactive terminal applications.
    /// Commands derived from this class use CommandContext streams
    /// so they can participate in pipelines.
    /// </summary>
    public abstract class TerminalAppBase : ICommandModule
    {
        protected TerminalAppBase(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public abstract string Description { get; }
        public abstract string Usage { get; }

        protected CommandContext? Context { get; private set; }

        public async Task<int> Execute(string[] args, CommandContext context)
        {
            Context = context;
            await Setup(args, context);
            Start();
            return 0;
        }

        /// <summary>
        /// Called once before the application starts.
        /// </summary>
        protected abstract Task Setup(string[] args, CommandContext context);

        /// <summary>
        /// Start running the application. Derived classes should
        /// use Context.Stdout/StdErr for all output.
        /// </summary>
        protected virtual void Start() { }

        protected void Exit()
        {
            OnExit();
        }

        protected virtual void OnExit() { }
    }
}
