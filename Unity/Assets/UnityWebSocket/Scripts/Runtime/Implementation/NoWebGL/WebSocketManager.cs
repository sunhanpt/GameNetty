#if !NET_LEGACY && (UNITY_EDITOR || !UNITY_WEBGL)
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityWebSocket
{
    [DefaultExecutionOrder(-10000)]
    internal class WebSocketManager : MonoBehaviour
    {
        private const string rootName = "[UnityWebSocket]";
        private static WebSocketManager _instance;
        public static WebSocketManager Instance
        {
            get
            {
                if (!_instance) CreateInstance();
                return _instance;
            }
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public static void CreateInstance()
        {
            GameObject go = GameObject.Find("/" + rootName);
            if (!go) go = new GameObject(rootName);
            _instance = go.GetComponent<WebSocketManager>();
            if (!_instance) _instance = go.AddComponent<WebSocketManager>();
        }

        private readonly List<WebSocket> sockets = new List<WebSocket>();

        public void Add(WebSocket socket)
        {
            if (!sockets.Contains(socket))
                sockets.Add(socket);
        }

        public void Remove(WebSocket socket)
        {
            if (sockets.Contains(socket))
                sockets.Remove(socket);
        }

        private void Update()
        {
            if (sockets.Count <= 0) return;
            for (int i = sockets.Count - 1; i >= 0; i--)
            {
                sockets[i].Update();
            }
        }

        private void OnApplicationQuit()
        {
            Task[] closes = new Task[sockets.Count];
            for (int i = 0; i < sockets.Count; ++i)
            {
                closes[i] = sockets[i].CloseForShutdown();
            }

            // Close events are produced by the socket workers without a Unity frame.
            Task.WhenAll(closes).Wait(500);
            for (int i = 0; i < closes.Length; ++i)
            {
                if (!closes[i].IsCompleted) sockets[i].Abort();
            }
        }
    }
}
#endif
