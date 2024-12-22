using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace UEVR;

public class ExecutableFilter {
    const string RESOURCE_NAME = "UEVR.PlainTextFilteredExecutables.json";

    readonly List<string> invalidExecutableNames = new();

    public ExecutableFilter() {
        var assembly = Assembly.GetExecutingAssembly();

        using (var stream = assembly.GetManifestResourceStream(RESOURCE_NAME)) {
            if (stream == null) return;
            var deserializedList = JsonSerializer.Deserialize<List<string>>(stream);
            if (deserializedList != null) invalidExecutableNames = deserializedList;
        }
    }

    public bool IsValidExecutable(string executableName)
        => !invalidExecutableNames.Any(n => string.Equals(executableName, n, StringComparison.OrdinalIgnoreCase));
}
