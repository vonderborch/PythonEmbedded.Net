import os
import selectors
import subprocess
import sys

# Step 1: Get the directory path of the current script
current_file_directory = os.path.dirname(os.path.realpath(__file__))

# Step 2: Get the build directory (for our virtual environment)
build_directory = os.path.join(current_file_directory, ".build")

# Step 3: Call generate_project_logo.py with the settings
script_path = os.path.join(current_file_directory, ".build", "generate_project_logo.py")

project_name = os.path.basename(current_file_directory)
if "." in project_name:
    font_sizes = "26, 84"
else:
    font_sizes = "84"

command = [
    "python",
    script_path,
    build_directory,
    os.path.join(build_directory, "base_logo.png"),
    os.path.join(current_file_directory, "logo.png"),
    os.path.join(current_file_directory, "logo.ico"),
    project_name,
    os.path.join(build_directory, "font", "copperplate_gothic_bold.otf"),
    font_sizes,
    str(280),
    "white",
]

if sys.platform == "win32":
    p = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True, shell=True)
    ok = True
    while ok:
        for line in p.stdout:
            print(f"{line}", end="")
        for line in p.stderr:
            print(f"ERROR: {line}", end="", file=sys.stderr)
        if p.poll() is not None:
            ok = False
else:
    p = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True)
    sel = selectors.DefaultSelector()
    sel.register(p.stdout, selectors.EVENT_READ)
    sel.register(p.stderr, selectors.EVENT_READ)
    ok = True
    while ok:
        for key, val1 in sel.select():
            line = key.fileobj.readline()
            if not line:
                ok = False
                break
            if key.fileobj is p.stdout:
                print(f"{line}", end="")
            else:
                print(f"ERROR: {line}", end="", file=sys.stderr)
