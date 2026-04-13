using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace PNet.Tests;

/// <summary>
/// Per-namespace coverage. p.NET ships pre-generated extension members for every public BCL type
/// across 107 namespaces — these tests assert each generated namespace is actually populated in
/// PNet.dll, so a regression that wipes a whole namespace would fail loudly.
/// </summary>
public class NamespaceCoverageTests
{
    private static readonly Assembly PNetAssembly =
        typeof(PNet.System.Collections.Generic.List_1_PrivateExtensions).Assembly;

    [Theory]
    [InlineData("Microsoft.VisualBasic")]
    [InlineData("Microsoft.VisualBasic.CompilerServices")]
    [InlineData("Microsoft.VisualBasic.FileIO")]
    [InlineData("Microsoft.Win32")]
    [InlineData("Microsoft.Win32.SafeHandles")]
    [InlineData("System")]
    [InlineData("System.Buffers")]
    [InlineData("System.CodeDom.Compiler")]
    [InlineData("System.Collections")]
    [InlineData("System.Collections.Concurrent")]
    [InlineData("System.Collections.Frozen")]
    [InlineData("System.Collections.Generic")]
    [InlineData("System.Collections.Immutable")]
    [InlineData("System.Collections.ObjectModel")]
    [InlineData("System.Collections.Specialized")]
    [InlineData("System.ComponentModel")]
    [InlineData("System.ComponentModel.DataAnnotations")]
    [InlineData("System.ComponentModel.DataAnnotations.Schema")]
    [InlineData("System.ComponentModel.Design")]
    [InlineData("System.ComponentModel.Design.Serialization")]
    [InlineData("System.Data")]
    [InlineData("System.Data.Common")]
    [InlineData("System.Data.SqlTypes")]
    [InlineData("System.Diagnostics")]
    [InlineData("System.Diagnostics.Contracts")]
    [InlineData("System.Diagnostics.Metrics")]
    [InlineData("System.Diagnostics.SymbolStore")]
    [InlineData("System.Diagnostics.Tracing")]
    [InlineData("System.Drawing")]
    [InlineData("System.Dynamic")]
    [InlineData("System.Formats.Asn1")]
    [InlineData("System.Formats.Tar")]
    [InlineData("System.Globalization")]
    [InlineData("System.IO")]
    [InlineData("System.IO.Compression")]
    [InlineData("System.IO.Enumeration")]
    [InlineData("System.IO.IsolatedStorage")]
    [InlineData("System.IO.MemoryMappedFiles")]
    [InlineData("System.IO.Pipelines")]
    [InlineData("System.IO.Pipes")]
    [InlineData("System.Linq")]
    [InlineData("System.Linq.Expressions")]
    [InlineData("System.Net")]
    [InlineData("System.Net.Cache")]
    [InlineData("System.Net.Http")]
    [InlineData("System.Net.Http.Headers")]
    [InlineData("System.Net.Http.Json")]
    [InlineData("System.Net.Http.Metrics")]
    [InlineData("System.Net.Mail")]
    [InlineData("System.Net.Mime")]
    [InlineData("System.Net.NetworkInformation")]
    [InlineData("System.Net.Quic")]
    [InlineData("System.Net.Security")]
    [InlineData("System.Net.ServerSentEvents")]
    [InlineData("System.Net.Sockets")]
    [InlineData("System.Net.WebSockets")]
    [InlineData("System.Numerics")]
    [InlineData("System.Reflection")]
    [InlineData("System.Reflection.Emit")]
    [InlineData("System.Reflection.Metadata")]
    [InlineData("System.Reflection.Metadata.Ecma335")]
    [InlineData("System.Reflection.PortableExecutable")]
    [InlineData("System.Resources")]
    [InlineData("System.Runtime")]
    [InlineData("System.Runtime.CompilerServices")]
    [InlineData("System.Runtime.ConstrainedExecution")]
    [InlineData("System.Runtime.ExceptionServices")]
    [InlineData("System.Runtime.InteropServices")]
    [InlineData("System.Runtime.InteropServices.Marshalling")]
    [InlineData("System.Runtime.Intrinsics")]
    [InlineData("System.Runtime.Loader")]
    [InlineData("System.Runtime.Remoting")]
    [InlineData("System.Runtime.Serialization")]
    [InlineData("System.Runtime.Serialization.DataContracts")]
    [InlineData("System.Runtime.Serialization.Formatters.Binary")]
    [InlineData("System.Runtime.Serialization.Json")]
    [InlineData("System.Runtime.Versioning")]
    [InlineData("System.Security")]
    [InlineData("System.Security.AccessControl")]
    [InlineData("System.Security.Authentication.ExtendedProtection")]
    [InlineData("System.Security.Claims")]
    [InlineData("System.Security.Cryptography")]
    [InlineData("System.Security.Cryptography.X509Certificates")]
    [InlineData("System.Security.Principal")]
    [InlineData("System.Text")]
    [InlineData("System.Text.Encodings.Web")]
    [InlineData("System.Text.Json")]
    [InlineData("System.Text.Json.Nodes")]
    [InlineData("System.Text.Json.Schema")]
    [InlineData("System.Text.Json.Serialization")]
    [InlineData("System.Text.Json.Serialization.Metadata")]
    [InlineData("System.Text.RegularExpressions")]
    [InlineData("System.Threading")]
    [InlineData("System.Threading.Channels")]
    [InlineData("System.Threading.Tasks")]
    [InlineData("System.Threading.Tasks.Dataflow")]
    [InlineData("System.Threading.Tasks.Sources")]
    [InlineData("System.Timers")]
    [InlineData("System.Transactions")]
    [InlineData("System.Windows.Markup")]
    [InlineData("System.Xml")]
    [InlineData("System.Xml.Linq")]
    [InlineData("System.Xml.Resolvers")]
    [InlineData("System.Xml.Schema")]
    [InlineData("System.Xml.Serialization")]
    [InlineData("System.Xml.XPath")]
    [InlineData("System.Xml.Xsl")]
    public void Generated_namespace_is_populated_in_PNet_assembly(string namespaceWithoutPrefix)
    {
        var fullNamespace = "PNet." + namespaceWithoutPrefix;

        var typesInNamespace = PNetAssembly.GetTypes()
            .Where(t => t.Namespace == fullNamespace)
            .ToList();

        typesInNamespace.Should().NotBeEmpty(
            "PNet.dll should contain at least one generated type in {0}", fullNamespace);

        // At least one type should expose a `p_*` member surface (whether as a method on the
        // static class or as a nested extension type — C# 14 extension lowering varies).
        bool anyExtensionMember = typesInNamespace.Any(t => HasAnyPMember(t));

        anyExtensionMember.Should().BeTrue(
            "namespace {0} should expose at least one `p_*` extension member", fullNamespace);
    }

    private static bool HasAnyPMember(global::System.Type type)
    {
        const BindingFlags AllMembers =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        if (type.GetMethods(AllMembers).Any(m => m.Name.StartsWith("p_") || m.Name.Contains("|p_")))
            return true;
        if (type.GetProperties(AllMembers).Any(p => p.Name.StartsWith("p_")))
            return true;
        // Nested types created by the C# 14 extension lowering.
        foreach (var nested in type.GetNestedTypes(AllMembers))
            if (HasAnyPMember(nested)) return true;
        return false;
    }
}
