namespace HackerOs.OS.Shell;

/// <summary>
/// Represents the type of data flowing through a stream
/// </summary>
public enum StreamType
{
    /// <summary>
    /// Text data that can be decoded with character encoding
    /// </summary>
    Text,
    
    /// <summary>
    /// Binary data that should be passed through without encoding
    /// </summary>
    Binary,
    
    /// <summary>
    /// Mixed data that contains both text and binary components
    /// </summary>
    Mixed
}

/// <summary>
/// Represents the state of a stream in the pipeline
/// </summary>
public enum StreamState
{
    /// <summary>
    /// Stream is being created and initialized
    /// </summary>
    Initializing,
    
    /// <summary>
    /// Stream is ready to receive data
    /// </summary>
    Ready,
    
    /// <summary>
    /// Stream is actively transferring data
    /// </summary>
    Active,
    
    /// <summary>
    /// Stream has been closed by the writer
    /// </summary>
    Closed,
    
    /// <summary>
    /// Stream encountered an error and is unusable
    /// </summary>
    Error
}
