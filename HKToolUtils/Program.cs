using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft;
using HKTool.ProjectManager;

namespace HKToolUtils
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2) return;
            var cmd = args[0];
            bool result = false;
            if (cmd.Equals("NewProject", StringComparison.OrdinalIgnoreCase) && args.Length == 3)
            {
                var name = args[2];

                using (var gitignoreS = new StreamReader(
                    Assembly.GetExecutingAssembly().GetManifestResourceStream("HKToolUtils.gitignoreTemplate.txt")))
                {
                    File.WriteAllText(".gitignore", gitignoreS.ReadToEnd());
                }
                using (var ghwork = new StreamReader(
                    Assembly.GetExecutingAssembly().GetManifestResourceStream("HKToolUtils.ghwork.yml")))
                {
                    Directory.CreateDirectory(".github/workflows");
                    File.WriteAllText(".github/workflows/build.yml", 
                        ghwork.ReadToEnd().Replace("{{ProjectName}}", name));
                }
                
                var p = ModProjectFactory.CreateModProject(name, Path.GetFullPath(args[1]));
                result = true;
                //p.CreateMSProject();
            }
            else if(cmd.Equals("RefreshMSProject", StringComparison.OrdinalIgnoreCase))
            {
                var p = ModProjectFactory.OpenModProject(Path.GetFullPath(args[1]));
                p.CreateMSProject();
                result = true;
            }
            else if(cmd.Equals("Build", StringComparison.OrdinalIgnoreCase))
            {
                var p = ModProjectFactory.OpenModProject(Path.GetFullPath(args[1]));
                result = p.Build();
            }
            else if (cmd.Equals("BuildInGithub", StringComparison.OrdinalIgnoreCase))
            {
                var p = ModProjectFactory.OpenModProject(Path.GetFullPath(args[1]));
                p.BuildInGithub = true;
                result = p.Build();
            }
            else if (cmd.Equals("DownloadDependencies", StringComparison.OrdinalIgnoreCase))
            {
                var p = ModProjectFactory.OpenModProject(Path.GetFullPath(args[1]));
                result = p.DownloadDependenciesDefault(true) && p.DownloadModdingAPI(true);
            }
            else if(cmd.Equals("DownloadMAPI", StringComparison.OrdinalIgnoreCase))
            {
                var p = ModProjectFactory.OpenModProject(Path.GetFullPath(args[1]));
                result = p.DownloadModdingAPI(true);
            }
            if(!result) Environment.Exit(-1);
            Environment.Exit(0);
        }
    }
}
