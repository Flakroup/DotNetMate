using System.Collections.Generic;

namespace DotNetMate.Core.Configuration;

public class CleanConfig
{
    public List<string> ExcludePatterns { get; set; }
    public List<string> CustomDirectories { get; set; }
}
