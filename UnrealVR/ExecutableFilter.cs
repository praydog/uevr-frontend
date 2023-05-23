using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json;
using System.IO;

namespace UnrealVR {
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
            var resourceName = "UnrealVR.FilteredExecutables.json";

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

        public bool IsValidExecutable(string executableName) {
            // Return true if the executable is not in the list of non-games
            return !m_invalidExecutables.Contains(executableName.ToLower());
        }
    }
}