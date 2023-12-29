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
                if (subDi.Name.Equals("plugins", StringComparison.OrdinalIgnoreCase)) {
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

        public static string? ExtractZipToDirectory(string sourceArchiveFileName, string destinationDirectoryName, string gameName) {
            try {
                ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName, overwriteFiles: true);

                var extractedFiles = Directory.GetFiles(destinationDirectoryName);
                if (extractedFiles.Length == 1 && Path.GetExtension(extractedFiles[0]).Equals(".zip", StringComparison.OrdinalIgnoreCase)) {
                    string nestedZipFile = extractedFiles[0];
                    string nestedZipName = Path.GetFileNameWithoutExtension(nestedZipFile);

                    string nestedDestination = Path.Combine(destinationDirectoryName, "..", nestedZipName);
                    Directory.CreateDirectory(nestedDestination); // Ensure the directory is created

                    ZipFile.ExtractToDirectory(nestedZipFile, nestedDestination, overwriteFiles: true);
                    File.Delete(nestedZipFile); // Optionally delete the nested zip file after extraction

                    return nestedZipName; // Return the name of the nested zip
                }

                return gameName; // Return the original game name if no nested zip
            } catch (Exception ex) {
                MessageBox.Show("An error occurred: " + ex.Message);
                return null; // Return null in case of an error
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