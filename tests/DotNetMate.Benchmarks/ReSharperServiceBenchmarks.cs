using BenchmarkDotNet.Attributes;
using DotNetMate.Core.JB;
using FEx.Agnostics.Abstractions.Flow;
using System.IO;

namespace DotNetMate.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class ReSharperServiceBenchmarks
{
    private FileInfo _validSettingsFile;
    private FileInfo _invalidFile;

    [GlobalSetup]
    public void Setup()
    {
        var tempPath = Path.GetTempPath();
        
        // Create valid .DotSettings file
        var validPath = Path.Combine(tempPath, $"test_{System.Guid.NewGuid()}.DotSettings");
        File.WriteAllText(validPath, GenerateSampleDotSettings());
        _validSettingsFile = new FileInfo(validPath);

        // Create invalid file
        var invalidPath = Path.Combine(tempPath, $"test_{System.Guid.NewGuid()}.txt");
        File.WriteAllText(invalidPath, "invalid");
        _invalidFile = new FileInfo(invalidPath);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_validSettingsFile?.Exists == true)
            File.Delete(_validSettingsFile.FullName);
        
        if (_invalidFile?.Exists == true)
            File.Delete(_invalidFile.FullName);
    }

    [Benchmark]
    public Result<Error> ValidateDotSettingsFile_Valid()
    {
        return ReSharperService.ValidateDotSettingsFile(_validSettingsFile);
    }

    [Benchmark]
    public Result<Error> ValidateDotSettingsFile_Invalid()
    {
        return ReSharperService.ValidateDotSettingsFile(_invalidFile);
    }

    [Benchmark]
    public Result<Error> ValidateDotSettingsFile_Null()
    {
        return ReSharperService.ValidateDotSettingsFile(null);
    }

    private static string GenerateSampleDotSettings()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<wpf:ResourceDictionary xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" 
                       xmlns:s=""clr-namespace:System;assembly=mscorlib"" 
                       xmlns:ss=""urn:shemas-jetbrains-com:settings-storage-xaml"" 
                       xmlns:wpf=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
    <s:String x:Key=""/Default/CodeStyle/Naming/CSharpNaming/PredefinedNamingRules/=PrivateInstanceFields/@EntryIndexedValue"">&lt;Policy Inspect=""True"" Prefix="""" Suffix="""" Style=""aaBb"" /&gt;</s:String>
    <s:String x:Key=""/Default/CodeStyle/CodeFormatting/CSharpFormat/WRAP_LIMIT/@EntryValue"">200</s:String>
    <s:Boolean x:Key=""/Default/Environment/SettingsMigration/IsMigratorApplied/=JetBrains_002EReSharper_002EPsi_002ECSharp_002ECodeStyle_002ESettingsUpgrade_002EMigrateBlankLinesAroundFieldToBlankLinesAroundProperty/@EntryIndexDefined"">True</s:Boolean>
</wpf:ResourceDictionary>";
    }
}

