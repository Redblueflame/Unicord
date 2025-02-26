﻿namespace DSharpPlus.Net.Udp
{
    /// <summary>
    /// Represents a network connection endpoint.
    /// </summary>
    public struct ConnectionEndpoint
    {
        public ConnectionEndpoint(string host, int port)
        {
            Hostname = host;
            Port = port;
        }

        /// <summary>
        /// Gets or sets the hostname associated with this endpoint.
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Gets or sets the port associated with this endpoint.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets the hash code of this endpoint.
        /// </summary>
        /// <returns>Hash code of this endpoint.</returns>
        public override int GetHashCode() => 13 + 7 * Hostname.GetHashCode() + 7 * Port;

        /// <summary>
        /// Gets the string representation of this connection endpoint.
        /// </summary>
        /// <returns>String representation of this endpoint.</returns>
        public override string ToString() => $"{Hostname}:{Port}";
    }
}
