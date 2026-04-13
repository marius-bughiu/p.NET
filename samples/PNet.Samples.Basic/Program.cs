using PNet.System.Collections.Generic; // unlocks `p_*` extension members generated at build time

// Read private integer state on List<T>
var list = new List<int> { 1, 2, 3, 4, 5 };
Console.WriteLine($"list.Count   (public)  = {list.Count}");
Console.WriteLine($"list.p_size  (private) = {list.p_size}");
Console.WriteLine($"list.p_version         = {list.p_version}");

// Mutate private `_size` by-ref — no reflection, no delegates, no allocations.
list.p_size = 3;
Console.WriteLine($"after list.p_size = 3  → list.Count = {list.Count}");
Console.WriteLine($"elements: [{string.Join(", ", list)}]");

Console.WriteLine();

// Dictionary<K,V> internals
var dict = new Dictionary<string, int> { ["alpha"] = 1, ["beta"] = 2, ["gamma"] = 3 };
Console.WriteLine($"Dictionary<string,int> private fields:");
Console.WriteLine($"  p_count         = {dict.p_count}");
Console.WriteLine($"  p_version       = {dict.p_version}");
Console.WriteLine($"  p_freeList      = {dict.p_freeList}");
Console.WriteLine($"  p_freeCount     = {dict.p_freeCount}");
Console.WriteLine($"  p_buckets.Length = {dict.p_buckets.Length}");

Console.WriteLine();

// HashSet<T> internals
var set = new HashSet<int> { 1, 2, 3 };
Console.WriteLine($"HashSet<int> private fields:");
Console.WriteLine($"  p_count    = {set.p_count}");
Console.WriteLine($"  p_version  = {set.p_version}");
