using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace UnrealVR {
    class UnrealVRSharedMemory {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct Data {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string path;

            [MarshalAs(UnmanagedType.I4)]
            public int pid;
        };

        public static string SharedMemoryName = "UnrealVRMod";

        public static bool Exists() {
            // Try to open the shared memory with read/write access
            try {
                using (MemoryMappedFile memory = MemoryMappedFile.OpenExisting(SharedMemoryName, MemoryMappedFileRights.ReadWrite)) {
                    return true;
                }
            } catch (Exception) {
            }

            return false;
        }

        public static MemoryMappedFile? GetMapping() {
            // Try to open the shared memory with read/write access
            try {
                return MemoryMappedFile.OpenExisting(SharedMemoryName, MemoryMappedFileRights.ReadWrite);
            } catch (Exception) {
            }

            return null;
        }
    };
}
