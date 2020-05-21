using System;
using System.Collections.Generic;

namespace Events {
    public class Events {

        public Dictionary<string, List<EventHandler>> Listeners { get; }
        public Events () { }

        public void On (string eventName, EventHandler handler) {
            if (Listeners.ContainsKey (eventName)) {
                if (Listeners[eventName].Count >= 5) {

                }
                Console.WriteLine (string.Format ("{0} is already on !", eventName));
                return;
                Listeners[eventName].Add (handler);
            } else {
                Listeners.Add (eventName, new List<EventHandler> () { handler });
            }
        }

        public void Off (string eventName) {
            if (Listeners.ContainsKey (eventName))
                Listeners.Remove (eventName);
        }
    }
}