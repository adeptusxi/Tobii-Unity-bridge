using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace TalonBridge.Gaze.Udp
{
    /// <summary>
    /// Receives gaze from the Talon bridge over localhost UDP.
    ///
    /// A background thread owns the socket and decodes each datagram, keeping
    /// only the newest sample; the main thread picks that up in
    /// <see cref="Poll"/> and publishes it. No Unity API is touched off the main
    /// thread, and the steady state allocates nothing per packet.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/UDP Gaze Provider")]
    public sealed class UdpGazeProvider : GazeProviderBase
    {
        private const int ReceiveBufferSize = 2048;

        [Header("Connection")]
        [Tooltip("Port the Talon bridge sends to. The socket binds to the " +
                 "loopback interface only.")]
        [SerializeField]
        private int port = 5005;

        [Tooltip("Seconds between attempts to reopen the socket after a failure.")]
        [SerializeField]
        [Min(0.1f)]
        private float retryInterval = 2f;

        [Header("Mapping")]
        [SerializeField]
        private GazeCoordinateMapper mapper = new GazeCoordinateMapper();

        [Header("Debug")]
        [Tooltip("Logs connection state and decode failures. Never logs per packet.")]
        [SerializeField]
        private bool debugLogging = false;

        private readonly object gate = new object();
        private readonly GazeDiagnostics diagnostics = new GazeDiagnostics();
        private readonly GazePacketParser parser = new GazePacketParser();
        private readonly byte[] receiveBuffer = new byte[ReceiveBufferSize];

        private Thread thread;
        private volatile Socket socket;
        private volatile bool running;

        private GazePacket latestPacket;
        private bool hasLatestPacket;
        private double latestArrivalUnixTime;

        private int rateWindowStartPackets;
        private float rateWindowStartTime;
        private float lastPacketRealtime = float.NegativeInfinity;

        public GazeDiagnostics Diagnostics => diagnostics;

        public GazeCoordinateMapper Mapper => mapper;

        public override string ProviderName => $"Talon UDP :{port}";

        public int Port
        {
            get => port;
            set => port = value;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            StartListening();
        }

        protected override void OnDisable()
        {
            StopListening();
            base.OnDisable();
        }

        private void OnApplicationQuit()
        {
            StopListening();
        }

        // -- socket lifecycle --------------------------------------------------

        private void StartListening()
        {
            if (running)
            {
                return;
            }

            diagnostics.Reset();
            diagnostics.Endpoint = $"{IPAddress.Loopback}:{port}";
            rateWindowStartPackets = 0;
            rateWindowStartTime = Time.realtimeSinceStartup;
            lastPacketRealtime = float.NegativeInfinity;

            running = true;
            thread = new Thread(ReceiveLoop)
            {
                Name = "TalonGazeReceiver",
                IsBackground = true,
            };
            thread.Start();
        }

        private void StopListening()
        {
            if (!running)
            {
                return;
            }

            running = false;

            // Closing the socket releases a blocked Receive immediately.
            Socket toClose = socket;
            socket = null;
            CloseSocket(toClose);

            Thread toJoin = thread;
            thread = null;
            if (toJoin != null && !toJoin.Join(500))
            {
                Log("receive thread did not stop in time");
            }

            diagnostics.IsListening = false;

            lock (gate)
            {
                hasLatestPacket = false;
            }
        }

        private static void CloseSocket(Socket target)
        {
            if (target == null)
            {
                return;
            }

            try
            {
                target.Close();
            }
            catch (Exception)
            {
                // The socket may already be faulted; nothing useful to do.
            }
        }

        // -- receive thread ----------------------------------------------------

        private void ReceiveLoop()
        {
            while (running)
            {
                Socket current = null;
                try
                {
                    current = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    current.ReceiveTimeout = 200;
                    current.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    socket = current;
                    diagnostics.IsListening = true;
                    diagnostics.LastError = string.Empty;
                    Log($"listening on {IPAddress.Loopback}:{port}");

                    Receive(current);
                }
                catch (Exception e)
                {
                    diagnostics.LastError = $"{e.GetType().Name}: {e.Message}";
                    Log($"socket error: {diagnostics.LastError}");
                }
                finally
                {
                    diagnostics.IsListening = false;
                    if (ReferenceEquals(socket, current))
                    {
                        socket = null;
                    }

                    CloseSocket(current);
                }

                // Wait before rebinding, so a port conflict or a restarting
                // Talon does not spin the thread.
                float waited = 0f;
                while (running && waited < retryInterval)
                {
                    Thread.Sleep(50);
                    waited += 0.05f;
                }
            }
        }

        private void Receive(Socket from)
        {
            while (running)
            {
                int received;
                try
                {
                    received = from.Receive(receiveBuffer);
                }
                catch (SocketException e)
                    when (e.SocketErrorCode == SocketError.TimedOut ||
                          e.SocketErrorCode == SocketError.WouldBlock)
                {
                    continue;
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // A previous send bounced; the sender is gone, not an error.
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (received <= 0)
                {
                    continue;
                }

                diagnostics.CountPacket();

                if (!parser.TryParse(receiveBuffer, received, out GazePacket packet))
                {
                    diagnostics.CountParseError();
                    diagnostics.LastError = $"decode failed: {parser.LastError}";
                    continue;
                }

                if (!packet.Ok)
                {
                    diagnostics.CountInvalid();
                }

                lock (gate)
                {
                    latestPacket = packet;
                    latestArrivalUnixTime = UnixNow();
                    hasLatestPacket = true;
                }
            }
        }

        // -- main thread -------------------------------------------------------

        protected override void Poll()
        {
            GazePacket packet;
            double arrival;
            bool received;
            lock (gate)
            {
                received = hasLatestPacket;
                packet = latestPacket;
                arrival = latestArrivalUnixTime;
                hasLatestPacket = false;
            }

            if (!received)
            {
                UpdateRate();
                return;
            }

            float now = Time.realtimeSinceStartup;
            lastPacketRealtime = now;
            diagnostics.LastPacketLatency = packet.WallTime > 0d ? arrival - packet.WallTime : 0d;
            UpdateRate();

            if (!packet.Ok)
            {
                PublishSample(GazeSample.Invalid(packet.SourceTimestamp, now));
                return;
            }

            if (!mapper.TryMap(packet, out Vector2 normalized, out bool insideSurface))
            {
                diagnostics.CountUnmappable();
                PublishSample(GazeSample.Invalid(packet.SourceTimestamp, now));
                return;
            }

            bool valid = insideSurface || !mapper.RequireInsideSurface;
            PublishSample(new GazeSample(
                normalized,
                valid,
                packet.Eyes,
                insideSurface,
                packet.SourceTimestamp,
                now));
        }

        private void UpdateRate()
        {
            float now = Time.realtimeSinceStartup;
            float elapsed = now - rateWindowStartTime;
            if (elapsed >= 1f)
            {
                int packets = diagnostics.PacketsReceived;
                diagnostics.PacketsPerSecond = (packets - rateWindowStartPackets) / elapsed;
                rateWindowStartPackets = packets;
                rateWindowStartTime = now;
            }

            diagnostics.TimeSinceLastPacket = float.IsNegativeInfinity(lastPacketRealtime)
                ? float.PositiveInfinity
                : now - lastPacketRealtime;
        }

        private static double UnixNow()
        {
            return (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log($"[TalonGaze] {message}");
            }
        }
    }
}
