using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Diagnostics;
using System.Security.Policy;
using System.Windows.Threading;
using System.Reflection;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows.Markup;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;

using Microsoft.Extensions.Configuration.Ini;
using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using static UEVR.SharedMemory;
using System.Threading.Channels;
using System.Security.Principal;

namespace UEVR {
    class GameSettingEntry : INotifyPropertyChanged {
        private string _key = "";
        private string _value = "";
        private string _tooltip = "";

        public string Key { get => _key; set => SetProperty(ref _key, value); }
        public string Value { 
            get => _value; 
            set { 
                SetProperty(ref _value, value); 
                OnPropertyChanged(nameof(ValueAsBool)); 
            } 
        }

        public string Tooltip { get => _tooltip; set => SetProperty(ref _tooltip, value); }

        public int KeyAsInt { get { return Int32.Parse(Key); } set { Key = value.ToString(); } }
        public bool ValueAsBool { 
            get => Boolean.Parse(Value);
            set { 
                Value = value.ToString().ToLower();
            } 
        }

        public Dictionary<string, string> ComboValues { get; set; } = new Dictionary<string, string>();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null) {
            if (Equals(storage, value)) return false;
            if (propertyName == null) return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    };

    enum RenderingMethod {
        [Description("Native Stereo")]
        NativeStereo = 0,
        [Description("Synced Sequential")]
        SyncedSequential = 1,
        [Description("Alternating/AFR")]
        Alternating = 2
    };

    enum SyncedSequentialMethods {
        SkipTick = 0,
        SkipDraw = 1,
    };

    class ComboMapping {

        public static Dictionary<string, string> RenderingMethodValues = new Dictionary<string, string>(){
            {"0", "Native Stereo" },
            {"1", "Synced Sequential" },
            {"2", "Alternating/AFR" }
        };

        public static Dictionary<string, string> SyncedSequentialMethodValues = new Dictionary<string, string>(){
            {"0", "Skip Tick" },
            {"1", "Skip Draw" },
        };

        public static Dictionary<string, Dictionary<string, string>> KeyEnums = new Dictionary<string, Dictionary<string, string>>() {
            { "VR_RenderingMethod", RenderingMethodValues },
            { "VR_SyncedSequentialMethod", SyncedSequentialMethodValues },
        };
    };

    class MandatoryConfig {
        public static Dictionary<string, string> Entries = new Dictionary<string, string>() {
            { "VR_RenderingMethod", ((int)RenderingMethod.NativeStereo).ToString() },
            { "VR_SyncedSequentialMethod", ((int)SyncedSequentialMethods.SkipDraw).ToString() },
            { "VR_UncapFramerate", "true" },
            { "VR_Compatibility_SkipPostInitProperties", "false" }
        };
    };

    class GameSettingTooltips {
        public static string VR_RenderingMethod =
        "Native Stereo: The default, most performant, and best looking rendering method (when it works). Runs through the native UE stereo pipeline. Can cause rendering bugs or crashes on some games.\n" +
        "Synced Sequential: A form of AFR. Can fix many rendering bugs. It is fully synchronized with none of the usual AFR artifacts. Causes TAA/temporal effect ghosting.\n" +
        "Alternating/AFR: The most basic form of AFR with all of the usual desync/artifacts. Should generally not be used unless the other two are causing issues.";

        public static string VR_SyncedSequentialMethod =
        "Requires \"Synced Sequential\" rendering to be enabled.\n" +
        "Skip Tick: Skips the engine tick on the next frame. Usually works well but sometimes causes issues.\n" +
        "Skip Draw: Skips the viewport draw on the next frame. Works with least issues but particle effects can play slower in some cases.\n";

        public static Dictionary<string, string> Entries = new Dictionary<string, string>() {
            { "VR_RenderingMethod", VR_RenderingMethod },
            { "VR_SyncedSequentialMethod", VR_SyncedSequentialMethod },
        };
    }

