#if !NET5_0_OR_GREATER
// Enables C# 9 `init` accessors and records when targeting netstandard2.1.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit;
}
#endif
