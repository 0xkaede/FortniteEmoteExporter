using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimationExport.Utils
{
    public static class Logger
    {
        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            Console.Write($"[{DateTime.Now.ToString("H:m:s")}] ");
            Console.Write("[");

            Console.ForegroundColor = level switch
            {
                LogLevel.Info => ConsoleColor.Cyan,
                LogLevel.Cue4 => ConsoleColor.DarkYellow,
                LogLevel.Error => ConsoleColor.Red,

            };

            Console.Write(level.GetDescription());
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine($"] {message}");
        }

        public static string GetDescription(this Enum value)
        {
            var type = value.GetType();
            var name = Enum.GetName(type, value);
            if (name == null)
                return null;

            var field = type.GetField(name);
            if (field == null)
                return null;

            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attr)
                return attr.Description;

            return null;
        }
    }

    public enum LogLevel
    {
        [Description("INF0")] Info,

        [Description("CUE4")] Cue4,

        [Description("ERROR")] Error
    }
}
