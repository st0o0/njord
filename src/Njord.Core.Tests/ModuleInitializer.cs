using System.Runtime.CompilerServices;
using DiffEngine;

namespace Njord.Core.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        DiffRunner.Disabled = true;
    }
}
