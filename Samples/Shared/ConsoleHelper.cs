using System;

namespace Clustron.Dkv.Samples.Shared
{
    public static class ConsoleHelper
    {
        public static void Header(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("====================================");
            Console.WriteLine(title);
            Console.WriteLine("====================================");
            Console.ResetColor();
        }

        public static void Section(string title)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine($"--- {title} ---");
            Console.ResetColor();
        }

        public static void Success(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{message}");
            Console.ResetColor();
        }

        public static void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{message}");
            Console.ResetColor();
        }

        public static void Info(string message)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
