using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

public static class Logger
{
    private static readonly object _lock = new object();
    private static readonly Encoding _utf8NoBom = new UTF8Encoding(false);
    private static string _logDirectory = Path.Combine(Application.persistentDataPath, "SkyLogs");
    private static string _debugLogFilePath = Path.Combine(Application.persistentDataPath, "SkyLogs", "ssjjLog.txt");

    public static string LogDirectory
    {
        get => _logDirectory;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _logDirectory = value;
            }
        }
    }

    public static string DebugLogFilePath
    {
        get => _debugLogFilePath;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _debugLogFilePath = value;
            }
        }
    }

    public static void Log(Exception ex, string additionalInfo = null)
    {
        try
        {
            lock (_lock)
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                string logFileName = $"Error_{DateTime.Now:yyyyMMdd}.log";
                string logFilePath = Path.Combine(LogDirectory, logFileName);
                var logContent = BuildLogContent(ex, additionalInfo);
                File.AppendAllText(logFilePath, logContent, Encoding.UTF8);
            }
        }
        catch (Exception logEx)
        {
            HandleLogFailure(logEx);
        }
    }

    public static void LogDebug(string category, string message)
    {
        if (!ShouldWritePreviewDebug(category, message))
        {
            return;
        }

        try
        {
            lock (_lock)
            {
                AppendDebugContent(DebugLogFilePath, category, message);
            }
        }
        catch (Exception ex)
        {
            HandleLogFailure(ex);
        }
    }

    public static void Log(string errorMessage, string errorType = "Error", string additionalInfo = null)
    {
        try
        {
            lock (_lock)
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
                string logFileName = $"Error_{DateTime.Now:yyyyMMdd}.log";
                string logFilePath = Path.Combine(LogDirectory, logFileName);
                var logContent = BuildLogContent(errorMessage, errorType, additionalInfo);
                File.AppendAllText(logFilePath, logContent, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            HandleLogFailure(ex);
        }
    }
    private static string BuildLogContent(Exception ex, string additionalInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"======= ErrorLog {DateTime.Now:yyyy-MM-dd HH:mm:ss} =======");
        sb.AppendLine($"ThreadId: {Thread.CurrentThread.ManagedThreadId}");
        sb.AppendLine($"ErrorType: {ex.GetType().FullName}");
        sb.AppendLine($"ErrorMessage: {ex.Message}");
        sb.AppendLine($"StackTrace: {ex.StackTrace}");

        Exception innerEx = ex.InnerException;
        int innerLevel = 1;
        while (innerEx != null)
        {
            sb.AppendLine($"InternalException[{innerLevel}]: {innerEx.GetType().FullName}");
            sb.AppendLine($"InternalMessage[{innerLevel}]: {innerEx.Message}");
            sb.AppendLine($"InternalStackTrace[{innerLevel}]: {innerEx.StackTrace}");
            innerEx = innerEx.InnerException;
            innerLevel++;
        }

        if (!string.IsNullOrWhiteSpace(additionalInfo))
        {
            sb.AppendLine($"AdditionalInformation: {additionalInfo}");
        }

        sb.AppendLine($"FriendlyName: {AppDomain.CurrentDomain.FriendlyName}");
        sb.AppendLine($"MachineName: {Environment.MachineName}");
        sb.AppendLine($"UserName: {Environment.UserName}");
        sb.AppendLine($"CurrentDirectory: {Environment.CurrentDirectory}");
        sb.AppendLine("==========================================");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string BuildDebugContent(string category, string message)
    {
        var sb = new StringBuilder();
        sb.Append('[')
          .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
          .Append("] ");

        if (!string.IsNullOrWhiteSpace(category))
        {
            sb.Append('[').Append(category).Append("] ");
        }

        sb.Append("ThreadId=").Append(Thread.CurrentThread.ManagedThreadId).Append(' ');
        sb.Append(message);
        sb.AppendLine();
        return sb.ToString();
    }

    private static bool ShouldWritePreviewDebug(string category, string message)
    {
        return string.Equals(category, "每枪数据", StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(message) &&
               message.StartsWith("开枪前观测：", StringComparison.Ordinal);
    }

    private static void AppendDebugContent(string filePath, string category, string message)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        EnsureDirectoryForFile(filePath);
        string logContent = BuildDebugContent(category, message);
        File.AppendAllText(filePath, logContent, _utf8NoBom);
    }
    private static string BuildLogContent(string errorMessage, string errorType, string additionalInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"======= ErrorLog {DateTime.Now:yyyy-MM-dd HH:mm:ss} =======");
        sb.AppendLine($"ThreadId: {Thread.CurrentThread.ManagedThreadId}");
        sb.AppendLine($"ErrorType: {errorType}");
        sb.AppendLine($"ErrorMessage: {errorMessage}");

        if (!string.IsNullOrWhiteSpace(additionalInfo))
        {
            sb.AppendLine($"AdditionalInformation: {additionalInfo}");
        }

        sb.AppendLine($"StackTrace: {Environment.StackTrace}");

        sb.AppendLine($"FriendlyName: {AppDomain.CurrentDomain.FriendlyName}");
        sb.AppendLine($"MachineName: {Environment.MachineName}");
        sb.AppendLine($"UserName: {Environment.UserName}");
        sb.AppendLine("==========================================");
        sb.AppendLine();

        return sb.ToString();
    }
    private static void HandleLogFailure(Exception ex)
    {
        try
        {
            using (var eventLog = new System.Diagnostics.EventLog("Application"))
            {
                eventLog.Source = "Application";
                eventLog.WriteEntry($"Log recording failed: {ex.Message}",
                    System.Diagnostics.EventLogEntryType.Error);
            }
        }
        catch
        {
            Console.WriteLine($"Unable to write to the log file: {ex.Message}");
            Console.WriteLine($"Original error: {ex}");
        }
    }

    private static void EnsureDirectoryForFile(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public static void CleanOldLogs(int daysToKeep = 1)
    {
        try
        {
            if (!Directory.Exists(LogDirectory)) return;

            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var logFiles = Directory.GetFiles(LogDirectory, "*.log");

            foreach (var logFile in logFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        fileInfo.Delete();
                    }
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            Log(ex, "An error occurred while cleaning up old logs");
        }
    }
}
