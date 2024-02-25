using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Windows;
using Microsoft.Win32;
using System.IO;

namespace UEVR {
    public class GameConfig {
        // https://stackoverflow.com/questions/19395128/c-sharp-zipfile-createfromdirectory-the-process-cannot-access-the-file-path-t
        public static void CreateZipFromDirectory(string sourceDirectoryName, string destinationArchiveFileName) {
            try {
                using (FileStream zipToOpen = new FileStream(destinationArchiveFileName, FileMode.Create))
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create)) {
                    DirectoryInfo di = new DirectoryInfo(sourceDirectoryName);
                    string basePath = di.FullName;

                    // Recursive method to process directories
                    ProcessDirectory(di, basePath, archive);
                }
            } catch (Exception ex) {
                MessageBox.Show("An error occurred: " + ex.Message);
            }
        }

        private static void ProcessDirectory(DirectoryInfo di, string basePath, ZipArchive archive) {
            // Process files in the directory
            foreach (FileInfo file in di.GetFiles()) {
                try {
                    string entryName = GetRelativePath(file.FullName, basePath);
                    var entry = archive.CreateEntry(entryName);
                    entry.LastWriteTime = file.LastWriteTime;

                    using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var stream = entry.Open()) {
                        fs.CopyTo(stream);
                    }
                } catch(Exception) {
                    continue;
                }
            }

            // Recursively process subdirectories
            foreach (DirectoryInfo subDi in di.GetDirectories()) {
                if (subDi.Name.Equals("plugins", StringComparison.OrdinalIgnoreCase) || subDi.Name.Equals("sdkdump", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                ProcessDirectory(subDi, basePath, archive);
            }
        }

        private static string GetRelativePath(string fullPath, string basePath) {
            // Ensure trailing backslash
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
                basePath += Path.DirectorySeparatorChar;
            }

            return fullPath.Substring(basePath.Length);
        }

        public static bool ZipContainsDLL(string sourceArchiveFileName) {
            try {
                using (ZipArchive archive = ZipFile.OpenRead(sourceArchiveFileName)) {
                    foreach (ZipArchiveEntry entry in archive.Entries) {
                        if (entry.FullName.ToLower().EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                            return true;
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            // No .DLL files found
            return false;
        }

        public static string? ExtractZipToDirectory(string sourceArchiveFileName, string destinationDirectoryName, string gameName) {
            try {
                string tempExtractionPath = Path.Combine(destinationDirectoryName, "temp_extraction");
                Directory.CreateDirectory(tempExtractionPath);

                ZipFile.ExtractToDirectory(sourceArchiveFileName, tempExtractionPath, overwriteFiles: true);

                var extractedEntries = Directory.GetFileSystemEntries(tempExtractionPath);
                if (extractedEntries.Length == 1) {
                    var singleEntry = extractedEntries[0];

                    // Check if the single entry is a zip file
                    if (File.Exists(singleEntry) && Path.GetExtension(singleEntry).Equals(".zip", StringComparison.OrdinalIgnoreCase)) {
                        string nestedZipName = Path.GetFileNameWithoutExtension(singleEntry);
                        string nestedDestination = Path.Combine(destinationDirectoryName, "..", nestedZipName);
                        Directory.CreateDirectory(nestedDestination);

                        ZipFile.ExtractToDirectory(singleEntry, nestedDestination, overwriteFiles: true);
                        File.Delete(singleEntry);

                        Directory.Delete(tempExtractionPath, true);
                        return nestedZipName;
                    }

                    // Check if the single entry is a directory with a matching name
                    if (Directory.Exists(singleEntry) && Path.GetFileName(singleEntry).Equals(gameName, StringComparison.OrdinalIgnoreCase)) {
                        MoveDirectoryContents(singleEntry, destinationDirectoryName);
                        Directory.Delete(tempExtractionPath, true);
                        return gameName;
                    }
                }

                // Move extracted files from temp directory to final destination
                MoveDirectoryContents(tempExtractionPath, destinationDirectoryName);
                Directory.Delete(tempExtractionPath, true);
                return gameName;
            } catch (Exception ex) {
                MessageBox.Show("An error occurred: " + ex.Message);
                return null;
            }
        }

        private static void MoveDirectoryContents(string sourceDir, string destinationDir) {
            foreach (var dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories)) {
                Directory.CreateDirectory(dirPath.Replace(sourceDir, destinationDir));
            }

            foreach (var newPath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)) {
                File.Copy(newPath, newPath.Replace(sourceDir, destinationDir), true);
            }
        }

        public static string? BrowseForImport(string? initialDirectory = null) {
            var openFileDialog = new OpenFileDialog {
                DefaultExt = ".zip",
                Filter = "Zip Files (*.zip)|*.zip",
                InitialDirectory = initialDirectory
            };

            bool? result = openFileDialog.ShowDialog();
            if (result == true) {
                return openFileDialog.FileName;
            }

            return null;
        }
    }
}