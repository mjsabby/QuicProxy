namespace QuicProxy
{
    using System;

    internal sealed class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
        public void LogError(string message)
        {
            Console.WriteLine($"[ERROR] {message}");
        }
        public void LogWarning(string message)
        {
            Console.WriteLine($"[WARNING] {message}");
        }
        public void LogInfo(string message)
        {
            Console.WriteLine($"[INFO] {message}");
        }
        public void LogDebug(string message)
        {
            Console.WriteLine($"[DEBUG] {message}");
        }
    }
}