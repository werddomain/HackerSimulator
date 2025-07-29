using System;
using System.Collections;
using System.Collections.Generic;

namespace HackerOs.OS.HSystem.Text.RegularExpressions
{
    /// <summary>
    /// Represents an immutable regular expression
    /// </summary>
    public class Regex
    {
        private readonly Regex _regex;

        /// <summary>
        /// Gets the pattern used by the Regex instance
        /// </summary>
        public string Pattern => _regex.ToString();

        /// <summary>
        /// Gets the options that were passed into the Regex constructor
        /// </summary>
        public RegexOptions Options { get; }

        /// <summary>
        /// Initializes a new instance of the Regex class
        /// </summary>
        public Regex(string pattern) : this(pattern, RegexOptions.None) { }

        /// <summary>
        /// Initializes a new instance of the Regex class with the specified pattern and options
        /// </summary>
        public Regex(string pattern, RegexOptions options)
        {
            var systemOptions = ConvertToSystemOptions(options);
            _regex = new System.Text.RegularExpressions.Regex(pattern, systemOptions);
            Options = options;
        }

        /// <summary>
        /// Searches the input string for the first occurrence of the regular expression
        /// </summary>
        public Match Match(string input)
        {
            var systemMatch = _regex.Match(input);
            return new Match(systemMatch);
        }

        /// <summary>
        /// Searches the input string for all occurrences of a regular expression
        /// </summary>
        public MatchCollection Matches(string input)
        {
            var systemMatches = _regex.Matches(input);
            return new MatchCollection(systemMatches);
        }

        /// <summary>
        /// Indicates whether the regular expression finds a match in the input string
        /// </summary>
        public bool IsMatch(string input)
        {
            return _regex.IsMatch(input);
        }

        /// <summary>
        /// Replaces all strings that match a regular expression pattern with a replacement string
        /// </summary>
        public string Replace(string input, string replacement)
        {
            return _regex.Replace(input, replacement);
        }

        /// <summary>
        /// Splits an input string into an array of substrings at the positions defined by a regular expression pattern
        /// </summary>
        public string[] Split(string input)
        {
            return _regex.Split(input);
        }

        /// <summary>
        /// Indicates whether the regular expression finds a match in the input string (static method)
        /// </summary>
        public static bool IsMatch(string input, string pattern)
        {
            return IsMatch(input, pattern);
        }

        /// <summary>
        /// Searches the input string for the first occurrence of the regular expression (static method)
        /// </summary>
        public static Match Match(string input, string pattern)
        {
            var systemMatch = Match(input, pattern);
            return new Match(systemMatch);
        }

        /// <summary>
        /// Searches the input string for all occurrences of a regular expression (static method)
        /// </summary>
        public static MatchCollection Matches(string input, string pattern)
        {
            var systemMatches = Matches(input, pattern);
            return new MatchCollection(systemMatches);
        }

        /// <summary>
        /// Replaces all strings that match a regular expression pattern with a replacement string (static method)
        /// </summary>
        public static string Replace(string input, string pattern, string replacement)
        {
            return Replace(input, pattern, replacement);
        }

        /// <summary>
        /// Splits an input string into an array of substrings at the positions defined by a regular expression pattern (static method)
        /// </summary>
        public static string[] Split(string input, string pattern)
        {
            return Split(input, pattern);
        }

        /// <summary>
        /// Escapes a minimal set of characters by replacing them with their escape codes
        /// </summary>
        public static string Escape(string str)
        {
            return Escape(str);
        }

        /// <summary>
        /// Converts any escaped characters in the input string
        /// </summary>
        public static string Unescape(string str)
        {
            return Unescape(str);
        }

        private static RegexOptions ConvertToSystemOptions(RegexOptions options)
        {
            var systemOptions = RegexOptions.None;
            
            if (options.HasFlag(RegexOptions.IgnoreCase))
                systemOptions |= RegexOptions.IgnoreCase;
            if (options.HasFlag(RegexOptions.Multiline))
                systemOptions |= RegexOptions.Multiline;
            if (options.HasFlag(RegexOptions.Singleline))
                systemOptions |= RegexOptions.Singleline;
            if (options.HasFlag(RegexOptions.IgnorePatternWhitespace))
                systemOptions |= RegexOptions.IgnorePatternWhitespace;
            if (options.HasFlag(RegexOptions.ExplicitCapture))
                systemOptions |= RegexOptions.ExplicitCapture;
            
            return systemOptions;
        }
    }

    /// <summary>
    /// Provides enumerated values to use to set regular expression options
    /// </summary>
    [Flags]
    public enum RegexOptions
    {
        None = 0,
        IgnoreCase = 1,
        Multiline = 2,
        ExplicitCapture = 4,
        Singleline = 16,
        IgnorePatternWhitespace = 32,
        RightToLeft = 64,
        ECMAScript = 256,
        CultureInvariant = 512
    }

