using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;

class FocusCheck {
 static void Layout(Window window) {window.Dispatcher.Invoke(DispatcherPriority.Background,new Action(()=>{}));window.UpdateLayout();}
 [STAThread] static int Main(string[] args) {
  Window window;
  using(var stream=File.OpenRead(args[0])) window=(Window)XamlReader.Load(stream);
  try {
   window.Show();Layout(window);
   var input=(TextBox)window.FindName("DeviceName");
   foreach(string name in new[]{"Save","Start","Stop","Theme"}) {
    var button=(Button)window.FindName(name);button.IsEnabled=true;
    button.BringIntoView();Keyboard.Focus(input);Layout(window);
    Size normal=button.RenderSize;Point normalPosition=button.TranslatePoint(new Point(),window);
    Keyboard.Focus(button);Layout(window);
    if(!button.IsKeyboardFocused)throw new Exception(name+" did not receive keyboard focus");
    Size focused=button.RenderSize;Point focusedPosition=button.TranslatePoint(new Point(),window);
    Keyboard.Focus(input);Layout(window);
    Console.WriteLine(name+": normal="+normal+", focused="+focused+", blurred="+button.RenderSize);
    if(normal!=focused || normal!=button.RenderSize || normalPosition!=focusedPosition)throw new Exception(name+" changes size or position on focus");
   }
   Console.WriteLine("PASS: button dimensions and positions remain stable through focus and blur.");return 0;
  } catch(Exception error) {Console.WriteLine(error.Message);return 1;}
  finally {window.Close();}
 }
}
