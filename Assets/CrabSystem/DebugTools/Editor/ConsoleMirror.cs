using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Copies every Console message to <project>/Logs/ConsoleMirror.log, so tools outside the editor
// can read the Console. Editor-only. Delete the folder to remove it.
//
// The file starts fresh once per editor session (not per domain reload), so entering play mode
// keeps what was logged before it. Stack traces are kept for errors, exceptions and asserts only.
[InitializeOnLoad]
public static class ConsoleMirror
{
    private const string SessionKey = "ConsoleMirror.Started";
    private static readonly object Gate = new object();
    private static readonly string LogPath =
        Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "ConsoleMirror.log");

    static ConsoleMirror()
    {
        if (!SessionState.GetBool(SessionKey, false))
        {
            SessionState.SetBool(SessionKey, true);
            Write($"=== Editor session {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n", false);
        }

        Application.logMessageReceivedThreaded -= OnLog;
        Application.logMessageReceivedThreaded += OnLog;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        Write($"--- {DateTime.Now:HH:mm:ss.fff} {change} ---\n", true);
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {type}: {message}\n";
        bool keepTrace = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        if (keepTrace && !string.IsNullOrEmpty(stackTrace)) line += stackTrace.TrimEnd() + "\n";
        Write(line, true);
    }

    private static void Write(string text, bool append)
    {
        lock (Gate)
        {
            try
            {
                if (append) File.AppendAllText(LogPath, text);
                else File.WriteAllText(LogPath, text);
            }
            catch (IOException) { }
        }
    }
}
