namespace HackerOs.OS.Shell;

/// <summary>
/// Types of pipeline operators
/// </summary>
public enum PipelineOperator
{
    Pipe,           // |
    And,            // &&
    Or,             // ||
    Semicolon       // ;
}

/// <summary>
/// Base class for all pipeline AST nodes
/// </summary>
public abstract class PipelineNode
{
    public abstract NodeType NodeType { get; }
}

/// <summary>
/// Types of pipeline nodes
/// </summary>
public enum NodeType
{
    Command,
    Pipeline,
    Redirection
}

/// <summary>
/// Represents a command node in the pipeline AST
/// </summary>
public class CommandNode : PipelineNode
{
    public override NodeType NodeType => NodeType.Command;
    
    public string Command { get; init; } = string.Empty;
    public string[] Arguments { get; init; } = Array.Empty<string>();
    public List<RedirectionNode> Redirections { get; init; } = new();

    public CommandNode(string command, string[] arguments, List<RedirectionNode>? redirections = null)
    {
        Command = command;
        Arguments = arguments;
        Redirections = redirections ?? new List<RedirectionNode>();
    }

    public RedirectionNode? GetInputRedirection() =>
        Redirections.FirstOrDefault(r => r.Type == RedirectionType.Input);

    public RedirectionNode? GetOutputRedirection() =>
        Redirections.FirstOrDefault(r => r.Type == RedirectionType.Output || r.Type == RedirectionType.Append);

    public RedirectionNode? GetErrorRedirection() =>
        Redirections.FirstOrDefault(r => r.Type == RedirectionType.ErrorOutput || r.Type == RedirectionType.ErrorAppend);
}

/// <summary>
/// Represents a pipeline of commands connected by pipes
/// </summary>
public class PipelineTreeNode : PipelineNode
{
    public override NodeType NodeType => NodeType.Pipeline;
    
    public List<CommandNode> Commands { get; init; } = new();
    public List<PipelineOperator> Operators { get; init; } = new();

    public PipelineTreeNode(List<CommandNode>? commands = null, List<PipelineOperator>? operators = null)
    {
        Commands = commands ?? new List<CommandNode>();
        Operators = operators ?? new List<PipelineOperator>();
    }

    public bool IsEmpty => Commands.Count == 0;
    public bool HasMultipleCommands => Commands.Count > 1;
}

/// <summary>
/// Represents a redirection node in the pipeline AST
/// </summary>
public class RedirectionNode : PipelineNode
{
    public override NodeType NodeType => NodeType.Redirection;
    
    public RedirectionType Type { get; init; }
    public string Target { get; init; } = string.Empty;
    public int FileDescriptor { get; init; }

    public RedirectionNode(RedirectionType type, string target, int fileDescriptor = 1)
    {
        Type = type;
        Target = target;
        FileDescriptor = fileDescriptor;
    }
}

/// <summary>
/// Represents a command segment with optional operator for parsing
/// </summary>
internal class CommandSegment
{
    public string Text { get; }
    public PipelineOperator? Operator { get; }

    public CommandSegment(string text, PipelineOperator? operator_)
    {
        Text = text;
        Operator = operator_;
    }
}