    /// <summary>
    /// Represents the results from a single regular expression match
    /// </summary>
    public class Match
    {
        private readonly Match _match;

        internal Match(Match match)
        {
            _match = match;
        }

        /// <summary>
        /// Gets a value indicating whether the match is successful
        /// </summary>
        public bool Success => _match.Success;

        /// <summary>
        /// Gets the captured substring from the input string
        /// </summary>
        public string Value => _match.Value;

        /// <summary>
        /// Gets the position in the original string where the first character of the captured substring is found
        /// </summary>
        public int Index => _match.Index;

        /// <summary>
        /// Gets the length of the captured substring
        /// </summary>
        public int Length => _match.Length;

        /// <summary>
        /// Gets a collection of groups matched by the regular expression
        /// </summary>
        public GroupCollection Groups { get; }

        /// <summary>
        /// Returns the next match
        /// </summary>
        public Match NextMatch()
        {
            return new Match(_match.NextMatch());
        }

        /// <summary>
        /// Returns the expansion of the specified replacement pattern
        /// </summary>
        public string Result(string replacement)
        {
            return _match.Result(replacement);
        }

        /// <summary>
        /// Gets a value indicating that no match was found
        /// </summary>
        public static Match Empty { get; } = new Match(Empty);

        public override string ToString()
        {
            return _match.ToString();
        }
    }
    
    /// <summary>
    /// Represents a collection of Match objects
    /// </summary>
    public class MatchCollection : IEnumerable<Match>
    {
        private readonly MatchCollection _matches;

        internal MatchCollection(MatchCollection matches)
        {
            _matches = matches;
        }

        /// <summary>
        /// Gets the number of matches
        /// </summary>
        public int Count => _matches.Count;

        /// <summary>
        /// Gets an individual member of the collection
        /// </summary>
        public Match this[int i] => new Match(_matches[i]);
        
        /// <summary>
        /// Returns an enumerator that iterates through the collection
        /// </summary>
        public IEnumerator<Match> GetEnumerator()
        {
            return new MatchEnumerator(_matches);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// Represents a collection of capture groups
    /// </summary>
    public class GroupCollection : IEnumerable<Group>
    {
        private readonly GroupCollection _groups;

        internal GroupCollection(GroupCollection groups)
        {
            _groups = groups;
        }

        /// <summary>
        /// Gets the number of groups
        /// </summary>
        public int Count => _groups.Count;

        /// <summary>
        /// Gets an individual member of the collection by index
        /// </summary>
        public Group this[int groupnum] => new Group(_groups[groupnum]);

        /// <summary>
        /// Gets an individual member of the collection by name
        /// </summary>
        public Group this[string groupname] => new Group(_groups[groupname]);

        /// <summary>
        /// Returns an enumerator that iterates through the collection
        /// </summary>
        public IEnumerator<Group> GetEnumerator()
        {
            return new GroupEnumerator(_groups);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// Represents a single capture group
    /// </summary>
    public class Group
    {
        private readonly Group _group;

        internal Group(Group group)
        {
            _group = group;
        }

        /// <summary>
        /// Gets a value indicating whether the match is successful
        /// </summary>
        public bool Success => _group.Success;

        /// <summary>
        /// Gets the captured substring from the input string
        /// </summary>
        public string Value => _group.Value;

        /// <summary>
        /// Gets the position in the original string where the first character of the captured substring is found
        /// </summary>
        public int Index => _group.Index;

        /// <summary>
        /// Gets the length of the captured substring
        /// </summary>
        public int Length => _group.Length;

        public override string ToString()
        {
            return _group.ToString();
        }
    }

    /// <summary>
    /// Enumerator for MatchCollection
    /// </summary>
    internal class MatchEnumerator : IEnumerator<Match>
    {
        private readonly MatchCollection _matches;
        private int _currentIndex = -1;

        public MatchEnumerator(MatchCollection matches)
        {
            _matches = matches;
        }

        public Match Current
        {
            get
            {
                if (_currentIndex >= 0 && _currentIndex < _matches.Count)
                    return new Match(_matches[_currentIndex]);
                throw new InvalidOperationException();
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            _currentIndex++;
            return _currentIndex < _matches.Count;
        }

        public void Reset()
        {
            _currentIndex = -1;
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    /// <summary>
    /// Enumerator for GroupCollection
    /// </summary>
    internal class GroupEnumerator : IEnumerator<Group>
    {
        private readonly GroupCollection _groups;
        private int _currentIndex = -1;

        public GroupEnumerator(GroupCollection groups)
        {
            _groups = groups;
        }

        public Group Current
        {
            get
            {
                if (_currentIndex >= 0 && _currentIndex < _groups.Count)
                    return new Group(_groups[_currentIndex]);
                throw new InvalidOperationException();
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            _currentIndex++;
            return _currentIndex < _groups.Count;
        }

        public void Reset()
        {
            _currentIndex = -1;
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
