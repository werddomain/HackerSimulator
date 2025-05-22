namespace HackerSimulator.Wasm.Commands
{
    public interface ICommandModule
    {
        string Name { get; }
        string Description { get; }
        string Usage { get; }
        Task<int> Execute(string[] args, CommandContext context);
    }
}
