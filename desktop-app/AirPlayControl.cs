using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Interop;

class Settings {
 public string Name="AirPlay Receiver"; public int Width=1920, Height=1080, Fps=60; public bool Hls=true;
 public void Validate() {
  if (String.IsNullOrWhiteSpace(Name) || Name.Length>40 || Name.Any(c=>Char.IsControl(c))) throw new Exception("Device name must contain 1–40 characters, with no line breaks or control characters.");
  if (Width<320 || Width>7680 || Height<240 || Height>4320 || Width%2!=0 || Height%2!=0) throw new Exception("Width must be 320–7680 and height 240–4320. Both must be even numbers.");
  if (Fps!=24 && Fps!=30 && Fps!=60) throw new Exception("Choose 24, 30 or 60 fps.");
 }
 public static Settings Load(string path) {
  var s=new Settings(); if(!File.Exists(path)) return s;
  foreach(var line in File.ReadAllLines(path)) {
   int i=line.IndexOf('='); if(i<0 || line.TrimStart().StartsWith("#")) continue;
   string k=line.Substring(0,i).Trim(), v=line.Substring(i+1).Trim();
   switch(k) {
    case "airplay.server-name": case "airplay.serverName": s.Name=Regex.Replace(v,@"\\u([0-9a-fA-F]{4})",m=>((char)Convert.ToInt32(m.Groups[1].Value,16)).ToString());break;
    case "airplay.width": s.Width=Int32.Parse(v);break;
    case "airplay.height": s.Height=Int32.Parse(v);break;
    case "airplay.fps": s.Fps=Int32.Parse(v);break;
    case "airplay.hls-enabled": s.Hls=Boolean.Parse(v);break;
   }
  } s.Validate(); return s;
 }
 public void Save(string path) {
  Validate(); var name=new StringBuilder();
  foreach(char c in Name.Trim()) name.Append(c>126 || c=='\\' ? "\\u"+((int)c).ToString("x4") : c.ToString());
  string value="# Managed by AirPlay Control\r\nairplay.server-name="+name+"\r\nairplay.width="+Width+"\r\nairplay.height="+Height+"\r\nairplay.fps="+Fps+"\r\nairplay.hls-enabled="+Hls.ToString().ToLowerInvariant()+"\r\nairplay.tray-enabled=false\r\nairplay.mdns-loopback=false\r\nclient.video.src=none\r\nclient.audio.src=none\r\ndump.enabled=false\r\nspring.output.ansi.enabled=NEVER\r\nlogging.file.name=logs/server.log\r\n";
  Directory.CreateDirectory(Path.GetDirectoryName(path));
  string tmp=path+".tmp"; File.WriteAllText(tmp,value,new UTF8Encoding(false));
  if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path);
 }
}

class ChildJob : IDisposable {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr a,string n);
 [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr j,int c,ref Limits l,int s);
 [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr j,IntPtr p);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 [StructLayout(LayoutKind.Sequential)] struct Basic { public long a,b; public uint flags; public UIntPtr min,max; public uint count; public UIntPtr affinity; public uint priority,scheduling; }
 [StructLayout(LayoutKind.Sequential)] struct Io { public ulong a,b,c,d,e,f; }
 [StructLayout(LayoutKind.Sequential)] struct Limits { public Basic basic; public Io io; public UIntPtr a,b,c,d; }
 IntPtr handle;
 public ChildJob(Process p) { handle=CreateJobObject(IntPtr.Zero,null); var l=new Limits(); l.basic.flags=0x2000;
  if(handle==IntPtr.Zero || !SetInformationJobObject(handle,9,ref l,Marshal.SizeOf(l)) || !AssignProcessToJobObject(handle,p.Handle)) { Dispose(); throw new Exception("Unable to manage the receiver process. Please reopen the app."); }
 }
 public void Dispose() { if(handle!=IntPtr.Zero) { CloseHandle(handle);handle=IntPtr.Zero; } }
}