    public class ValueTemplateSelector : DataTemplateSelector {
        public DataTemplate? ComboBoxTemplate { get; set; }
        public DataTemplate? TextBoxTemplate { get; set; }
        public DataTemplate? CheckboxTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container) {
            var keyValuePair = (GameSettingEntry)item;
            if (ComboMapping.KeyEnums.ContainsKey(keyValuePair.Key)) {
                return ComboBoxTemplate;
            } else if (keyValuePair.Value.ToLower().Contains("true") || keyValuePair.Value.ToLower().Contains("false")) {
                return CheckboxTemplate;
            } else {
                return TextBoxTemplate;
            }
        }
    }

    public partial class MainWindow : Window {
        // variables
        // process list
        private List<Process> m_processList = new List<Process>();
        private MainWindowSettings m_mainWindowSettings = new MainWindowSettings();

        private string m_lastSelectedProcessName = new string("");
        private int m_lastSelectedProcessId = 0;

        private SharedMemory.Data? m_lastSharedData = null;
        private bool m_connected = false;

        private DispatcherTimer m_updateTimer = new DispatcherTimer {
            Interval = new TimeSpan(0, 0, 1)
        };

        private IConfiguration? m_currentConfig = null;
        private string? m_currentConfigPath = null;

        private ExecutableFilter m_executableFilter = new ExecutableFilter();
        private string? m_commandLineAttachExe = null;
        private bool m_ignoreFutureVDWarnings = false;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        private string excludedProcessesFile = "excluded.txt";

        public MainWindow() {
            InitializeComponent();

            // Grab the command-line arguments
            string[] args = Environment.GetCommandLineArgs();

            // Parse and handle arguments
            foreach (string arg in args) {
                if (arg.StartsWith("--attach=")) {
                    m_commandLineAttachExe = arg.Split('=')[1];
                }
            }
        }

        public static bool IsAdministrator() {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
            if (!IsAdministrator()) {
                m_nNotificationsGroupBox.Visibility = Visibility.Visible;
                m_restartAsAdminButton.Visibility = Visibility.Visible;
                m_adminExplanation.Visibility = Visibility.Visible;
            }

            FillProcessList();
            m_openvrRadio.IsChecked = m_mainWindowSettings.OpenVRRadio;
            m_openxrRadio.IsChecked = m_mainWindowSettings.OpenXRRadio;
            m_nullifyVRPluginsCheckbox.IsChecked = m_mainWindowSettings.NullifyVRPluginsCheckbox;
            m_ignoreFutureVDWarnings = m_mainWindowSettings.IgnoreFutureVDWarnings;
            m_focusGameOnInjectionCheckbox.IsChecked = m_mainWindowSettings.FocusGameOnInjection;

            m_updateTimer.Tick += (sender, e) => Dispatcher.Invoke(MainWindow_Update);
            m_updateTimer.Start();
        }

        private static bool IsExecutableRunning(string executableName) {
            return Process.GetProcesses().Any(p => p.ProcessName.Equals(executableName, StringComparison.OrdinalIgnoreCase));
        }

        private void RestartAsAdminButton_Click(object sender, RoutedEventArgs e) {
            // Get the path of the current executable
            var mainModule = Process.GetCurrentProcess().MainModule;
            if (mainModule == null) {
                return;
            }

            var exePath = mainModule.FileName;
            if (exePath == null) {
                return;
            }

            // Create a new process with administrator privileges
            var processInfo = new ProcessStartInfo {
                FileName = exePath,
                Verb = "runas",
                UseShellExecute = true,
            };

            try {
                // Attempt to start the process
                Process.Start(processInfo);
            } catch (Win32Exception ex) {
                // Handle the case when the user cancels the UAC prompt or there's an error
                MessageBox.Show($"Error: {ex.Message}\n\nThe application will continue running without administrator privileges.", "Failed to Restart as Admin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Close the current application instance
            Application.Current.Shutdown();
        }

        private DateTime m_lastAutoInjectTime = DateTime.MinValue;

        private void Update_InjectStatus() {
            if (m_connected) {
                m_injectButton.Content = "Terminate Connected Process";
                return;
            }

            DateTime now = DateTime.Now;
            TimeSpan oneSecond = TimeSpan.FromSeconds(1);

            if (m_commandLineAttachExe == null) {
                if (m_lastSelectedProcessId == 0) {
                    m_injectButton.Content = "Inject";
                    return;
                }

                try {
                    var verifyProcess = Process.GetProcessById(m_lastSelectedProcessId);

                    if (verifyProcess == null || verifyProcess.HasExited || verifyProcess.ProcessName != m_lastSelectedProcessName) {
                        var processes = Process.GetProcessesByName(m_lastSelectedProcessName);

                        if (processes == null || processes.Length == 0 || !AnyInjectableProcesses(processes)) {
                            m_injectButton.Content = "Waiting for Process";
                            return;
                        }
                    }

                    m_injectButton.Content = "Inject";
                } catch (ArgumentException) {
                    var processes = Process.GetProcessesByName(m_lastSelectedProcessName);

                    if (processes == null || processes.Length == 0 || !AnyInjectableProcesses(processes)) {
                        m_injectButton.Content = "Waiting for Process";
                        return;
                    }

                    m_injectButton.Content = "Inject";
                }
            } else {
                m_injectButton.Content = "Waiting for " + m_commandLineAttachExe.ToLower() + "...";

                var processes = Process.GetProcessesByName(m_commandLineAttachExe.ToLower().Replace(".exe", ""));

                if (processes.Count() == 0) {
                    return;
                }

                Process? process = null;

                foreach (Process p in processes) {
                    if (IsInjectableProcess(p)) {
                        m_lastSelectedProcessId = p.Id;
                        m_lastSelectedProcessName = p.ProcessName;
                        process = p;
                    }
                }

                if (process == null) {
                    return;
                }

                if (now - m_lastAutoInjectTime > oneSecond) {
                    if (m_nullifyVRPluginsCheckbox.IsChecked == true) {
                        IntPtr nullifierBase;
                        if (Injector.InjectDll(process.Id, "UEVRPluginNullifier.dll", out nullifierBase) && nullifierBase.ToInt64() > 0) {
                            if (!Injector.CallFunctionNoArgs(process.Id, "UEVRPluginNullifier.dll", nullifierBase, "nullify", true)) {
                                //MessageBox.Show("Failed to nullify VR plugins.");
                            }
                        } else {
                            //MessageBox.Show("Failed to inject plugin nullifier.");
                        }
                    }

                    string runtimeName;

                    if (m_openvrRadio.IsChecked == true) {
                        runtimeName = "openvr_api.dll";
                    } else if (m_openxrRadio.IsChecked == true) {
                        runtimeName = "openxr_loader.dll";
                    } else {
                        runtimeName = "openvr_api.dll";
                    }

                    if (Injector.InjectDll(process.Id, runtimeName)) {
                        InitializeConfig(process.ProcessName);

                        try {
                            if (m_currentConfig != null) {
                                if (m_currentConfig["Frontend_RequestedRuntime"] != runtimeName) {
                                    m_currentConfig["Frontend_RequestedRuntime"] = runtimeName;
                                    RefreshConfigUI();
                                    SaveCurrentConfig();
                                }
                            }
                        } catch (Exception) {

                        }

                        Injector.InjectDll(process.Id, "UEVRBackend.dll");
                    }

                    m_lastAutoInjectTime = now;
                    m_commandLineAttachExe = null; // no need anymore.
                    FillProcessList();
                    if (m_focusGameOnInjectionCheckbox.IsChecked == true)
                    {
                        SwitchToThisWindow(process.MainWindowHandle, true);
                    }
                }
            }
        }

        private void Hide_ConnectionOptions() {
            m_openGameDirectoryBtn.Visibility = Visibility.Collapsed;
        }

        private void Show_ConnectionOptions() {
            m_openGameDirectoryBtn.Visibility = Visibility.Visible;
        }

        private DateTime lastInjectorStatusUpdate = DateTime.MinValue;
        private DateTime lastFrontendSignal = DateTime.MinValue;

        private void Update_InjectorConnectionStatus() {
            var data = SharedMemory.GetData();
            DateTime now = DateTime.Now;
            TimeSpan oneSecond = TimeSpan.FromSeconds(1);

            if (data != null) {
                m_connectionStatus.Text = UEVRConnectionStatus.Connected;
                m_connectionStatus.Text += ": " + data?.path;
                m_connectionStatus.Text += "\nThread ID: " + data?.mainThreadId.ToString();
                m_lastSharedData = data;
                m_connected = true;
                Show_ConnectionOptions();

                if (data?.signalFrontendConfigSetup == true && (now - lastFrontendSignal > oneSecond)) {
                    SharedMemory.SendCommand(SharedMemory.Command.ConfigSetupAcknowledged);
                    RefreshCurrentConfig();

                    lastFrontendSignal = now;
                }
            } else {
                if (m_connected && !string.IsNullOrEmpty(m_commandLineAttachExe))
                {
                    // If we launched with an attached game exe, we shut ourselves down once that game closes.
                    Application.Current.Shutdown();
                    return;
                }
                
                m_connectionStatus.Text = UEVRConnectionStatus.NoInstanceDetected;
                m_connected = false;
                Hide_ConnectionOptions();
            }

            lastInjectorStatusUpdate = now;
        }

        private string GetGlobalDir() {
            string directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            directory += "\\UnrealVRMod";

            if (!System.IO.Directory.Exists(directory)) {
                System.IO.Directory.CreateDirectory(directory);
            }

            return directory;
        }

        private string GetGlobalGameDir(string gameName) {
            string directory = GetGlobalDir() + "\\" + gameName;

            if (!System.IO.Directory.Exists(directory)) {
                System.IO.Directory.CreateDirectory(directory);
            }

            return directory;
        }

        private void NavigateToDirectory(string directory) {
            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string explorerPath = System.IO.Path.Combine(windowsDirectory, "explorer.exe");
            Process.Start(explorerPath, "\"" + directory + "\"");
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }

        private string GetGlobalDirPath() {
            string directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            directory += "\\UnrealVRMod";
            return directory;
        }

        private void OpenGlobalDir_Clicked(object sender, RoutedEventArgs e) {
            string directory = GetGlobalDirPath();

            if (!System.IO.Directory.Exists(directory)) {
                System.IO.Directory.CreateDirectory(directory);
            }

            NavigateToDirectory(directory);
        }

        private void OpenGameDir_Clicked(object sender, RoutedEventArgs e) {
            if (m_lastSharedData == null) {
                return;
            }

            var directory = System.IO.Path.GetDirectoryName(m_lastSharedData?.path);
            if (directory == null) {
                return;
            }

            NavigateToDirectory(directory);
        }
        private void ExportConfig_Clicked(object sender, RoutedEventArgs e) {
            if (!m_connected) {
                MessageBox.Show("Inject into a game first!");
                return;
            }

            if (m_lastSharedData == null) {
                MessageBox.Show("No game connection detected.");
                return;
            }

            var dir = GetGlobalGameDir(m_lastSelectedProcessName);
            if (dir == null) {
                return;
            }

            if (!Directory.Exists(dir)) {
                MessageBox.Show("Directory does not exist.");
                return;
            }

            var exportedConfigsDir = GetGlobalDirPath() + "\\ExportedConfigs";

            if (!Directory.Exists(exportedConfigsDir)) {
                Directory.CreateDirectory(exportedConfigsDir);
            }

            GameConfig.CreateZipFromDirectory(dir, exportedConfigsDir + "\\" + m_lastSelectedProcessName + ".zip");
            NavigateToDirectory(exportedConfigsDir);
        }

        private void ImportConfig_Clicked(object sender, RoutedEventArgs e) {
            var importPath = GameConfig.BrowseForImport(GetGlobalDirPath());

            if (importPath == null) {
                return;
            }

            var gameName = System.IO.Path.GetFileNameWithoutExtension(importPath);
            if (gameName == null) {
                MessageBox.Show("Invalid filename");
                return;
            }

            var globalDir = GetGlobalDirPath();
            var gameGlobalDir = globalDir + "\\" + gameName;

            try {
                if (!Directory.Exists(gameGlobalDir)) {
                    Directory.CreateDirectory(gameGlobalDir);
                }

                var finalGameName = GameConfig.ExtractZipToDirectory(importPath, gameGlobalDir, gameName);

                if (finalGameName == null) {
                    MessageBox.Show("Failed to extract the ZIP file.");
                    return;
                }

                var finalDirectory = System.IO.Path.Combine(globalDir, finalGameName);
                NavigateToDirectory(finalDirectory);

                RefreshCurrentConfig();


                if (m_connected) {
                    SharedMemory.SendCommand(SharedMemory.Command.ReloadConfig);
                }
            } catch (Exception ex) {
                MessageBox.Show("An error occurred: " + ex.Message);
            }
        }

        private bool m_virtualDesktopWarned = false;
        private bool m_virtualDesktopChecked = false;
        private void Check_VirtualDesktop() {
            if (m_virtualDesktopWarned || m_ignoreFutureVDWarnings) {
                return;
            }

            if (IsExecutableRunning("VirtualDesktop.Streamer")) {
                m_virtualDesktopWarned = true;
                var dialog = new VDWarnDialog();
                dialog.ShowDialog();

                if (dialog.DialogResultOK) {
                    if (dialog.HideFutureWarnings) {
                        m_ignoreFutureVDWarnings = true;
                    }
                }
            }
        }

        private void MainWindow_Update() {
            Update_InjectorConnectionStatus();
            Update_InjectStatus();

            if (m_virtualDesktopChecked == false) {
                m_virtualDesktopChecked = true;
                Check_VirtualDesktop();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            m_mainWindowSettings.OpenXRRadio = m_openxrRadio.IsChecked == true;
            m_mainWindowSettings.OpenVRRadio = m_openvrRadio.IsChecked == true;
            m_mainWindowSettings.NullifyVRPluginsCheckbox = m_nullifyVRPluginsCheckbox.IsChecked == true;
            m_mainWindowSettings.IgnoreFutureVDWarnings = m_ignoreFutureVDWarnings;
            m_mainWindowSettings.FocusGameOnInjection = m_focusGameOnInjectionCheckbox.IsChecked == true;

            m_mainWindowSettings.Save();
        }

        private string m_lastDisplayedWarningProcess = "";
        private string[] m_discouragedPlugins = {
            "OpenVR",
            "OpenXR",
            "Oculus"
        };

        private string? AreVRPluginsPresent_InEngineDir(string enginePath) {
            string pluginsPath = enginePath + "\\Binaries\\ThirdParty";

            if (!Directory.Exists(pluginsPath)) {
                return null;
            }

            foreach (string discouragedPlugin in m_discouragedPlugins) {
                string pluginPath = pluginsPath + "\\" + discouragedPlugin;

                if (Directory.Exists(pluginPath)) {
                    return pluginsPath;
                }
            }

            return null;
        }

        private string? AreVRPluginsPresent(string gameDirectory) {
            try {
                var parentPath = gameDirectory;

                for (int i = 0; i < 10; ++i) {
                    parentPath = System.IO.Path.GetDirectoryName(parentPath);

                    if (parentPath == null) {
                        return null;
                    }

                    if (Directory.Exists(parentPath + "\\Engine")) {
                        return AreVRPluginsPresent_InEngineDir(parentPath + "\\Engine");
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"Exception caught: {ex}");
            }

            return null;
        }

        private bool IsUnrealEngineGame(string gameDirectory, string targetName) {
            try {
                if (targetName.ToLower().EndsWith("-win64-shipping")) {
                    return true;
                }

                if (targetName.ToLower().EndsWith("-wingdk-shipping")) {
                    return true;
                }

                // Check if going up the parent directories reveals the directory "\Engine\Binaries\ThirdParty".
                var parentPath = gameDirectory;
                for (int i = 0; i < 10; ++i) {  // Limit the number of directories to move up to prevent endless loops.
                    if (parentPath == null) {
                        return false;
                    }

                    if (Directory.Exists(parentPath + "\\Engine\\Binaries\\ThirdParty")) {
                        return true;
                    }

                    if (Directory.Exists(parentPath + "\\Engine\\Binaries\\Win64")) {
                        return true;
                    }

                    parentPath = System.IO.Path.GetDirectoryName(parentPath);
                }
            } catch (Exception ex) {
                Console.WriteLine($"Exception caught: {ex}");
            }

            return false;
        }

        private string IniToString(IConfiguration config) {
            string result = "";

            foreach (var kv in config.AsEnumerable()) {
                result += kv.Key + "=" + kv.Value + "\n";
            }

            return result;
        }

        private void SaveCurrentConfig() {
            try {
                if (m_currentConfig == null || m_currentConfigPath == null) {
                    return;
                }

                var iniStr = IniToString(m_currentConfig);
                Debug.Print(iniStr);

                File.WriteAllText(m_currentConfigPath, iniStr);

                if (m_connected) {
                    SharedMemory.SendCommand(SharedMemory.Command.ReloadConfig);
                }
            } catch(Exception ex) {
                MessageBox.Show(ex.ToString());
            }
        }

        private void TextChanged_Value(object sender, RoutedEventArgs e) {
            try {
                if (m_currentConfig == null || m_currentConfigPath == null) {
                    return;
                }

                var textBox = (TextBox)sender;
                var keyValuePair = (GameSettingEntry)textBox.DataContext;

                // For some reason the TextBox.text is updated but thne keyValuePair.Value isn't at this point.
                bool changed = m_currentConfig[keyValuePair.Key] != textBox.Text || keyValuePair.Value != textBox.Text;
                var newValue = textBox.Text;

                if (changed) {
                    RefreshCurrentConfig();
                }

                m_currentConfig[keyValuePair.Key] = newValue;
                RefreshConfigUI();

                if (changed) {
                    SaveCurrentConfig();
                }
            } catch(Exception ex) { 
                Console.WriteLine(ex.ToString()); 
            }
        }

        private void ComboChanged_Value(object sender, RoutedEventArgs e) {
            try {
                if (m_currentConfig == null || m_currentConfigPath == null) {
                    return;
                }

                var comboBox = (ComboBox)sender;
                var keyValuePair = (GameSettingEntry)comboBox.DataContext;

                bool changed = m_currentConfig[keyValuePair.Key] != keyValuePair.Value;
                var newValue = keyValuePair.Value;

                if (changed) {
                    RefreshCurrentConfig();
                }

                m_currentConfig[keyValuePair.Key] = newValue;
                RefreshConfigUI();

                if (changed) {
                    SaveCurrentConfig();
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        private void CheckChanged_Value(object sender, RoutedEventArgs e) {
            try {
                if (m_currentConfig == null || m_currentConfigPath == null) {
                    return;
                }

                var checkbox = (CheckBox)sender;
                var keyValuePair = (GameSettingEntry)checkbox.DataContext;

                bool changed = m_currentConfig[keyValuePair.Key] != keyValuePair.Value;
                string newValue = keyValuePair.Value;

                if (changed) {
                    RefreshCurrentConfig();
                }

                m_currentConfig[keyValuePair.Key] = newValue;
                RefreshConfigUI();

                if (changed) {
                    SaveCurrentConfig();
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        private void RefreshCurrentConfig() {
            if (m_currentConfig == null || m_currentConfigPath == null) {
                return;
            }

            InitializeConfig_FromPath(m_currentConfigPath);
        }

        private void RefreshConfigUI() {
            if (m_currentConfig == null) {
                return;
            }

            var vanillaList = m_currentConfig.AsEnumerable().ToList();
            vanillaList.Sort((a, b) => a.Key.CompareTo(b.Key));

            List<GameSettingEntry> newList = new List<GameSettingEntry>();

            foreach (var kv in vanillaList) {
                if (!string.IsNullOrEmpty(kv.Key) && !string.IsNullOrEmpty(kv.Value)) {
                    Dictionary<string, string> comboValues = new Dictionary<string, string>();
                    string tooltip = "";

                    if (ComboMapping.KeyEnums.ContainsKey(kv.Key)) {
                        var valueList = ComboMapping.KeyEnums[kv.Key];

                        if (valueList != null && valueList.ContainsKey(kv.Value)) {
                            comboValues = valueList;
                        }
                    }

                    if (GameSettingTooltips.Entries.ContainsKey(kv.Key)) {
                        tooltip = GameSettingTooltips.Entries[kv.Key];
                    }

                    newList.Add(new GameSettingEntry { Key = kv.Key, Value = kv.Value, ComboValues = comboValues, Tooltip = tooltip });
                }
            }

            if (m_iniListView.ItemsSource == null) {
                m_iniListView.ItemsSource = newList;
            } else {
                foreach (var kv in newList) {
                    var source = (List<GameSettingEntry>)m_iniListView.ItemsSource;

                    var elements = source.FindAll(el => el.Key == kv.Key);

                    if (elements.Count() == 0) {
                        // Just set the entire list, we don't care.
                        m_iniListView.ItemsSource = newList;
                        break;
                    } else {
                        elements[0].Value = kv.Value;
                        elements[0].ComboValues = kv.ComboValues;
                        elements[0].Tooltip = kv.Tooltip;
                    }
                }
            }

            m_iniListView.Visibility = Visibility.Visible;
        }

        private void InitializeConfig_FromPath(string configPath) {
            var builder = new ConfigurationBuilder().AddIniFile(configPath, optional: true, reloadOnChange: false);

            m_currentConfig = builder.Build();
            m_currentConfigPath = configPath;

            foreach (var entry in MandatoryConfig.Entries) {
                if (m_currentConfig.AsEnumerable().ToList().FindAll(v => v.Key == entry.Key).Count() == 0) {
                    m_currentConfig[entry.Key] = entry.Value;
                }
            }

            RefreshConfigUI();
        }

        private void InitializeConfig(string gameName) {
            var configDir = GetGlobalGameDir(gameName);
            var configPath = configDir + "\\config.txt";

            InitializeConfig_FromPath(configPath);
        }

        private bool m_isFirstProcessFill = true;

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            //ComboBoxItem comboBoxItem = ((sender as ComboBox).SelectedItem as ComboBoxItem);

            try {
                var box = (sender as ComboBox);
                if (box == null || box.SelectedIndex < 0 || box.SelectedIndex > m_processList.Count) {
                    return;
                }

                var p = m_processList[box.SelectedIndex];
                if (p == null || p.HasExited) {
                    return;
                }

                m_lastSelectedProcessName = p.ProcessName;
                m_lastSelectedProcessId = p.Id;

                // Search for the VR plugins inside the game directory
                // and warn the user if they exist.
                if (m_lastDisplayedWarningProcess != m_lastSelectedProcessName && p.MainModule != null) {
                    m_lastDisplayedWarningProcess = m_lastSelectedProcessName;

                    var gamePath = p.MainModule.FileName;
                    
                    if (gamePath != null) {
                        var gameDirectory = System.IO.Path.GetDirectoryName(gamePath);

                        if (gameDirectory != null) {
                            var pluginsDir = AreVRPluginsPresent(gameDirectory);

                            if (pluginsDir != null) {
                                MessageBox.Show("VR plugins have been detected in the game install directory.\n" +
                                                "You may want to delete or rename these as they will cause issues with the mod.\n" +
                                                "You may also want to pass -nohmd as a command-line option to the game. This can sometimes work without deleting anything.");
                                var result = MessageBox.Show("Do you want to open the plugins directory now?", "Confirmation", MessageBoxButton.YesNo);

                                switch (result) {
                                    case MessageBoxResult.Yes:
                                        NavigateToDirectory(pluginsDir);
                                        break;
                                    case MessageBoxResult.No:
                                        break;
                                };
                            }

                            Check_VirtualDesktop();

                            m_iniListView.ItemsSource = null; // Because we are switching processes.
                            InitializeConfig(p.ProcessName);

                            if (!IsUnrealEngineGame(gameDirectory, m_lastSelectedProcessName) && !m_isFirstProcessFill) {
                                MessageBox.Show("Warning: " + m_lastSelectedProcessName + " does not appear to be an Unreal Engine title");
                            }
                        }

                        m_lastDefaultProcessListName = GenerateProcessName(p);
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"Exception caught: {ex}");
            }
        }

        private void ComboBox_DropDownOpened(object sender, System.EventArgs e) {
            m_lastSelectedProcessName = "";
            m_lastSelectedProcessId = 0;

            FillProcessList();
            Update_InjectStatus();

            m_isFirstProcessFill = false;
        }

        private void Donate_Clicked(object sender, RoutedEventArgs e) {
            Process.Start(new ProcessStartInfo("https://patreon.com/praydog") { UseShellExecute = true });
        }

        private void Documentation_Clicked(object sender, RoutedEventArgs e) {
            Process.Start(new ProcessStartInfo("https://praydog.github.io/uevr-docs/") { UseShellExecute = true });
        }
        private void Discord_Clicked(object sender, RoutedEventArgs e) {
            Process.Start(new ProcessStartInfo("http://flat2vr.com") { UseShellExecute = true });
        }
        private void GitHub_Clicked(object sender, RoutedEventArgs e) {
            Process.Start(new ProcessStartInfo("https://github.com/praydog/UEVR") { UseShellExecute = true });
        }

        private void Inject_Clicked(object sender, RoutedEventArgs e) {
            // "Terminate Connected Process"
            if (m_connected) {
                try {
                    var pid = m_lastSharedData?.pid;

                    if (pid != null) {
                        var target = Process.GetProcessById((int)pid);
                        target.CloseMainWindow();
                        target.Kill();
                    }
                } catch(Exception) {

                }

                return;
            }

            var selectedProcessName = m_processListBox.SelectedItem;

            if (selectedProcessName == null) {
                return;
            }

            var index = m_processListBox.SelectedIndex;
            var process = m_processList[index];

            if (process == null) {
                return;
            }

            // Double check that the process we want to inject into exists
            // this can happen if the user presses inject again while
            // the previous combo entry is still selected but the old process
            // has died.
            try {
                var verifyProcess = Process.GetProcessById(m_lastSelectedProcessId);

                if (verifyProcess == null || verifyProcess.HasExited || verifyProcess.ProcessName != m_lastSelectedProcessName) {
                    var processes = Process.GetProcessesByName(m_lastSelectedProcessName);

                    if (processes == null || processes.Length == 0 || !AnyInjectableProcesses(processes)) {
                        return;
                    }

                    foreach (var candidate in processes) {
                        if (IsInjectableProcess(candidate)) {
                            process = candidate;
                            break;
                        }
                    }

                    m_processList[index] = process;
                    m_processListBox.Items[index] = GenerateProcessName(process);
                    m_processListBox.SelectedIndex = index;
                }
            } catch(Exception ex) {
                MessageBox.Show(ex.Message);
                return;
            }

            string runtimeName;

            if (m_openvrRadio.IsChecked == true) {
                runtimeName = "openvr_api.dll";
            } else if (m_openxrRadio.IsChecked == true) {
                runtimeName = "openxr_loader.dll";
            } else {
                runtimeName = "openvr_api.dll";
            }

            if (m_nullifyVRPluginsCheckbox.IsChecked == true) {
                IntPtr nullifierBase;
                if (Injector.InjectDll(process.Id, "UEVRPluginNullifier.dll", out nullifierBase) && nullifierBase.ToInt64() > 0) {
                    if (!Injector.CallFunctionNoArgs(process.Id, "UEVRPluginNullifier.dll", nullifierBase, "nullify", true)) {
                        MessageBox.Show("Failed to nullify VR plugins.");
                    }
                } else {
                    MessageBox.Show("Failed to inject plugin nullifier.");
                }
            }

            if (Injector.InjectDll(process.Id, runtimeName)) {
                try {
                    if (m_currentConfig != null) {
                        if (m_currentConfig["Frontend_RequestedRuntime"] != runtimeName) {
                            m_currentConfig["Frontend_RequestedRuntime"] = runtimeName;
                            RefreshConfigUI();
                            SaveCurrentConfig();
                        }
                    }
                } catch (Exception) {

                }

                Injector.InjectDll(process.Id, "UEVRBackend.dll");
            }

            if (m_focusGameOnInjectionCheckbox.IsChecked == true)
            {
                SwitchToThisWindow(process.MainWindowHandle, true);
            }
        }

        private string GenerateProcessName(Process p) {
            return p.ProcessName + " (pid: " + p.Id + ")" + " (" + p.MainWindowTitle + ")";
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool wow64Process);

        private bool IsInjectableProcess(Process process) {
        
            try {
                if (Environment.Is64BitOperatingSystem) {
                    try {
                        bool isWow64 = false;
                        if (IsWow64Process(process.Handle, out isWow64) && isWow64) {
                            return false;
                        }
                    } catch {
                        // If we threw an exception here, then the process probably can't be accessed anyways.
                        return false;
                    }
                }

                if (process.MainWindowTitle.Length == 0) {
                    return false;
                }

                if (process.Id == Process.GetCurrentProcess().Id) {
                    return false;
                }

                if (!m_executableFilter.IsValidExecutable(process.ProcessName.ToLower())) {
                    return false;
                }

                foreach (ProcessModule module in process.Modules) {
                    if (module.ModuleName == null) {
                        continue;
                    }

                    string moduleLow = module.ModuleName.ToLower();
                    if (moduleLow == "d3d11.dll" || moduleLow == "d3d12.dll") {
                        return true ;
                    }
                }

                // Check if the excluded processes file exists
                if (File.Exists(excludedProcessesFile)) {
                    
                    List<string> excludedProcesses = new List<string>();
    
                    // Read excluded process names from the text file
                    excludedProcesses = File.ReadAllLines(excludedProcessesFile).ToList();
    
                    // Check if the process name is in the excluded list
                    if (excludedProcesses.Contains(process.ProcessName)) {
                        return false;
                    }
                    
                }

                return false;
            } catch(Exception ex) {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        private bool AnyInjectableProcesses(Process[] processList) {
            foreach (Process process in processList) {
                if (IsInjectableProcess(process)) {
                    return true;
                }
            }

            return false;
        }
        private SemaphoreSlim m_processSemaphore = new SemaphoreSlim(1, 1); // create a semaphore with initial count of 1 and max count of 1
        private string? m_lastDefaultProcessListName = null;

        private async void FillProcessList() {
            // Allow the previous running FillProcessList task to finish first
            if (m_processSemaphore.CurrentCount == 0) {
                return;
            }

            await m_processSemaphore.WaitAsync();

            try {
                m_processList.Clear();
                m_processListBox.Items.Clear();

                await Task.Run(() => {
                    // get the list of processes
                    Process[] processList = Process.GetProcesses();

                    // loop through the list of processes
                    foreach (Process process in processList) {
                        if (!IsInjectableProcess(process)) {
                            continue;
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            m_processList.Add(process);
                            m_processList.Sort((a, b) => a.ProcessName.CompareTo(b.ProcessName));
                            m_processListBox.Items.Clear();

                            foreach (Process p in m_processList) {
                                string processName = GenerateProcessName(p);
                                m_processListBox.Items.Add(processName);

                                if (m_processListBox.SelectedItem == null && m_processListBox.Items.Count > 0) {
                                    if (m_lastDefaultProcessListName == null || m_lastDefaultProcessListName == processName) {
                                        m_processListBox.SelectedItem = m_processListBox.Items[m_processListBox.Items.Count - 1];
                                        m_lastDefaultProcessListName = processName;
                                    }
                                }
                            }
                        });
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        m_processListBox.Items.Clear();

                        foreach (Process process in m_processList) {
                            string processName = GenerateProcessName(process);
                            m_processListBox.Items.Add(processName);

                            if (m_processListBox.SelectedItem == null && m_processListBox.Items.Count > 0) {
                                if (m_lastDefaultProcessListName == null || m_lastDefaultProcessListName == processName) {
                                    m_processListBox.SelectedItem = m_processListBox.Items[m_processListBox.Items.Count - 1];
                                    m_lastDefaultProcessListName = processName;
                                }
                            }
                        }
                    });
                });
            } finally {
                m_processSemaphore.Release();
            }
        }
    }
}
