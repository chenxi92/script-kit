using System;
using System.IO;
using UnityEngine;

public class LogWritter : MonoBehaviour
{
    /// <summary>
    /// Log file expired time in days.
    /// </summary>
    private static readonly int expiredTime = 7;

    private string _logFilePath; 

    private string TimeStamp
    {
        get
        {
            return DateTime.Now.ToString("yyyy:MM:dd hh:mm:ss");
        }
    }

    public void Awake()
    {
        string fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
        string logFileFolder = Path.Combine(Application.persistentDataPath, "Logs");
        if (!Directory.Exists(logFileFolder))
        {
            Directory.CreateDirectory(logFileFolder);
        }
        _logFilePath = Path.Combine(logFileFolder, fileName);
        DeleteExpiredFiles(logFileFolder);

        WriteLog($"\n\n\n===== {TimeStamp} Awake() =====");

        Application.logMessageReceived += LogCallback;
        DontDestroyOnLoad(this);
    }
    
    private void LogCallback(string condition, string stackTrace, LogType type)
    {
        var log = $"[{type}] [{TimeStamp}] {condition}";
        if (type == LogType.Error || type == LogType.Exception)
        {
            log = $"[{type}] [{TimeStamp}] {condition}\n{stackTrace}";
        }
        WriteLog(log);
    }

    private void WriteLog(string log)
    {
        try
        {
            if (!File.Exists(_logFilePath))
            {
                File.WriteAllText(_logFilePath, log + Environment.NewLine);
            }
            else
            {
                File.AppendAllText(_logFilePath, log + Environment.NewLine);
            }
        }
        catch (System.Exception e)
        {
            // swallow exception
        }
    }

    private void DeleteExpiredFiles(string folder)
    {
        try
        {
            string[] logFiles = Directory.GetFiles(folder, "*.txt");
            foreach (string filePath in logFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (DateTime.TryParse(fileName, out DateTime fileDate))
                {
                    if (fileDate < DateTime.Now.AddDays(-expiredTime))
                    {
                        File.Delete(filePath);
                    }
                }
            }
        }
        catch
        {
            // swallow exception
        }
    }
}
