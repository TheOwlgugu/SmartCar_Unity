using UnityEngine;
using Fleck;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

public class NetworkServerManager : MonoBehaviour
{
    [Header("服务器端口")]
    [SerializeField] private int port = 8082; // 和你的Nano端保持一致

    private WebSocketServer server;
    private List<IWebSocketConnection> allSockets = new List<IWebSocketConnection>();

    // 因为 Fleck 的回调在非主线程执行，不能直接操作 Unity 的 UI 或对象
    // 我们用这个线程安全的队列，把收到的消息暂存下来，交由主线程处理
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    //核心魔法：游戏启动后自动创建自己并挂载，且切场景不销毁
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("[NetworkServerManager]");
        go.AddComponent<NetworkServerManager>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        // 初始化 Fleck 服务器
        server = new WebSocketServer($"ws://0.0.0.0:{port}");

        // 开启 RestartAfterListenError，防止端口被占用或异常断开后无法重启
        server.RestartAfterListenError = true;

        server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                Debug.Log($"[网络服务器] 客户端已连接！IP: {socket.ConnectionInfo.ClientIpAddress}");
                lock (allSockets)
                {
                    allSockets.Add(socket);
                }
            };

            socket.OnClose = () =>
            {
                Debug.Log("[网络服务器] 客户端已断开！");
                lock (allSockets)
                {
                    allSockets.Remove(socket);
                }
            };

            socket.OnMessage = message =>
            {
                // Fleck 运行在后台线程，收到消息先塞进队列
                messageQueue.Enqueue(message);
            };
        });

        Debug.Log($"[网络服务器] 启动成功，正在监听端口: {port}");
    }

    void Update()
    {
        // 在主线程（Update）中处理收到的消息，这样你就可以安全地调用 Unity API 了
        while (messageQueue.TryDequeue(out string message))
        {
            Debug.Log($"[网络服务器] 收到 Nano 消息: {message}");

            // 此处可以接入你的业务逻辑，例如解析坐标、触发地图更新等
            // 测试回复：
            if (message.Contains("ping"))
            {
                SendToAll("{\"command\":\"pong\", \"msg\":\"Unity已收到\"}");
            }
        }
    }

    /// <summary>
    /// 给所有已连接的 Nano 广播消息（比如下发地图坐标）
    /// </summary>
    public static void SendToAll(string jsonMessage)
    {
        // 为了安全获取所有 sockets，需要加锁
        var socketsCopy = new List<IWebSocketConnection>();

        // 这里为了简单，直接找场景里的实例。如果有多个实例可以改为单例模式。
        var instance = FindObjectOfType<NetworkServerManager>();
        if (instance != null && instance.allSockets != null)
        {
            lock (instance.allSockets)
            {
                socketsCopy.AddRange(instance.allSockets);
            }
        }

        foreach (var socket in socketsCopy)
        {
            if (socket.IsAvailable)
            {
                socket.Send(jsonMessage);
            }
        }
        Debug.Log($"[网络服务器] 已广播消息: {jsonMessage}");
    }

    void OnDestroy()
    {
        server?.Dispose();
        Debug.Log("[网络服务器] 已关闭。");
    }
}