// Verifies every [HarmonyPatch] in Cloakcraft.dll points at a method that really exists in the
// game assemblies in lib/. Run after every Valheim update: dotnet run --project tests -- build/Cloakcraft.dll lib
using System.Reflection;

var dll = Path.GetFullPath(args.Length > 0 ? args[0] : "build/Cloakcraft.dll");
var lib = Path.GetFullPath(args.Length > 1 ? args[1] : "lib");
var paths = Directory.GetFiles(lib, "*.dll").Append(dll).ToList();
var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths), "mscorlib");
var asm = mlc.LoadFromAssemblyPath(dll);
int checks = 0, failures = 0;

foreach (var type in asm.GetTypes())
{
    string? targetType = null, method = null; Type[]? sig = null;
    foreach (var a in type.GetCustomAttributesData().Where(a => a.AttributeType.Name == "HarmonyPatch"))
    {
        var ctorArgs = a.ConstructorArguments;
        if (ctorArgs.Count >= 1 && ctorArgs[0].ArgumentType.Name == "Type") targetType = ((Type)ctorArgs[0].Value!).FullName;
        if (ctorArgs.Count >= 2 && ctorArgs[1].ArgumentType.Name == "String") method = (string)ctorArgs[1].Value!;
        if (ctorArgs.Count >= 3 && ctorArgs[2].Value is IReadOnlyCollection<CustomAttributeTypedArgument> arr)
            sig = arr.Select(x => (Type)x.Value!).ToArray();
    }
    if (targetType == null || method == null) continue;
    checks++;
    var t = paths.Select(p => { try { return mlc.LoadFromAssemblyPath(p).GetType(targetType); } catch { return null; } }).FirstOrDefault(x => x != null);
    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    var found = t == null ? null
        : sig == null ? t.GetMethods(flags).FirstOrDefault(m => m.Name == method)
        : t.GetMethods(flags).FirstOrDefault(m => m.Name == method && m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(sig.Select(s => s.FullName)));
    Console.WriteLine($"{(found != null ? "ok  " : "FAIL")} {targetType}.{method}{(sig != null ? "(" + string.Join(",", sig.Select(s => s.Name)) + ")" : "")}  <- {type.Name}");
    if (found == null) failures++;
}
// Private members reached by reflection (AccessTools) in the mod source.
var reflected = new (string type, string member)[] {
    ("Humanoid", "m_shoulderItem"), ("Humanoid", "m_visEquipment"), ("VisEquipment", "m_shoulderItemInstances"),
    ("ZNetScene", "m_namedPrefabs"), ("SEMan", "m_character"), ("ObjectDB", "UpdateRegisters"), ("Localization", "AddWord"), ("Player", "m_underRoof"), ("Container", "m_nview"), ("Container", "CheckAccess"), ("InventoryGui", "UpdateCraftingPanel"), ("Character", "m_animator"), ("EnvSetup", "m_psystems"), ("Utils", "GetBoneTransform"), ("ShieldGenerator", "IsInsideShield"), 
};
foreach (var (tn, mn) in reflected)
{
    checks++;
    var t = paths.Select(p => { try { return mlc.LoadFromAssemblyPath(p).GetType(tn); } catch { return null; } }).FirstOrDefault(x => x != null);
    var ok = t != null && t.GetMember(mn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Length > 0;
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {tn}.{mn}  <- reflection");
    if (!ok) failures++;
}
Console.WriteLine($"{checks} targets checked, {failures} missing");
return failures == 0 ? 0 : 1;
