import json
import hashlib

def hash_string(s):
    return hashlib.sha256(s.encode()).hexdigest().upper()

# Read existing JSON file
with open("PlainTextFilteredExecutables.json", "r") as f:
    executables = json.load(f)

# Create a list to hold hashed executable names
hashed_executables = [hash_string(executable) for executable in executables]

# Save to a new JSON file
with open("FilteredExecutables.json", "w") as f:
    json.dump(hashed_executables, f, indent=4)