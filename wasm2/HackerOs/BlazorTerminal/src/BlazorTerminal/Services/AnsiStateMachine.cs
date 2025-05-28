using BlazorTerminal.Models;
using System.Text;

namespace BlazorTerminal.Services
{
    /// <summary>
    /// The current state of the ANSI parser state machine
    /// </summary>
    public enum AnsiParserState
    {
        /// <summary>
        /// Normal text processing state
        /// </summary>
        Normal,
        
        /// <summary>
        /// Escape character received
        /// </summary>
        Escape,
        
        /// <summary>
        /// Control Sequence Introducer (ESC [) received
        /// </summary>
        CsiEntry,
        
        /// <summary>
        /// Processing CSI parameters
        /// </summary>
        CsiParam,
        
        /// <summary>
        /// Processing final character in CSI sequence
        /// </summary>
        CsiFinal
    }

    /// <summary>
    /// State machine for parsing ANSI escape sequences in terminal output
    /// </summary>
    public class AnsiStateMachine
    {
        private AnsiParserState _state = AnsiParserState.Normal;
        private readonly List<int> _params = new List<int>();
        private readonly StringBuilder _currentParam = new StringBuilder();
        private bool _collectParam = false;

        /// <summary>
        /// Event raised when an escape sequence has been processed
        /// </summary>
        public event EventHandler<AnsiSequence>? SequenceProcessed;

        /// <summary>
        /// Process a character through the state machine
        /// </summary>
        /// <param name="c">The character to process</param>
        /// <returns>True if the character was part of an escape sequence, false otherwise</returns>
        public bool Process(char c)
        {
            switch (_state)
            {
                case AnsiParserState.Normal:
                    if (c == TerminalConstants.ESC)
                    {
                        _state = AnsiParserState.Escape;
                        return true;
                    }
                    return false;

                case AnsiParserState.Escape:
                    if (c == '[') // CSI - Control Sequence Introducer
                    {
                        _state = AnsiParserState.CsiEntry;
                        _params.Clear();
                        _currentParam.Clear();
                        _collectParam = true;
                        return true;
                    }
                    else
                    {
                        // Handle other escape sequences here in the future
                        _state = AnsiParserState.Normal;
                        return false;
                    }

                case AnsiParserState.CsiEntry:
                    if (c >= '0' && c <= '9')
                    {
                        _state = AnsiParserState.CsiParam;
                        _currentParam.Append(c);
                        return true;
                    }
                    else if (c == ';')
                    {
                        _params.Add(0); // Empty parameter defaults to 0
                        _currentParam.Clear();
                        return true;
                    }
                    else
                    {
                        _state = AnsiParserState.CsiFinal;
                        goto case AnsiParserState.CsiFinal;
                    }

                case AnsiParserState.CsiParam:
                    if (c >= '0' && c <= '9')
                    {
                        _currentParam.Append(c);
                        return true;
                    }
                    else if (c == ';')
                    {
                        if (_currentParam.Length > 0)
                        {
                            _params.Add(int.Parse(_currentParam.ToString()));
                            _currentParam.Clear();
                        }
                        else
                        {
                            _params.Add(0); // Empty parameter defaults to 0
                        }
                        return true;
                    }
                    else
                    {
                        if (_currentParam.Length > 0)
                        {
                            _params.Add(int.Parse(_currentParam.ToString()));
                            _currentParam.Clear();
                        }
                        _state = AnsiParserState.CsiFinal;
                        goto case AnsiParserState.CsiFinal;
                    }

                case AnsiParserState.CsiFinal:
                    // Process the completed sequence
                    var sequence = new AnsiSequence(c, _params.ToArray());
                    SequenceProcessed?.Invoke(this, sequence);
                    
                    _state = AnsiParserState.Normal;
                    _collectParam = false;
                    return true;

                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Reset the state machine to its initial state
        /// </summary>
        public void Reset()
        {
            _state = AnsiParserState.Normal;
            _params.Clear();
            _currentParam.Clear();
            _collectParam = false;
        }
    }
    
    /// <summary>
    /// Represents a parsed ANSI escape sequence
    /// </summary>
    public class AnsiSequence
    {
        /// <summary>
        /// Gets the command character for this sequence
        /// </summary>
        public char Command { get; }
        
        /// <summary>
        /// Gets the parameters for this sequence
        /// </summary>
        public int[] Parameters { get; }
        
        /// <summary>
        /// Creates a new AnsiSequence with the specified command and parameters
        /// </summary>
        /// <param name="command">The command character</param>
        /// <param name="parameters">The parameters for the command</param>
        public AnsiSequence(char command, int[] parameters)
        {
            Command = command;
            Parameters = parameters;
        }
    }
}
