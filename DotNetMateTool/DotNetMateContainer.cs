using FEx.DependencyInjection;
using FEx.Fundamentals;
using FEx.Json;
using FEx.Logging;
using FEx.Logging.Abstractions.Interfaces;
using StrongInject;

namespace DotNetMateTool;

[RegisterModule(typeof(FExDefaultFundamentalsModule))]
[RegisterModule(typeof(FExDependencyInjectionModule))]
[RegisterModule(typeof(FExJsonModule))]
[RegisterModule(typeof(FExLoggingModule))]
public partial class DotNetMateContainer : IFExFundamentalsContainer, IFExJsonContainer, IFExLoggingContainer
{
}