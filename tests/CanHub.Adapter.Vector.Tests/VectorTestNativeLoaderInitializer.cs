using System.Runtime.CompilerServices;
using CanHub.Adapter.Vector.Internal;

namespace CanHub.Adapter.Vector.Tests;

internal static class VectorTestNativeLoaderInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        VectorNativeLoader.EnsureRegistered();
    }
}
