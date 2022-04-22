namespace HKTool.ProjectManager;

public partial class ModProjectManager
{
    public string OutputDll
    {
        get
        {
            return Path.Combine(OutputPath, $"{ProjectData.ProjectName}.dll");
        }
    }
    public static void ILModifyAssembly(AssemblyDefinition ass)
    {
        foreach (var m in ass.Modules)
        {
            foreach (var v in m.Types) ILModifyType(v);
            var mscorlib = m.AssemblyReferences.FirstOrDefault(x => x.Name == "mscorlib");
            foreach(var a in m.AssemblyReferences)
            {
                if(a.Name == "System.Private.CoreLib")
                {
                    a.Name = mscorlib.Name;
                    a.Attributes = mscorlib.Attributes;
                    a.Hash = mscorlib.Hash;
                    a.PublicKey = mscorlib.PublicKey;
                    a.PublicKeyToken = mscorlib.PublicKeyToken;
                    a.Version = mscorlib.Version;
                    a.Culture = mscorlib.Culture;
                }
            }
        }
    }
    public static void ILModifyType(TypeDefinition type)
    {
        foreach (var v in type.Methods) ILModify(v);
        foreach (var v in type.NestedTypes) ILModifyType(v);
    }
    public static void ILModify(MethodDefinition method)
    {
        if (!method.HasBody) return;
        if (method.Body.Instructions.Count == 0) return;

        var i = method.Body.Instructions[0];
        var next = i;
        while ((i = next) is not null)
        {
            next = i.Next;
            if (i.OpCode == OpCodes.Call)
            {
                var m = (MethodReference)i.Operand;
                if (m.DeclaringType.FullName == "HKTool.Utils.Compile.ReflectionHelperEx")
                {
                    IL_ReflectionHelperEx(m, method, i);
                }
            }

        }
    }
    public bool Build()
    {
        if (!DownloadDependenciesDefault() || !DownloadModdingAPI())
        {
            return false;
        }
        List<string> dllDir = new();

        var metadataReference = new List<MetadataReference>();
        foreach (var v in Directory.EnumerateFiles(WebDependenciesPath, "*.dll", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(DependenciesPath, "*.dll", SearchOption.AllDirectories))
            )
        {
            if (ProjectData.IgnoreDlls.Contains(Path.GetFileName(v))) continue;
            try
            {
                var dir = Path.GetDirectoryName(v);
                if(!dllDir.Contains(dir)) dllDir.Add(dir);
                metadataReference.Add(MetadataReference.CreateFromFile(v));
                File.Copy(v, Path.Combine(OutputPath, Path.GetFileName(v)), true);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }
        foreach (var v in Directory.EnumerateFiles(ModdingAPIPath, "*.dll", SearchOption.AllDirectories))
        {
            if (ProjectData.IgnoreDlls.Contains(Path.GetFileName(v))) continue;
            try
            {
                var dir = Path.GetDirectoryName(v);
                if(!dllDir.Contains(dir)) dllDir.Add(dir);
                metadataReference.Add(MetadataReference.CreateFromFile(v));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }
        var syntaxTree = new List<SyntaxTree>();
        syntaxTree.Add(CSharpSyntaxTree.ParseText($"[assembly: System.Runtime.InteropServices.Guid(\"{ProjectData.Guid}\")]\n" +
            $"[assembly: System.Reflection.AssemblyVersion(\"{ProjectData.ModVersion}\")]\n"));
        if (ProjectData.UseGZip)
        {
            syntaxTree.Add(CSharpSyntaxTree.ParseText($"[assembly: HKTool.Attributes.EmbeddedResourceCompressionAttribute]"));
        }
        foreach (var v in Directory.EnumerateFiles(CodePath, "*.cs", SearchOption.AllDirectories))
        {
            try
            {
                syntaxTree.Add(CSharpSyntaxTree.ParseText(
                    File.ReadAllText(v), CSharpParseOptions.Default, v, Encoding.UTF8, default
                    ));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }

        List<ResourceDescription> ers = new List<ResourceDescription>();
        foreach (var v in ProjectData.EmbeddedResource)
        {
            var fp = Path.Combine(EmbeddedResourcePath, v.Key);
            if (File.Exists(fp))
            {
                Stream s = File.OpenRead(fp);
                if (ProjectData.UseGZip)
                {
                    var ms = new MemoryStream();
                    using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, true))
                    {
                        s.CopyTo(gzip);
                    }
                    ms.Position = 0;
                    s = ms;
                }
                var r0 = new ResourceDescription(v.Value, () => s, true);
                ers.Add(r0);
            }
        }
        var r = CSharpCompilation.Create(ProjectData.ProjectName)
            .WithOptions(new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            true, ProjectData.ProjectName, null, null,
            null, OptimizationLevel.Release, true, true, null, null, default, null,
            Platform.AnyCpu, ReportDiagnostic.Warn, 4, null, true, false, null,
            null
            )
            .WithAllowUnsafe(true)
            .WithNullableContextOptions(ProjectData.EnableNullable ? NullableContextOptions.Enable : NullableContextOptions.Disable)
            ).AddSyntaxTrees(syntaxTree)
            .AddReferences(metadataReference)
            .Emit(OutputDll,
            Path.Combine(OutputPath, $"{ProjectData.ProjectName}.pdb"),
            Path.Combine(OutputPath, $"{ProjectData.ProjectName}.xml"),
            null, ers, default);
        if (!r.Success)
        {
            foreach (var v in r.Diagnostics)
            {
                if (v.Id == "CS8019" || v.Id == "CS1701") continue;
                switch (v.Severity)
                {
                    case DiagnosticSeverity.Error:
                        Console.Error.WriteLine(v.ToString());
                        break;
                    case DiagnosticSeverity.Warning:
                    case DiagnosticSeverity.Info:
                        Console.WriteLine(v.ToString());
                        break;
                }
            }
            Console.Error.WriteLine("Failed!");
            return false;
        }
        else
        {
            using (var ar = new DefaultAssemblyResolver())
            {
                foreach(var v in dllDir) ar.AddSearchDirectory(v);
                using (var s = new MemoryStream(File.ReadAllBytes(OutputDll)))
                using (var ad = AssemblyDefinition.ReadAssembly(s, new ReaderParameters()
                {
                    AssemblyResolver = ar
                }))
                {
                    ILModifyAssembly(ad);
                    ad.Write(OutputDll, new WriterParameters()
                    {

                    });
                }
            }
            if (!BuildInGithub)
            {
                foreach (var v in r.Diagnostics)
                {
                    if (v.Id == "CS8019" || v.Id == "CS1701") continue;
                    switch (v.Severity)
                    {
                        case DiagnosticSeverity.Error:
                            Console.Error.WriteLine(v.ToString());
                            break;
                        case DiagnosticSeverity.Warning:
                        case DiagnosticSeverity.Info:
                            Console.WriteLine(v.ToString());
                            break;
                    }
                }
            }

            Console.WriteLine(
                "SHA256(" + $"{ProjectData.ProjectName}.dll): " +
                BitConverter.ToString(SHA256.HashData(File.ReadAllBytes(Path.Combine(OutputPath, $"{ProjectData.ProjectName}.dll"))))
                .Replace("-", "").ToLower());
            if (ProjectData.CreateZip)
            {
                using (Stream stream = File.OpenWrite(Path.Combine(OutputPath, $"{ProjectData.ProjectName}.zip")))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(Path.Combine(OutputPath, $"{ProjectData.ProjectName}.dll"), $"{ProjectData.ProjectName}.dll");
                    zip.CreateEntryFromFile(Path.Combine(OutputPath, $"{ProjectData.ProjectName}.pdb"), $"{ProjectData.ProjectName}.pdb");
                    foreach (var v in ProjectData.ZipFiles)
                    {
                        var name = string.IsNullOrEmpty(v.Value) ? v.Key : v.Value;
                        zip.CreateEntryFromFile(Path.Combine(OutputPath, v.Key), name);
                    }
                }
                Console.WriteLine(
                "SHA256(" + $"{ProjectData.ProjectName}.zip): " +
                BitConverter.ToString(SHA256.HashData(File.ReadAllBytes(Path.Combine(OutputPath, $"{ProjectData.ProjectName}.zip"))))
                .Replace("-", "").ToLower());
            }
            return true;
        }
    }
}