class AirPlayControl {
 static string Root=AppDomain.CurrentDomain.BaseDirectory;
 static string Data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AirPlayControl");
 static string Config=Path.Combine(Data,"application.properties");
 static int JavaMajor(string output) {
  var match=Regex.Match(output,"(?:version\\s+\"|openjdk\\s+)([0-9]+)");
  return match.Success?Int32.Parse(match.Groups[1].Value):0;
 }
 static string FindJava() {
  var candidates=new System.Collections.Generic.List<string>();
  string javaHome=Environment.GetEnvironmentVariable("JAVA_HOME");
  if(!String.IsNullOrWhiteSpace(javaHome)) candidates.Add(Path.Combine(javaHome.Trim('"'),"bin","java.exe"));
  foreach(string entry in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)) {
   try {if(!String.IsNullOrWhiteSpace(entry)) candidates.Add(Path.Combine(entry.Trim().Trim('"'),"java.exe"));} catch(ArgumentException) {}
  }
  foreach(string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase)) {
   if(!File.Exists(candidate)) continue;
   try {using(var p=Process.Start(new ProcessStartInfo(candidate,"-XshowSettings:properties -version") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})) {
    var errorOutput=p.StandardError.ReadToEndAsync();var standardOutput=p.StandardOutput.ReadToEndAsync();
    if(!p.WaitForExit(5000)) {p.Kill();continue;}
    string output=errorOutput.Result+standardOutput.Result;
    if(p.ExitCode==0 && JavaMajor(output)>=21) {
     var homeMatch=Regex.Match(output,@"(?m)^\s*java\.home\s*=\s*(.+?)\r?$");
     string actual=homeMatch.Success?Path.Combine(homeMatch.Groups[1].Value.Trim(),"bin","java.exe"):candidate;
     if(File.Exists(actual)) return actual;
    }
   }} catch(System.ComponentModel.Win32Exception) {} catch(IOException) {}
  }
  throw new Exception("Java 21 or later was not found. Open Download Java.html, install Java with Add to PATH enabled, then reopen this app. You can also set JAVA_HOME to your Java installation folder.");
 }
 static string Jar=Path.Combine(Root,"releases","java-airplay-mse-1.1.1-windowed.jar");
 static string Browser() { return new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft","Edge","Application","msedge.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Google","Chrome","Application","chrome.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Google","Chrome","Application","chrome.exe")}.FirstOrDefault(File.Exists); }
 static string Quote(string s) { return "\""+s+"\""; }
 static ProcessStartInfo Engine(string dir) {
  string java=FindJava();
  if(!File.Exists(Jar)) throw new Exception("The AirPlay v1.1.1 application file is missing.");
  var browser=Browser(); if(browser==null) throw new Exception("Microsoft Edge or Google Chrome was not found. Please install either browser.");
  return new ProcessStartInfo(java,"--enable-native-access=ALL-UNNAMED "+Quote("-Dairplay.mse.browser="+browser)+" -jar "+Quote(Jar)+" --spring.config.additional-location="+Quote(new Uri(Path.Combine(dir,"application.properties")).AbsoluteUri)) { WorkingDirectory=dir,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 };
 }
 Window window; Process child; ChildJob job; bool stopping,closing,ready,mseReady,registered,connected,failed; DateTime started; string activeName;
 TextBox name,width,height,log; ComboBox resolution,fps; CheckBox hls; Button start,stop,save; TextBlock status,detail,feedback;
 T Find<T>(string n) { return (T)window.FindName(n); }
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr w,int a,ref int v,int s);
 void Ui(Action a) { if(!closing) window.Dispatcher.BeginInvoke(a); }
 void State(string s,string d) { status.Text=s;detail.Text=d; }
 int themeMode; bool darkTheme;
 static int NextTheme(int mode) {return (mode+1)%3;}
 static int ReadTheme(string path) {int mode;return File.Exists(path) && Int32.TryParse(File.ReadAllText(path),out mode) && mode>=0 && mode<=2?mode:0;}
 static bool SystemIsDark() {
  using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
   return key!=null && Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0;
 }
 void ApplyTheme() {
  darkTheme=themeMode==2 || (themeMode==0 && SystemIsDark());
  string[] keys={"WindowBrush","TextBrush","MutedBrush","SurfaceBrush","InputBrush","BorderBrush","BadgeBrush","IconBrush","AccentBrush","AccentTextBrush","SelectionBrush"};
  string[] light={"#F3F3F3","#1B1B1B","#606060","#FFFFFF","#FCFCFC","#D0D0D0","#E5EAF0","#E8F1FB","#0067C0","#FFFFFF","#E5F1FB"};
  string[] dark={"#202020","#F5F5F5","#BDBDBD","#2B2B2B","#333333","#505050","#383E46","#263E55","#60CDFF","#102431","#354B5C"};
  for(int i=0;i<keys.Length;i++) window.Resources[keys[i]]=new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString((darkTheme?dark:light)[i]));
  var button=Find<Button>("Theme");button.Content="Theme: "+new[]{"System","Light","Dark"}[themeMode];button.ToolTip="Switch to "+new[]{"System","Light","Dark"}[NextTheme(themeMode)]+" theme";
  int value=darkTheme?1:0;var handle=new WindowInteropHelper(window).Handle;if(handle!=IntPtr.Zero)DwmSetWindowAttribute(handle,20,ref value,4);
 }
 void LogLine(string s) {
  if(s==null) return;
  s=Regex.Replace(s,@"\x1B\[[0-?]*[ -/]*[@-~]","");
  try { Directory.CreateDirectory(Path.Combine(Data,"logs")); File.AppendAllText(Path.Combine(Data,"logs","control.log"),s+Environment.NewLine,Encoding.UTF8); } catch(IOException) {} catch(UnauthorizedAccessException) {}
  log.AppendText(s+Environment.NewLine); if(log.Text.Length>60000) log.Text=log.Text.Substring(log.Text.Length-40000); log.ScrollToEnd();
  if(s.Contains("MsePlayer ready")) mseReady=true;
  if(s.Contains("_airplay._tcp.local registered on")) registered=true;
  if(s.Contains("Started MsePlayerApp")) ready=true;
  if(s.Contains("MSE: no Chromium") || s.Contains("MSE failed") || s.Contains("APPLICATION FAILED")) { failed=true; State("Receiver error","Expand Details and logs for the error. Check that your browser works."); return; }
  if(s.Contains("MSE presentation ready")) connected=true;
  if(s.Contains("MSE mirror stopped")) connected=false;
  if(ready && mseReady && registered && !stopping && !failed) {
   if(connected) State("Mirroring","Your screen appears in a separate player window.");
   else State("Ready to connect","In Screen Mirroring, select "+activeName+".");
  }
 }
 Settings Read() { var s=new Settings {Name=name.Text.Trim(), Hls=hls.IsChecked==true};
  if(!Int32.TryParse(width.Text,out s.Width) || !Int32.TryParse(height.Text,out s.Height)) throw new Exception("Enter a valid whole number for width and height.");
  s.Fps=Int32.Parse((string)((ComboBoxItem)fps.SelectedItem).Tag); s.Validate(); return s;
 }
 void Start() {
  if(child!=null) return;
  try {
   var settings=Read(); settings.Save(Config); activeName=settings.Name; ready=mseReady=registered=connected=failed=false; stopping=false; started=DateTime.UtcNow;
   child=new Process {StartInfo=Engine(Data),EnableRaisingEvents=true}; var p=child;
   p.OutputDataReceived+=(s,e)=>Ui(()=>{if(child==p)LogLine(e.Data);}); p.ErrorDataReceived+=(s,e)=>Ui(()=>{if(child==p)LogLine(e.Data);});
   p.Exited+=(s,e)=>Ui(()=>{ if(child!=p) return; int code=p.ExitCode; bool expected=stopping;
    if(job!=null) {job.Dispose();job=null;} child=null;p.Dispose(); start.IsEnabled=true;stop.IsEnabled=false; save.Content="Save settings";
    State(expected?"Stopped":"Receiver exited",expected?"Click Start receiver to connect again.":"Exit code "+code+". Expand Details and logs for more information."); });
   p.Start(); try {job=new ChildJob(p);} catch {p.Kill();throw;}
   p.BeginOutputReadLine();p.BeginErrorReadLine();start.IsEnabled=false;stop.IsEnabled=true;save.Content="Save and restart";
   State("Starting…","Preparing playback and local network discovery.");feedback.Text="Settings saved.";
  } catch(Exception e) { if(child!=null) {child.Dispose();child=null;} MessageBox.Show(window,e.Message,"Unable to start",MessageBoxButton.OK,MessageBoxImage.Warning); }
 }
 void Stop() { if(child==null || stopping) return; stopping=true;stop.IsEnabled=false;
  State("Stopping…","Closing the receiver and its player processes.");
  if(job!=null) {job.Dispose();job=null;}
 }
 async void Save() { save.IsEnabled=false; try { Read().Save(Config); bool restart=child!=null;
   if(restart) {Stop(); while(child!=null) await Task.Delay(100); Start();} else feedback.Text="Settings saved. They will apply on the next start.";
  } catch(Exception e) {MessageBox.Show(window,e.Message,"Check settings",MessageBoxButton.OK,MessageBoxImage.Warning);} finally {save.IsEnabled=true;} }
 void Fill(Settings s) {name.Text=s.Name;width.Text=s.Width.ToString();height.Text=s.Height.ToString();hls.IsChecked=s.Hls;
  fps.SelectedIndex=s.Fps==24?0:s.Fps==30?1:2;
  var presets=new[]{"1280x720","1920x1080","2560x1440","3840x2160"}; int index=Array.IndexOf(presets,s.Width+"x"+s.Height); resolution.SelectedIndex=index<0?4:index;
 }
 void Run(bool autoStart,bool uiTest=false) {
  Directory.CreateDirectory(Data);
  using(var stream=File.OpenRead(Path.Combine(Root,"desktop-app","MainWindow.xaml"))) window=(Window)XamlReader.Load(stream);
  window.Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(Path.Combine(Root,"desktop-app","assets","airplay.ico")));
  name=Find<TextBox>("DeviceName");width=Find<TextBox>("VideoWidth");height=Find<TextBox>("VideoHeight");log=Find<TextBox>("Log");resolution=Find<ComboBox>("Resolution");fps=Find<ComboBox>("Fps");hls=Find<CheckBox>("Hls");start=Find<Button>("Start");stop=Find<Button>("Stop");save=Find<Button>("Save");status=Find<TextBlock>("Status");detail=Find<TextBlock>("StatusDetail");feedback=Find<TextBlock>("Feedback");
  try {
   if(File.Exists(Config)) Fill(Settings.Load(Config));
   else {var initial=Settings.Load(Path.Combine(Root,"application.properties"));initial.Name=new Settings().Name;Fill(initial);}
  } catch(Exception e) {Fill(new Settings());feedback.Text="Unable to read settings. Showing defaults: "+e.Message;}
  resolution.SelectionChanged+=(s,e)=>{int i=resolution.SelectedIndex;if(i>=0 && i<4) {width.Text=new[]{1280,1920,2560,3840}[i].ToString();height.Text=new[]{720,1080,1440,2160}[i].ToString();}};
  width.TextChanged+=(s,e)=>{if(width.IsKeyboardFocusWithin)resolution.SelectedIndex=4;};height.TextChanged+=(s,e)=>{if(height.IsKeyboardFocusWithin)resolution.SelectedIndex=4;};
  start.Click+=(s,e)=>Start();stop.Click+=(s,e)=>Stop();save.Click+=(s,e)=>Save();
  Find<Button>("OpenLogs").Click+=(s,e)=>{Directory.CreateDirectory(Path.Combine(Data,"logs"));Process.Start("explorer.exe",Quote(Path.Combine(Data,"logs")));};
  string themePath=Path.Combine(Data,"ui-theme.txt");
  try {themeMode=ReadTheme(themePath);} catch(IOException) {themeMode=0;} catch(UnauthorizedAccessException) {themeMode=0;}
  Find<Button>("Theme").Click+=(s,e)=>{themeMode=NextTheme(themeMode);ApplyTheme();try {File.WriteAllText(themePath,themeMode.ToString());} catch(IOException) {feedback.Text="Theme changed, but the preference could not be saved.";} catch(UnauthorizedAccessException) {feedback.Text="Theme changed, but the preference could not be saved.";}};
  window.PreviewKeyDown+=(s,e)=>{var help=Find<System.Windows.Controls.Primitives.Popup>("HlsHelp");if(e.Key==System.Windows.Input.Key.Escape && help.IsOpen){help.IsOpen=false;e.Handled=true;}};
  Find<System.Windows.Documents.Hyperlink>("GitHub").RequestNavigate+=(s,e)=>{Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri){UseShellExecute=true});e.Handled=true;};
  ApplyTheme();
  Find<TextBlock>("EnvironmentInfo").Text="Playback: Microsoft Edge / Chrome (opens when connected)\nSettings and logs: "+Data;
  window.SourceInitialized+=(s,e)=>{int round=2;DwmSetWindowAttribute(new WindowInteropHelper(window).Handle,33,ref round,4);ApplyTheme();};
  window.Closing+=(s,e)=>{closing=true;if(job!=null)job.Dispose();};
  var timer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromSeconds(2)};
  timer.Tick+=(s,e)=>{if(themeMode==0 && darkTheme!=SystemIsDark())ApplyTheme();if(child!=null && !(ready && mseReady && registered) && !stopping && !failed && DateTime.UtcNow-started>TimeSpan.FromSeconds(35)) State("Taking longer than expected","Expand Details and logs. You can stop the receiver and try again.");};timer.Start();
  if(uiTest) {VerifyUi();return;}
  if(autoStart) window.ContentRendered+=(s,e)=>Start();
  new Application().Run(window);
 }
 void VerifyUi() {
  string output=Path.Combine(Root,"work","ui-check");Directory.CreateDirectory(output);
  window.Show();name.Text="AirPlay Receiver";
  try {
   foreach(int mode in new[]{1,2,0}) {
    themeMode=mode;ApplyTheme();
    foreach(int index in new[]{1,4,0}) {
     resolution.SelectedIndex=index;
     window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>{}));window.UpdateLayout();
     var panel=Find<StackPanel>("CustomResolution");
     if((panel.Visibility==Visibility.Visible)!=(index==4))throw new Exception("Custom resolution visibility check failed");
     if(index==0 && (width.Text!="1280" || height.Text!="720"))throw new Exception("Resolution preset check failed");
     if(index==1 && (width.Text!="1920" || height.Text!="1080"))throw new Exception("Resolution preset check failed");
    }
    resolution.SelectedIndex=mode==2?4:1;window.UpdateLayout();
    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(window);
    var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
    using(var stream=File.Create(Path.Combine(output,new[]{"system","light","dark"}[mode]+".png")))encoder.Save(stream);
    var info=Find<System.Windows.Controls.Primitives.ToggleButton>("HlsInfo");var help=Find<System.Windows.Controls.Primitives.Popup>("HlsHelp");
    bool hlsBefore=hls.IsChecked==true;info.IsChecked=true;
    window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>{}));window.UpdateLayout();
    if(!help.IsOpen || (hls.IsChecked==true)!=hlsBefore)throw new Exception("HLS info popup check failed");
    var content=(FrameworkElement)help.Child;content.UpdateLayout();
    var helpBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);helpBitmap.Render(content);
    var helpEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();helpEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(helpBitmap));
    using(var stream=File.Create(Path.Combine(output,"hls-"+new[]{"system","light","dark"}[mode]+".png")))helpEncoder.Save(stream);
    info.IsChecked=false;window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>{}));if(help.IsOpen)throw new Exception("HLS info dismiss check failed");
   }
   var link=Find<System.Windows.Documents.Hyperlink>("GitHub");if(link.NavigateUri.AbsoluteUri!="https://github.com/laosun124/airplay-control-center")throw new Exception("Repository link check failed");
   File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: light/dark/system themes, preset/custom visibility, preset dimensions, repository link, HLS help open/dismiss without changing playback, and rendered UI snapshots.");
  } finally {window.Close();}
 }
 static int SelfTest() {
  Directory.CreateDirectory(Path.Combine(Data,"logs"));
  string report=Path.Combine(Data,"logs","self-test.txt");
  string dir=Path.Combine(Path.GetTempPath(),"airplay-check-"+Guid.NewGuid());Directory.CreateDirectory(dir);
  try {
   if(Settings.Load(Path.Combine(dir,"missing.properties")).Name!="AirPlay Receiver") throw new Exception("Default device name check failed");
   if(NextTheme(0)!=1 || NextTheme(1)!=2 || NextTheme(2)!=0)throw new Exception("Theme cycle check failed");
   string themeFile=Path.Combine(dir,"theme.txt");File.WriteAllText(themeFile,"2");if(ReadTheme(themeFile)!=2)throw new Exception("Theme preference check failed");File.WriteAllText(themeFile,"invalid");if(ReadTheme(themeFile)!=0)throw new Exception("Theme fallback check failed");
   if(JavaMajor("openjdk version \"21.0.13\"")!=21 || JavaMajor("java version \"25.0.1\"")!=25 || JavaMajor("openjdk 17.0.1")!=17 || JavaMajor("invalid")!=0) throw new Exception("Java version parsing failed");
   string path=Path.Combine(dir,"application.properties");var s=new Settings {Name="\u6d4b\u8bd5 AirPlay\\\u8bbe\u5907"};s.Save(path);var t=Settings.Load(path);
   if(t.Name!=s.Name || t.Width!=1920 || t.Fps!=60) throw new Exception("Configuration round trip failed");
   t.Fps=30;t.Save(path);if(Settings.Load(path).Fps!=30)throw new Exception("Atomic update failed");
   t.Width=1;bool invalid=false;try{t.Validate();}catch{invalid=true;}if(!invalid)throw new Exception("Validation failed");
   Engine(dir);File.WriteAllText(report,"PASS: neutral default name, Unicode settings round trip, atomic save, validation, installed Java version parsing and Java/JAR/browser discovery.");return 0;
  } catch(Exception e){File.WriteAllText(report,e.ToString());return 1;}
  finally{Directory.Delete(dir,true);}
 }
 [STAThread] static int Main(string[] args) {
  Thread.CurrentThread.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("en-US");
  Thread.CurrentThread.CurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo("en-US");
  if(args.Contains("--self-test"))return SelfTest();
  bool created; using(var mutex=new Mutex(true,"Local\\AirPlayControl",out created)) {
   if(!created){MessageBox.Show("AirPlay Control Center is already open. Switch to it from the taskbar.");return 0;}
   try{new AirPlayControl().Run(args.Contains("--start"),args.Contains("--ui-test"));return 0;}catch(Exception e){if(args.Contains("--ui-test")){Directory.CreateDirectory(Path.Combine(Root,"work"));File.WriteAllText(Path.Combine(Root,"work","ui-error.txt"),e.ToString());}else MessageBox.Show(e.ToString(),"AirPlay Control Center");return 1;}
  }
 }
}
