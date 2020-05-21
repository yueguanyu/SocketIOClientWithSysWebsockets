using System.Net.WebSockets;

namespace EngineIOClient {
    public class Option {
        public int HeartbeatTimeoutDelay { get; set; } = 2000;
        public int TimeoutTimesForClose { get; set; } = 3;
        public bool AutoConnect { get; set; } = true;
        public string Ca { get; set; }
        public bool RejectUnauthorized { get; set; } = false;

        public ClientWebSocket Socket { get; set; }
    }
}