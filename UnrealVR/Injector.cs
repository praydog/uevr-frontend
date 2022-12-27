using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace UnrealVR {
    class Injector {
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        // Inject the DLL into the target process
        public static bool InjectDll(string dllPath, int processId) {
            string fullPath = Path.GetFullPath(dllPath);

            // Open the target process with the necessary access
            IntPtr processHandle = OpenProcess(0x1F0FFF, false, processId);

            if (processHandle == IntPtr.Zero) {
                MessageBox.Show("Could not open a handle to the target process.\nYou may need to start this program as an administrator, or the process may be protected.");
                return false;
            }

            // Get the address of the LoadLibrary function
            IntPtr loadLibraryAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            if (loadLibraryAddress == IntPtr.Zero) {
                MessageBox.Show("Could not obtain LoadLibraryA address in the target process.");
                return false;
            }

            // Allocate memory in the target process for the DLL path
            IntPtr dllPathAddress = VirtualAllocEx(processHandle, IntPtr.Zero, (uint)fullPath.Length, 0x1000, 0x40);

            if (dllPathAddress == IntPtr.Zero) {
                MessageBox.Show("Failed to allocate memory in the target process.");
                return false;
            }

            // Write the DLL path to the allocated memory
            int bytesWritten = 0;
            var bytes = Encoding.ASCII.GetBytes(fullPath);
            WriteProcessMemory(processHandle, dllPathAddress, bytes, (uint)fullPath.Length, out bytesWritten);

            // Create a remote thread in the target process that calls LoadLibrary with the DLL path
            IntPtr threadHandle = CreateRemoteThread(processHandle, IntPtr.Zero, 0, loadLibraryAddress, dllPathAddress, 0, IntPtr.Zero);

            if (threadHandle == IntPtr.Zero) {
                MessageBox.Show("Failed to create remote thread in the target processs.");
                return false;
            }

            return true;
        }
    }
}