using System;

namespace DotNetMateTool;

internal static class Banner
{
    private const string Logo = @"
   ____        _   _   _      _   __  __       _       
  |  _ \  ___ | |_| \ | | ___| |_|  \/  | __ _| |_ ___ 
  | | | |/ _ \| __|  \| |/ _ \ __| |\/| |/ _` | __/ _ \
  | |_| | (_) | |_| |\  |  __/ |_| |  | | (_| | ||  __/
  |____/ \___/ \__|_| \_|\___|\__|_|  |_|\__,_|\__\___|";

    public static void Display()
    {
        // Use cyan color for the logo
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(Logo);
        Console.ResetColor();

        // Add a tagline
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("  " + new string('─', 86));
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  .NET Developer Toolbox - Clean, Configure, Organize");
        Console.ForegroundColor = ConsoleColor.DarkGray;

        var version = typeof(Banner).Assembly.GetName().Version!;
        var versionString = $"{version.Major}.{version.Minor}.{version.Build}";
        Console.WriteLine($"  Version: {versionString}");

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("  " + new string('─', 86));
        Console.ResetColor();
        Console.WriteLine();
    }
}