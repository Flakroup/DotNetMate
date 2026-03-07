using FEx.Core;
using FEx.Core.Abstractions.Interfaces;
using FEx.DependencyInjection;
using FEx.DependencyInjection.Abstractions.Interfaces;
using FEx.Json;
using FEx.Logging;
using FEx.Logging.Abstractions.Interfaces;
using StrongInject;

namespace DotNetMateTool;

[RegisterModule(typeof(FExDependencyInjectionModule))]
[RegisterModule(typeof(FExJsonModule))]
[RegisterModule(typeof(FExLoggingModule))]
[RegisterModule(typeof(FExCoreModule))]
[Register(typeof(DotNetMateRunner), Scope.SingleInstance)]
public sealed partial class DotNetMateContainer : IFExDependencyInjectionContainer, IFExJsonContainer,
    IFExLoggingContainer, IFExCoreContainer, IContainer<DotNetMateRunner>
{
}