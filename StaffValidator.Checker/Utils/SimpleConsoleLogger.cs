using System;
using Microsoft.Extensions.Logging;

namespace StaffValidator.Checker.Utils
{
    public class SimpleConsoleLogger<T> : ILogger<T>
    {
        IDisposable ILogger.BeginScope<TState>(TState state) => new NoopDisposable();

        bool ILogger.IsEnabled(LogLevel logLevel) => true;

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try
            {
                var msg = formatter != null ? formatter(state, exception) : state?.ToString();
                Console.WriteLine($"[{logLevel}] {msg}");
                if (exception != null)
                {
                    Console.WriteLine(exception.ToString());
                }
            }
            catch { }
        }
    }

    internal class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
