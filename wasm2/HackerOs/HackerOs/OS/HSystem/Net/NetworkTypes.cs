using System;
using System.Threading.Tasks;

namespace HackerOs.OS.HSystem.Net
{
    /// <summary>
    /// Represents an endpoint for network communication in the HackerOS system.
    /// </summary>
    public abstract class EndPoint
    {
        /// <summary>
        /// Gets the address family of the endpoint.
        /// </summary>
        public abstract AddressFamily AddressFamily { get; }

        /// <summary>
        /// Returns a string representation of the endpoint.
        /// </summary>
        public abstract override string ToString();
    }

    /// <summary>
    /// Represents an IP endpoint (IP address and port number).
    /// </summary>
    public class IPEndPoint : EndPoint
    {
        /// <summary>
        /// Gets or sets the IP address of the endpoint.
        /// </summary>
        public IPAddress Address { get; set; }

        /// <summary>
        /// Gets or sets the port number of the endpoint.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Initializes a new instance of the IPEndPoint class.
        /// </summary>
        public IPEndPoint(IPAddress address, int port)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Port = port;
        }

        /// <summary>
        /// Gets the address family of the IP endpoint.
        /// </summary>
        public override AddressFamily AddressFamily => Address.AddressFamily;

        /// <summary>
        /// Returns a string representation of the IP endpoint.
        /// </summary>
        public override string ToString() => $"{Address}:{Port}";
    }

    /// <summary>
    /// Represents an IP address.
    /// </summary>
    public class IPAddress
    {
        private readonly byte[] _addressBytes;
        private readonly AddressFamily _addressFamily;

        /// <summary>
        /// Initializes a new instance of the IPAddress class.
        /// </summary>
        public IPAddress(byte[] address)
        {
            _addressBytes = address ?? throw new ArgumentNullException(nameof(address));
            _addressFamily = address.Length == 4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6;
        }

        /// <summary>
        /// Gets the address family of the IP address.
        /// </summary>
        public AddressFamily AddressFamily => _addressFamily;

        /// <summary>
        /// Gets the loopback address (127.0.0.1).
        /// </summary>
        public static IPAddress Loopback => new(new byte[] { 127, 0, 0, 1 });

        /// <summary>
        /// Gets the "any" address (0.0.0.0).
        /// </summary>
        public static IPAddress Any => new(new byte[] { 0, 0, 0, 0 });

        /// <summary>
        /// Parses a string representation of an IP address.
        /// </summary>
        public static IPAddress Parse(string ipString)
        {
            if (string.IsNullOrEmpty(ipString))
                throw new ArgumentNullException(nameof(ipString));

            var parts = ipString.Split('.');
            if (parts.Length != 4)
                throw new FormatException("Invalid IP address format");

            var bytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (!byte.TryParse(parts[i], out bytes[i]))
                    throw new FormatException("Invalid IP address format");
            }

            return new IPAddress(bytes);
        }

        /// <summary>
        /// Returns a string representation of the IP address.
        /// </summary>
        public override string ToString()
        {
            if (_addressFamily == AddressFamily.InterNetwork)
                return $"{_addressBytes[0]}.{_addressBytes[1]}.{_addressBytes[2]}.{_addressBytes[3]}";
            
            // IPv6 simplified representation
            return string.Join(":", _addressBytes.Select(b => b.ToString("x2")));
        }
    }

    /// <summary>
    /// Specifies the addressing scheme used by a socket.
    /// </summary>
    public enum AddressFamily
    {
        /// <summary>
        /// Unknown address family.
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// Unspecified address family.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Unix local to host address family.
        /// </summary>
        Unix = 1,

        /// <summary>
        /// Address for IP version 4.
        /// </summary>
        InterNetwork = 2,

        /// <summary>
        /// Address for IP version 6.
        /// </summary>
        InterNetworkV6 = 23
    }

    /// <summary>
    /// Represents network-related exceptions in the HackerOS system.
    /// </summary>
    public class NetworkException : Exception
    {
        public NetworkException() : base() { }
        public NetworkException(string message) : base(message) { }
        public NetworkException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Represents socket-related exceptions.
    /// </summary>
    public class SocketException : NetworkException
    {
        /// <summary>
        /// Gets the error code associated with this exception.
        /// </summary>
        public int ErrorCode { get; }

        public SocketException(int errorCode) : base($"Socket error: {errorCode}")
        {
            ErrorCode = errorCode;
        }

        public SocketException(int errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
