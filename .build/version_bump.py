import json
import os
import re
import sys
import argparse
import subprocess
import platform

def get_latest_version(directory):
    cache_file = os.path.join(directory, ".version.json")
    if os.path.exists(cache_file):
        with open(cache_file, 'r') as f:
            version = json.load(f)
            return version.get('major', 0), version.get('minor', 0), version.get('patch', 0), version.get('revision', 0)
    else:
        return 0, 0, 0, 0

def save_latest_version(directory, major, minor, patch, revision):
    cache_file = os.path.join(directory, ".version.json")
    version = {
        'major': major,
        'minor': minor,
        'patch': patch,
        'revision': revision
    }
    with open(cache_file, 'w') as f:
        json.dump(version, f, indent=4)
    return version

def get_project_files(directory, excluded_directories):
    project_files = []
    for root, dirs, files in os.walk(directory):
        # Filter out excluded directories
        dirs[:] = [d for d in dirs if not any(exclude.lower() in os.path.join(root, d).lower() for exclude in excluded_directories)]
        
        for file in files:
            if file.endswith(".csproj"):
                project_files.append(os.path.join(root, file))
    return project_files

def update_project_files(directory, major, minor, patch, revision, excluded_directories):
    version_str = f"{major}.{minor}.{patch}.{revision}"
    print(f"New version: {version_str}")
    
    files = get_project_files(directory, excluded_directories)
    print(f"Found {len(files)} files to update...")
    
    regex = r'(?<=Version>)\d+\.\d+\.\d+(\.\d+)?(?=</)'
    
    for file_path in files:
        print(f"  Updating {file_path}...")
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = re.sub(regex, version_str, content)
        
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
            
    save_latest_version(directory, major, minor, patch, revision)

def show_dialog(title, message, default_version):
    system = platform.system()
    if system == "Darwin": # macOS
        osascript = f'display dialog "{message}" default answer "{default_version}" buttons {{"OK", "Cancel"}} default button 1 with title "{title}"'
        try:
            result = subprocess.check_output(['osascript', '-e', osascript], stderr=subprocess.STDOUT).decode('utf-8')
            # Result format: button returned:OK, text returned:1.3.2.39
            match = re.search(r'text returned:(.*)', result)
            if match:
                return match.group(1).strip()
        except subprocess.CalledProcessError as e:
            if "User canceled" in e.output.decode('utf-8'):
                print("User cancelled.")
                sys.exit(0)
            raise
    elif system == "Windows":
        try:
            import ctypes
            # Simple fallback for Windows if we can't get a proper InputBox easily without heavy deps
            # Visual Basic InputBox is the easiest if we can call it.
            # But let's try a simpler approach or just print that it's not supported if we can't.
            # For now, let's use the PowerShell logic via subprocess if available, 
            # or just use input() if interactive.
            # Actually, let's try to use the same VB trick as PS:
            vb_script = f'result = InputBox("{message}", "{title}", "{default_version}"): WScript.Echo result'
            with open("temp_input.vbs", "w") as f:
                f.write(vb_script)
            result = subprocess.check_output(['wscript', '//Nologo', 'temp_input.vbs']).decode('utf-8').strip()
            os.remove("temp_input.vbs")
            if not result:
                print("User cancelled.")
                sys.exit(0)
            return result
        except Exception as e:
            print(f"Dialog failed on Windows: {e}")
            
    # Fallback to console input if dialog fails or unsupported
    print(f"{title}: {message}")
    user_input = input(f"Enter version [{default_version}]: ")
    return user_input.strip() if user_input.strip() else default_version

def main():
    parser = argparse.ArgumentParser(description='Bump version in project files.')
    parser.add_argument('--directory', required=True, help='Root directory of the project')
    parser.add_argument('--mode', choices=['Release-Major', 'Release-Minor', 'Release-Patch', 'Release-Revision', 'Dialog'], default='Release-Revision', help='Bumping mode')
    parser.add_argument('--exclude', nargs='*', default=[], help='Directories to exclude')
    
    args = parser.parse_args()
    
    major, minor, patch, revision = get_latest_version(args.directory)
    print(f"Previous version: {major}.{minor}.{patch}.{revision}")
    
    if args.mode == 'Release-Major':
        major += 1
        minor = 0
        patch = 0
        revision = 0
    elif args.mode == 'Release-Minor':
        minor += 1
        patch = 0
        revision = 0
    elif args.mode == 'Release-Patch':
        patch += 1
        revision = 0
    elif args.mode == 'Release-Revision':
        revision += 1
    elif args.mode == 'Dialog':
        # Default to a revision bump for the dialog default value
        revision += 1
        version_input = show_dialog("Bump Version", "Please enter the new version", f"{major}.{minor}.{patch}.{revision}")
        parts = version_input.split('.')
        if len(parts) >= 1: major = int(parts[0])
        if len(parts) >= 2: minor = int(parts[1])
        if len(parts) >= 3: patch = int(parts[2])
        if len(parts) >= 4: revision = int(parts[3])
        
    update_project_files(args.directory, major, minor, patch, revision, args.exclude)

if __name__ == "__main__":
    main()
