namespace HKTool.ProjectManager;

public partial class ModProjectManager
{
    public static MethodBase GetMethodFromHandle = typeof(MethodBase)
        .GetMethod("GetMethodFromHandle", new Type[]{ typeof(RuntimeMethodHandle) });
    public static MethodBase GetFieldFromHandle = typeof(FieldInfo)
        .GetMethod("GetFieldFromHandle", new Type[]{ typeof(RuntimeFieldHandle) });
    public static MethodBase GetTypeFromHandle = typeof(Type)
        .GetMethod("GetTypeFromHandle", new Type[]{ typeof(RuntimeTypeHandle) });
    public static TypeDefinition FindType(string name, ModuleDefinition md)
    {
        foreach (var v in md.Types)
        {
            if (v.FullName == name) return v;
        }
        foreach (var v in md.AssemblyReferences)
        {
            var t = md.AssemblyResolver.Resolve(v)
            .MainModule.Types.FirstOrDefault(x => x.FullName == name);
            if (t != null) return t;
        }
        return null;
    }
    public static TypeDefinition FindTypeEx(string name, ModuleDefinition md)
    {
        var parts = name.Split('+');
        var parent = FindType(parts[0], md);
        if (parent == null) return null;
        for (int a = 1; a < parts.Length; a++)
        {
            var n = parts[a];
            var t = parent.NestedTypes.FirstOrDefault(x => x.Name == n);
            if (t == null) return null;
            parent = t;
        }
        return parent;
    }
    public static void IL_ReflectionHelperEx(MethodReference mr, MethodDefinition md, Instruction i)
    {
        if(mr.Name == "GetSelf")
        {
            i.OpCode = OpCodes.Ldarg_0;
            i.Operand = null;
            return;
        }
        var lastLdstr = i.Previous;
        if (lastLdstr.OpCode != OpCodes.Ldstr) return;
        var s = (string)lastLdstr.Operand;
        if (mr.Name == "GetFieldSelf")
        {
            var field = md.DeclaringType.Fields.FirstOrDefault(x => x.Name == s);
            lastLdstr.OpCode = OpCodes.Ldtoken;
            lastLdstr.Operand = field;
            i.Operand = md.Module.ImportReference(
                GetFieldFromHandle
                );
        }
        else if (mr.Name == "GetMethodSelf")
        {
            var method2 = md.DeclaringType.Methods.FirstOrDefault(x => x.Name == s);
            lastLdstr.OpCode = OpCodes.Ldtoken;
            lastLdstr.Operand = method2;
            i.Operand = md.Module.ImportReference(
                GetMethodFromHandle
                );
        }
        else if (mr.Name == "FindType")
        {
            var parent = FindTypeEx(s, md.Module);
            if (parent == null) return;
            lastLdstr.OpCode = OpCodes.Ldtoken;
            lastLdstr.Operand = md.Module.ImportReference(parent);
            i.Operand = md.Module.ImportReference(
                GetTypeFromHandle
                );
        }
        else if (mr.Name == "FindFieldInfo")
        {
            var tn = s.Substring(0, s.IndexOf(':'));
            var fn = s.Substring(s.LastIndexOf(':') + 1);
            var type = FindTypeEx(tn, md.Module);
            if (type == null) return;
            var field = type.Fields.FirstOrDefault(x => x.Name == fn);
            if(field == null) return;
            lastLdstr.OpCode = OpCodes.Ldtoken;
            lastLdstr.Operand = md.Module.ImportReference(field);
            i.Operand = md.Module.ImportReference(
                GetFieldFromHandle
                );
        }
        else if (mr.Name == "FindMethodBase")
        {
            var tn = s.Substring(0, s.IndexOf(':'));
            var fn = s.Substring(s.LastIndexOf(':') + 1);
            var type = FindTypeEx(tn, md.Module);
            if (type == null) return;
            var method = type.Methods.FirstOrDefault(x => x.Name == fn);
            if(method == null) return;
            lastLdstr.OpCode = OpCodes.Ldtoken;
            lastLdstr.Operand = md.Module.ImportReference(method);
            i.Operand = md.Module.ImportReference(
                GetMethodFromHandle
                );
        }
    }
}