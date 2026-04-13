# p.NET

> **Every private .NET API. As an extension member. At public-member speed.**

[![NuGet](https://img.shields.io/nuget/v/p.NET.svg?label=NuGet&logo=nuget)](https://www.nuget.org/packages/p.NET)
[![Downloads](https://img.shields.io/nuget/dt/p.NET.svg?label=Downloads&logo=nuget)](https://www.nuget.org/packages/p.NET)
[![CI](https://github.com/marius-bughiu/p.net/actions/workflows/ci.yml/badge.svg)](https://github.com/marius-bughiu/p.net/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Sponsor](https://img.shields.io/github/sponsors/marius-bughiu?logo=githubsponsors&color=ea4aaa)](https://github.com/sponsors/marius-bughiu)

```csharp
using PNet.System.Collections.Generic;

var list = new List<int> { 1, 2, 3, 4, 5 };
list.p_size = 3;                  // mutate the private _size field directly
                                  // list is now [1, 2, 3], no RemoveAt() needed

var dict = new Dictionary<string, int>(capacity: 1024);
int[] buckets = dict.p_buckets;   // peek at the private bucket array
int   version = dict.p_version;   // read the private version counter
```

No `FieldInfo`. No `Expression<T>.Compile()`. No `DynamicMethod`. No source generator running on your side. **Just IntelliSense.** Every private/internal field and method of every public BCL type, exposed as a `p_…` extension member that the JIT inlines into a direct field access.

---

## Why p.NET

| You want | Without p.NET | With p.NET |
|---|---|---|
| Read a private field | `typeof(T).GetField("…", BindingFlags.NonPublic\|BindingFlags.Instance).GetValue(obj)` | `obj.p_field` |
| Write a private field | `…field.SetValue(obj, value)` | `obj.p_field = value` |
| Call a private method | `…method.Invoke(obj, new object[]{ a, b })` | `obj.p_Method(a, b)` |
| Cost per access | ~30 ns + boxing + alloc | **0 ns**, no alloc, JIT-inlined |
| Discoverability | "what was that field called again?" | autocomplete on `obj.p_` |
| AOT-safe | ❌ | ✅ |

The wrapper for `list.p_size` decays at JIT time to literally `mov eax, [rcx + offset]` — byte-identical to a direct field read. There is no delegate, no metadata lookup, no reflection at runtime.

## Install

```sh
dotnet add package p.NET
```

Or in your `.csproj`:

```xml
<PackageReference Include="p.NET" Version="*" />
```

Then anywhere in your code:

```csharp
using PNet.System.Collections.Generic;
```

That's the whole installation. **Target framework: net10.0.**

## How it works

`p.NET` is a *normal* compiled .NET 10 library. It contains nothing but C# 14 [extension members](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14) backed by `[UnsafeAccessor]` `extern static` declarations.

Those extension members are written by a Roslyn source generator that runs **at our build time** (not yours). The generator:

1. Reads every BCL implementation DLL from the .NET 10 runtime via `System.Reflection.Metadata.PEReader`.
2. Walks every public type and enumerates every non-public member.
3. Validates each referenced type exists in the consumer's ref-pack via Roslyn (so the output compiles cleanly against what you can see).
4. Emits one `p_<name>` extension member per private/internal member.

The output ships in `lib/net10.0/PNet.dll`. **You install the package and forget there's a generator at all.**

```
your code  ── using PNet.System.Collections.Generic;
                     │
                     ▼
            PNet.dll (pre-built, in NuGet)
                     │  extension members → extern UnsafeAccessor
                     ▼
            JIT inlines to direct ldfld / call
                     │
                     ▼
            actual private member of List<T>
```

## Naming rule

The extension member name is `p_` + the original member name with **one** leading underscore stripped:

| Original member | Extension |
|---|---|
| `_size`        | `p_size` |
| `_items`       | `p_items` |
| `_version`     | `p_version` |
| `s_emptyArray` | `p_s_emptyArray` |
| `EnsureCapacity` | `p_EnsureCapacity` |

So you don't have to remember whether the BCL author used `_foo`, `s_foo`, or `Foo` — type `p_` and let IntelliSense show you everything.

## Coverage

**Every public type with non-public members across the entire .NET 10 BCL.**

The shipped `PNet.dll` carries ~1,600 generated types across ~110 namespaces — `System`, `System.Buffers`, `System.Collections.*`, `System.IO`, `System.Net.*`, `System.Reflection`, `System.Runtime.*`, `System.Security.Cryptography`, `System.Text`, `System.Threading.*`, `Microsoft.Win32`, even `Microsoft.VisualBasic`. Anything `using PNet.<X>;` could plausibly resolve, it does. New BCL types in future .NET releases automatically light up `p_*` members on the next p.NET version.

## Performance

| Access path | Per-call | Allocations | Inlineable |
|---|---|---|---|
| `FieldInfo.GetValue` | ~30 ns + boxing | ~24 B (boxed value type) | ❌ |
| Cached `Expression<T>.Compile()` | ~2 ns delegate dispatch | per-call boxing for value types | ❌ |
| Cached `DynamicMethod` delegate | ~2 ns delegate dispatch | none | ❌ |
| **`p.NET` extension member** | **0 ns** — direct `ldfld`/`call` | **none** | ✅ |
| Hand-written direct access (public) | 0 ns | none | ✅ |

p.NET extensions are AOT- and trim-safe. There is no reflection, no dynamic IL emission, nothing the linker has to keep alive.

## What's NOT generated (yet)

We deliberately skip a few categories so the library stays compilable everywhere:

- **Generic methods** (e.g. `List<T>.ConvertAll<TOutput>`) — reproducing constraint sets correctly is fiddly; v2.
- **Members whose signature references types not in your ref-pack** — e.g. an internal field typed as another internal helper. You couldn't name those types from your code anyway.
- **Pointer / function-pointer / ref-struct receivers**.
- **Static-class targets** (e.g. `System.Math`) — extension blocks need a receiver; v2.

A handful of members may also be unreachable due to current `UnsafeAccessor` JIT limitations on certain field shapes (e.g. generic-array fields like `List<T>._items` in .NET 10) — this is a runtime constraint, not a p.NET one.

## Build from source

```sh
git clone https://github.com/marius-bughiu/p.net
cd p.net
dotnet build p.net.sln
dotnet test tests/PNet.Tests
dotnet run --project samples/PNet.Samples.Basic
```

## Repo layout

```
src/
  PNet.Generators/    # internal Roslyn generator (netstandard2.0)
                      # reads runtime impl DLLs with PEReader,
                      # emits the extension-member source files
  PNet/               # the shipping library (net10.0) — only DLL in the package
tests/
  PNet.Tests/         # xUnit tests over the generated extensions vs real BCL types
samples/
  PNet.Samples.Basic/ # 30-second demo
```

## Safety

It's called **p.NET** for a reason. Touching another assembly's private state will break in undocumented ways across BCL versions. Don't ship this in code paths whose correctness depends on a specific runtime build.

If you're going to break the rules, you might as well do it at the speed of a direct field access.

## Sponsor

If p.NET saves you from yet another `BindingFlags.NonPublic` incantation, consider [sponsoring me](https://github.com/sponsors/marius-bughiu) ❤️

## License

[MIT](./LICENSE) — Marius Bughiu.
