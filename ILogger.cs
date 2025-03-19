namespace QuicProxy
{
    internal interface ILogger
    {
        void Log(string message);

        void LogError(string message);

        void LogWarning(string message);

        void LogInfo(string message);

        void LogDebug(string message);
    }
}