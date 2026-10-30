using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
[assembly: AssemblyCompany("Berkay Atağ")]
[assembly: AssemblyProduct("WFI")]
[assembly: AssemblyFileVersion("0.7.1.0")]
[assembly: AssemblyVersion("0.7.1.0")]
[assembly: AssemblyInformationalVersion("0.7.1")]
[assembly: AssemblyTitle("WFI Uninstall")]
[assembly: AssemblyDescription("Windows Failure Intelligence (WFI)")]
[assembly: AssemblyCopyright("© 2026 Berkay Atağ")]
internal static class UninstallBootstrap {
 [STAThread] static int Main(string[] args) {
  try {
   if(args.Length!=0)return 2;
   if(MessageBox.Show("WFI kaldırılsın mı? Yapılandırma, cihaz kimliği ve veriler korunur.","WFI",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return 0;
   var root=AppDomain.CurrentDomain.BaseDirectory;
   var script=Path.Combine(root,"Uninstall-Wfi.ps1");
   var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"));
   start.UseShellExecute=false;start.CreateNoWindow=true;start.Arguments="-NoProfile -ExecutionPolicy Bypass -File \""+script+"\"";
   using(var process=Process.Start(start)){process.WaitForExit();if(process.ExitCode!=0)MessageBox.Show("Kaldırma tamamlanamadı. Kurulum rehberini inceleyin.","WFI");return process.ExitCode;}
  }catch{MessageBox.Show("Kaldırma başlatılamadı.","WFI");return 10;}
 }
}
