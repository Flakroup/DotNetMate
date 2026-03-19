using System.Collections.Generic;

namespace DotNetMate.Core.Configuration;

public class GitLogConfig
{
    public string DefaultAfter { get; set; }
    public bool? Tempo { get; set; }
    public List<string> RepoPaths { get; set; }
}
