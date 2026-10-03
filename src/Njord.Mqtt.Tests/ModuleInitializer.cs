using System.Runtime.CompilerServices;
using DiffEngine;

namespace Njord.Mqtt.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        DiffRunner.Disabled = true;
    }
}
