// .NET Framework bootstrap: no native runtime extraction in a user-writable temp folder.
using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows.Forms;

internal static class AgentBootstrap
{
    [STAThread]
    static int Main(string[] args)
    {
        bool silent=Array.IndexOf(args,"--silent")>=0;
        string root=null;
        try
        {
            string parent=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if((File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new IOException();
            root=Path.Combine(parent,"WFI-Setup-"+Guid.NewGuid().ToString("N"));
            var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
            foreach(string sid in new[]{"S-1-5-18","S-1-5-32-544"})acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            Directory.CreateDirectory(root,acl);
            using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("WfiPayload"))
            using(var zip=new ZipArchive(resource,ZipArchiveMode.Read))
            foreach(var entry in zip.Entries)
            {
                string path=Path.GetFullPath(Path.Combine(root,entry.FullName));
                if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException();
                if(entry.Name.Length==0){Directory.CreateDirectory(path);continue;}
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var input=entry.Open())using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
            }
            var start=new ProcessStartInfo(Path.Combine(root,"WFI-Agent-Setup.exe"));
            start.UseShellExecute=false;start.WorkingDirectory=root;
            start.Arguments=string.Join(" ",Array.ConvertAll(args,Quote));
            using(var process=Process.Start(start)){process.WaitForExit();return process.ExitCode;}
        }
        catch{if(!silent)MessageBox.Show("Kurulum paketi açılamadı. Yönetici yetkisini, disk alanını ve paket bütünlüğünü kontrol edin.","WFI Setup");return 10;}
        finally
        {
            if(root!=null&&Directory.Exists(root))
            {
                try
                {
                    bool safe=true;foreach(var path in Directory.EnumerateFileSystemEntries(root,"*",SearchOption.AllDirectories))if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0){safe=false;break;}
                    if(safe&&Path.GetFileName(root).StartsWith("WFI-Setup-",StringComparison.Ordinal)&&Path.GetDirectoryName(root)==Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))Directory.Delete(root,true);
                }catch{/* A failed cleanup leaves an admin-only staging directory, never removes an unrelated path. */}
            }
        }
    }
    static string Quote(string value)
    {
        var result=new System.Text.StringBuilder("\"");int slashes=0;
        foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"'){result.Append('\\',slashes*2+1);result.Append(c);}else{result.Append('\\',slashes);result.Append(c);}slashes=0;}
        result.Append('\\',slashes*2);return result.Append('"').ToString();
    }
}
