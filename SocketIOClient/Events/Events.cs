using System;
using System.Collections.Generic;

namespace Events {
    public class Events {

        public Dictionary<string, List<EventHandler>> Listeners { get; } = new Dictionary<string, List<EventHandler>> ();
        public Events () { }

        public void On (string eventName, EventHandler handler) {
            if (Listeners.ContainsKey (eventName)) {
                if (Listeners[eventName].Count >= 10) {
                    throw new Exception ($"listener has set over 10");
                }
                if (Listeners[eventName].Contains (handler)) {
                    return;
                }
                Console.WriteLine ($"listener add one more");
                Listeners[eventName].Add (handler);
            } else {
                Listeners.Add (eventName, new List<EventHandler> () { handler });
            }
            Console.WriteLine ($"On current listener:{Listeners[eventName].Count}");
        }

        public void Off (string eventName, EventHandler handler) {
            if (Listeners.ContainsKey (eventName)) {
                Listeners[eventName].Remove (handler);
                if (Listeners[eventName].Count == 0) {
                    Listeners.Remove (eventName);
                }
            }
            Console.WriteLine ($"Off current listener:{Listeners[eventName].Count}");
        }

        public void Emit (string eventName, byte[] buffer) {
            if (Listeners[eventName] == null) {
                return;
            }
            foreach (var handler in Listeners[eventName]) {
                handler (buffer);
            }
        }
    }
}