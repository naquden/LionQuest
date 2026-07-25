using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lightweight localhost TCP bridge for interactive agent iteration when the editor is open.
/// Listens on 127.0.0.1:7891. Commands (newline-terminated):
///   compile         — forces a script reimport; returns OK or FAIL + errors
///   menu <path>     — executes a menu item (e.g. "LionQuest/Generate/Start Screen")
///   play            — enters Play mode
///   stop            — exits Play mode
///   ping            — health check, returns "pong"
///
/// Start/stop via Tools > Agent Bridge in the editor menu.
/// </summary>
[InitializeOnLoad]
public static class AgentBridge
{
    const int PORT = 7891;
    static TcpListener _listener;
    static Thread _thread;
    static volatile bool _running;

    static AgentBridge()
    {
        // Not auto-started — must be started via menu to avoid unexpected ports
    }

    [MenuItem("Tools/Agent Bridge/Start")]
    public static void Start()
    {
        if (_running)
        {
            Debug.Log("[AgentBridge] Already running on port " + PORT);
            return;
        }
        _listener = new TcpListener(IPAddress.Loopback, PORT);
        _listener.Start();
        _running = true;
        _thread = new Thread(ListenLoop) { IsBackground = true, Name = "AgentBridge" };
        _thread.Start();
        Debug.Log($"[AgentBridge] Listening on 127.0.0.1:{PORT}");
    }

    [MenuItem("Tools/Agent Bridge/Stop")]
    public static void Stop()
    {
        _running = false;
        _listener?.Stop();
        _thread?.Join(500);
        Debug.Log("[AgentBridge] Stopped.");
    }

    static void ListenLoop()
    {
        while (_running)
        {
            try
            {
                TcpClient client = _listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
            catch
            {
                // listener stopped
            }
        }
    }

    static void HandleClient(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            try
            {
                string line = reader.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(line)) return;

                if (line == "ping")
                {
                    writer.WriteLine("pong");
                    return;
                }

                // All editor API calls must happen on the main thread
                string response = null;
                Exception error = null;
                EditorApplication.delayCall += () =>
                {
                    try { response = Dispatch(line); }
                    catch (Exception e) { error = e; }
                };

                // Poll for result (max 10s)
                int waited = 0;
                while (response == null && error == null && waited < 100)
                {
                    Thread.Sleep(100);
                    waited++;
                }

                if (error != null)
                    writer.WriteLine("FAIL|" + error.Message);
                else if (response != null)
                    writer.WriteLine(response);
                else
                    writer.WriteLine("FAIL|timeout waiting for editor main thread");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AgentBridge] Client error: " + e.Message);
            }
        }
    }

    static string Dispatch(string command)
    {
        if (command == "compile")
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return "OK|compile refresh triggered";
        }
        if (command == "play")
        {
            EditorApplication.isPlaying = true;
            return "OK|entering play mode";
        }
        if (command == "stop")
        {
            EditorApplication.isPlaying = false;
            return "OK|stopping play mode";
        }
        if (command.StartsWith("menu "))
        {
            string menuPath = command.Substring(5).Trim();
            bool found = EditorApplication.ExecuteMenuItem(menuPath);
            return found ? $"OK|menu '{menuPath}' executed" : $"FAIL|menu '{menuPath}' not found";
        }
        return $"FAIL|unknown command: {command}";
    }
}
