#if NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP2_0 || NETCOREAPP2_1 || NETCOREAPP2_2 || NETCOREAPP3_0 || NETCOREAPP3_1 || NET45 || NET451 || NET452 || NET6 || NET461 || NET462 || NET47 || NET471 || NET472 || NET48 || NET481

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public class RequiredMemberAttribute : Attribute { }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public class CompilerFeatureRequiredAttribute : Attribute
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public CompilerFeatureRequiredAttribute(string name) { }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class SetsRequiredMembersAttribute : Attribute { }
}

#endif