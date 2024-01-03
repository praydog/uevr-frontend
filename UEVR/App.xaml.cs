using System;
using System.Collections.Generic;
using System.Windows;

namespace UEVR
{
    /// <summary>
    /// Parsed executable arguments to use executing any game process 
    /// </summary>
    internal class AppArguments
    {
        public string Executable { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string ProcessName { get; set; }
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static AppArguments GetAppArguments(string[] args)
        {
            if (args.Length == 0) {
                return null;
            }

            var result = new AppArguments();
            foreach (string arg in args)
            {
                // Dividir cada argumento en clave y valor
                string[] argSplitted = arg.Split('=');
                if (argSplitted.Length == 2)
                {
                    var parameter = argSplitted[0].Trim();
                    var value = argSplitted[1].Trim();

                    var property = result.GetType().GetProperty(parameter);
                    if (property != null)
                    {
                        property.SetValue(result, value, null);
                    }
                    else
                    {
                        throw new NotImplementedException($"Parameter '{parameter}' not found");
                    }
                }
                else
                {
                    throw new NotImplementedException($"Argument '{arg}' without the expected format");
                }
            }

            return result;
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            MainWindow wnd = new();
            wnd.Show();

            var arguments = GetAppArguments(e.Args);
            if (arguments != null)
            {
                wnd.AttachToProcess(arguments);
            }
        }
    }
}
