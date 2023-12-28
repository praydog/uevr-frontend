using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json;
using System.IO;
using System.Security.Cryptography;

namespace UEVR {
    public class ExecutableFilter {
        private HashSet<string> m_invalidExecutables = new HashSet<string>();

        public ExecutableFilter() {
            var filter = LoadFilterList();
            if (filter != null) {
                m_invalidExecutables = new HashSet<string>(filter);
            }
        }

        private List<string>? LoadFilterList() {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "UEVR.FilteredExecutables.json";

            using (Stream? stream = assembly.GetManifestResourceStream(resourceName)) {
                if (stream == null) {
                    return new List<String>();
                }

                using (StreamReader reader = new StreamReader(stream)) {
                    string json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<List<string>>(json);
                }
            }
        }

        private string HashString(string text) {
            using (SHA256 sha256 = SHA256.Create()) {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hash.Length; i++) {
                    sb.Append(hash[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        public bool IsValidExecutable(string executableName) {
            // Hash the incoming executable name
            string hashedExecutableName = HashString(executableName);

            // Return true if the hashed executable is not in the list of non-games
            return !m_invalidExecutables.Contains(hashedExecutableName);
        }
    }
}