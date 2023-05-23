using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Windows;
using Microsoft.Win32;
using System.IO;

namespace UnrealVR {
    public class GameConfig {
        // https://stackoverflow.com/questions/19395128/c-sharp-zipfile-createfromdirectory-the-process-cannot-access-the-file-path-t
        public static void CreateZipFromDirectory(string sourceDirectoryName, string destinationArchiveFileName) {
            try {
                using (FileStream zipToOpen = new FileStream(destinationArchiveFileName, FileMode.Create))
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create)) {
                    foreach (var file in Directory.GetFiles(sourceDirectoryName)) {
                        var entryName = Path.GetFileName(file);
                        var entry = archive.CreateEntry(entryName);
                        entry.LastWriteTime = File.GetLastWriteTime(file);
                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var stream = entry.Open()) {
                            fs.CopyTo(stream);
                        }
                    }
                }
            } catch (Exception ex) {
                MessageBox.Show("An error occurred: " + ex.Message);
            }
        }

        public static void ExtractZipToDirectory(string sourceArchiveFileName, string destinationDirectoryName) {
            try {
                ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName, overwriteFiles: true);
            } catch (Exception ex) {
                MessageBox.Show("An error occurred: " + ex.Message);
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