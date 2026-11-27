using UnityEngine;

namespace UnityWebSocket.Demo
{
    public class UnityWebSocketDemo : MonoBehaviour
    {
        public string address = "wss://echo.websocket.events";
        public string sendText = "Hello UnityWebSocket!";
        [SerializeField] private bool showOnStart = true;

        private IWebSocket socket;
        private bool visible;

        private bool logMessage = true;
        private string log = "";
        private int sendCount;
        private int receiveCount;
        private Vector2 scrollPos;
        private Color green = new Color(0.1f, 1, 0.1f);
        private Color red = new Color(1f, 0.1f, 0.1f);
        private Color wait = new Color(0.7f, 0.3f, 0.3f);

        private void Awake()
        {
            visible = showOnStart;
        }

        private void OnGUI()
        {
            if (!visible) return;
            // Keep the debug controls readable in portrait without overflowing short Game Views.
            float scale = Mathf.Min(Screen.width / 520f, Screen.height / 360f);
            if (scale <= 0f) return;

            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            try
            {
                DrawPanel(Screen.width / scale, Screen.height / scale);
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        private void DrawPanel(float panelWidth, float panelHeight)
        {
            var width = GUILayout.Width(panelWidth - 10);

            WebSocketState state = socket == null ? WebSocketState.Closed : socket.ReadyState;

            // draw header
            GUILayout.BeginHorizontal();
            GUILayout.Label("SDK Version: " + Settings.VERSION, GUILayout.Width(panelWidth - 100));
            GUI.color = green;
            GUILayout.Label($"FPS: {fps:F2}", GUILayout.Width(80));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            // draw websocket state
            GUILayout.BeginHorizontal();
            GUILayout.Label("State: ", GUILayout.Width(36));
            GUI.color = WebSocketState.Closed == state ? red : WebSocketState.Open == state ? green : wait;
            GUILayout.Label($"{state}", GUILayout.Width(120));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            // draw address
            GUI.enabled = state == WebSocketState.Closed;
            GUILayout.Label("Address: ", width);
            address = GUILayout.TextField(address, width);

            // draw connect button
            GUILayout.BeginHorizontal();
            GUI.enabled = state == WebSocketState.Closed;
            if (GUILayout.Button(state == WebSocketState.Connecting ? "Connecting..." : "Connect"))
            {
                socket = new WebSocket(address);
                socket.OnOpen += Socket_OnOpen;
                socket.OnMessage += Socket_OnMessage;
                socket.OnClose += Socket_OnClose;
                socket.OnError += Socket_OnError;
                AddLog(string.Format("Connecting..."));
                socket.ConnectAsync();
            }

            // draw close button
            GUI.enabled = state == WebSocketState.Open;
            if (GUILayout.Button(state == WebSocketState.Closing ? "Closing..." : "Close"))
            {
                AddLog(string.Format("Closing..."));
                socket.CloseAsync();
            }
            GUILayout.EndHorizontal();

            // draw input message
            GUILayout.Label("Message: ");
            sendText = GUILayout.TextArea(sendText, GUILayout.MinHeight(50), width);

            // draw send message button
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Send") && !string.IsNullOrEmpty(sendText))
            {
                socket.SendAsync(sendText);
                AddLog(string.Format("Send: {0}", sendText));
                sendCount += 1;
            }
            if (GUILayout.Button("Send Bytes") && !string.IsNullOrEmpty(sendText))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(sendText);
                socket.SendAsync(bytes,0,bytes.Length);
                AddLog(string.Format("Send Bytes ({1}): {0}", sendText, bytes.Length));
                sendCount += 1;
            }
            if (GUILayout.Button("Send x100") && !string.IsNullOrEmpty(sendText))
            {
                for (int i = 0; i < 100; i++)
                {
                    var text = (i + 1).ToString() + ". " + sendText;
                    socket.SendAsync(text);
                    AddLog(string.Format("Send: {0}", text));
                    sendCount += 1;
                }
            }
            if (GUILayout.Button("Send Bytes x100") && !string.IsNullOrEmpty(sendText))
            {
                for (int i = 0; i < 100; i++)
                {
                    var text = (i + 1).ToString() + ". " + sendText;
                    var bytes = System.Text.Encoding.UTF8.GetBytes(text);
                    socket.SendAsync(bytes,0,bytes.Length);
                    AddLog(string.Format("Send Bytes ({1}): {0}", text, bytes.Length));
                    sendCount += 1;
                }
            }
            GUILayout.EndHorizontal();

            // draw message count
            GUI.enabled = true;
            GUILayout.BeginHorizontal();
            logMessage = GUILayout.Toggle(logMessage, "Log Message");
            GUILayout.Label(string.Format("Send Count: {0}", sendCount));
            GUILayout.Label(string.Format("Receive Count: {0}", receiveCount));
            GUILayout.EndHorizontal();

            // draw clear button
            if (GUILayout.Button("Clear"))
            {
                log = "";
                receiveCount = 0;
                sendCount = 0;
            }

            // draw message content
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(panelHeight - 270), width);
            GUILayout.Label(log);
            GUILayout.EndScrollView();
        }

        private void AddLog(string str)
        {
            if (!logMessage) return;
            if (str.Length > 100) str = str.Substring(0, 100) + "...";
            log += str + "\n";
            if (log.Length > 22 * 1024)
            {
                log = log.Substring(log.Length - 22 * 1024);
            }
            scrollPos.y = int.MaxValue;
        }

        private void Socket_OnOpen(object sender, OpenEventArgs e)
        {
            AddLog(string.Format("Connected: {0}", address));
        }

        private void Socket_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.IsBinary)
            {
                AddLog(string.Format("Receive Bytes ({1}): {0}", e.Data, e.RawData.Length));
            }
            else if (e.IsText)
            {
                AddLog(string.Format("Receive: {0}", e.Data));
            }
            receiveCount += 1;
        }

        private void Socket_OnClose(object sender, CloseEventArgs e)
        {
            AddLog(string.Format("Closed: StatusCode: {0}, Reason: {1}", e.StatusCode, e.Reason));
        }

        private void Socket_OnError(object sender, ErrorEventArgs e)
        {
            AddLog(string.Format("Error: {0}", e.Message));
        }

        private void OnApplicationQuit()
        {
            if (socket != null && socket.ReadyState != WebSocketState.Closed)
            {
                socket.CloseAsync();
            }
        }

        private int frame = 0;
        private float time = 0;
        private float fps = 0;
        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.F8)) visible = !visible;
#endif
            frame += 1;
            time += Time.deltaTime;
            if (time >= 0.5f)
            {
                fps = frame / time;
                frame = 0;
                time = 0;
            }
        }
    }
}
