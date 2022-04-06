

namespace HKTool.ProjectManager;

[Serializable]
public class WebDependencyFileInfo
{
    public Dictionary<string, byte[]> Files { get; set; } = new Dictionary<string, byte[]>();
}
[Serializable]
public class WebDependenciesInfo
{
    [NonSerialized]
    public string savePath = "";
    public Dictionary<string, WebDependencyFileInfo> Files { get; set; } = new Dictionary<string, WebDependencyFileInfo>();
}

[Serializable]
public class ProjectData
{
    public string ProjectName { get; set; } = "UNKNOW";
    public string ModVersion { get; set; } = "0.0.0.0";
    public string CodeDir { get; set; } = @".\Scripts\";
    public string EmbeddedResourceDir { get; set; } = @".\Res\";
    public Dictionary<string, string> EmbeddedResource { get; set; } = new Dictionary<string, string>();
    public string Guid { get; set; }
    public string DependenciesDir { get; set; } = @".\Dependencies\";
    public string ModdingAPIVersion { get; set; } = "";
    public List<string> IgnoreDlls { get; set; } = new List<string>();
    public List<string> WebDependencies { get; set; } = new List<string>();
    public bool UseGZip { get; set; } = true;
    public bool UseCommonLibrary { get; set; } = true;
    public bool CreateZip { get; set; } = true;
    public Dictionary<string, string> ZipFiles { get; set; } = new Dictionary<string, string>();
    public bool EnableNullable { get; set; } = true;
}

