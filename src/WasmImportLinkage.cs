#if !NET9_0_OR_GREATER
// temporarily add this attribute until it is available in dotnet 9
namespace System.Runtime.InteropServices
{
    internal partial class WasmImportLinkageAttribute : Attribute {}
}
#endif