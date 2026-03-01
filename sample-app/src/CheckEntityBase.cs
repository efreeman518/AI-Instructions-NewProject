using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
var resolver = new PathAssemblyResolver(Directory.GetFiles(runtimeDir, "*.dll")
    .Concat(new[] { args[0] }));
using var mlc = new MetadataLoadContext(resolver);
var asm = mlc.LoadFromAssemblyPath(args[0]);
foreach (var t in asm.GetTypes().Where(t => t.Name == "EntityBase"))
{
    Console.WriteLine($"Type: {t.FullName} IsPublic={t.IsPublic} IsAbstract={t.IsAbstract}");
    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        var getter = p.GetGetMethod(true);
        var access = getter?.IsPublic == true ? "public" : getter?.IsFamily == true ? "protected" : getter?.IsAssembly == true ? "internal" : getter?.IsFamilyOrAssembly == true ? "protected internal" : "private";
        Console.WriteLine($"  {access} {p.PropertyType.Name} {p.Name}");
    }
}
