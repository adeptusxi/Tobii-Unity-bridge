using System.Threading;

namespace TalonBridge.Gaze.Udp
{
    /// <summary>
    /// Counters and status for a UDP gaze receiver. Counters are written by the
    /// receive thread and read on the main thread; the derived rates are
    /// computed on the main thread.
    /// </summary>
    public sealed class GazeDiagnostics
    {
        private int packetsReceived;
        private int parseErrors;
        private int invalidPackets;
        private int unmappablePackets;

        /// <summary>Endpoint the receiver is bound to.</summary>
        public string Endpoint { get; internal set; } = string.Empty;

        /// <summary>Whether the socket is currently open.</summary>
        public bool IsListening { get; internal set; }

        /// <summary>Last socket or parse error, or an empty string.</summary>
        public string LastError { get; internal set; } = string.Empty;

        public int PacketsReceived => Volatile.Read(ref packetsReceived);

        /// <summary>Packets that could not be decoded.</summary>
        public int ParseErrors => Volatile.Read(ref parseErrors);

        /// <summary>Packets reporting no valid gaze, including heartbeats.</summary>
        public int InvalidPackets => Volatile.Read(ref invalidPackets);

        /// <summary>Packets whose coordinates the mapper could not use.</summary>
        public int UnmappablePackets => Volatile.Read(ref unmappablePackets);

        /// <summary>Packets per second, averaged over the last second.</summary>
        public float PacketsPerSecond { get; internal set; }

        /// <summary>Seconds since the last packet arrived.</summary>
        public float TimeSinceLastPacket { get; internal set; } = float.PositiveInfinity;

        /// <summary>
        /// Age of the last packet when it reached Unity, from the send time the
        /// bridge stamped on it. Both clocks are local, so this is end-to-end
        /// transport latency.
        /// </summary>
        public double LastPacketLatency { get; internal set; }

        internal void CountPacket() => Interlocked.Increment(ref packetsReceived);

        internal void CountParseError() => Interlocked.Increment(ref parseErrors);

        internal void CountInvalid() => Interlocked.Increment(ref invalidPackets);

        internal void CountUnmappable() => Interlocked.Increment(ref unmappablePackets);

        internal void Reset()
        {
            Interlocked.Exchange(ref packetsReceived, 0);
            Interlocked.Exchange(ref parseErrors, 0);
            Interlocked.Exchange(ref invalidPackets, 0);
            Interlocked.Exchange(ref unmappablePackets, 0);
            PacketsPerSecond = 0f;
            TimeSinceLastPacket = float.PositiveInfinity;
            LastPacketLatency = 0d;
            LastError = string.Empty;
        }
    }
}
