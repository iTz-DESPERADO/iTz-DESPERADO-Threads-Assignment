using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace OrderProcessing.Shared.Logging
{
    public static class Log
    {
        private static readonly Lock Sync = new();
      
        public static void Info(string message) => Write(message, ConsoleColor.Gray);
        public static void Success(string message) => Write(message, ConsoleColor.Green);
        public static void Error(string message) => Write(message, ConsoleColor.Red);

        public static void Write(string message , ConsoleColor consoleColor)
        {
            lock(Sync)
            {
                var previous = Console.ForegroundColor;

                try
                {
                    Console.ForegroundColor = consoleColor;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] {message}");
                }
                finally
                {
                    Console.ForegroundColor = previous;
                }
            }
        }
    }
}
