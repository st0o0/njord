using System.Runtime.CompilerServices;
using DiffEngine;

namespace Njord.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // CLI-first workflow: never launch a diff tool, just fail with the diff.
        DiffRunner.Disabled = true;
    }
}
