namespace PythonEmbedded.Net.OLD;

public static class Constants
{
    public static string InstanceMetadataFileName = "instance_metadata.json";
    
    public static string RequirementsCheckScript = """
import sys
import os
import importlib.metadata
import json
import traceback

try:
    from packaging.requirements import Requirement
    HAS_PACKAGING = True
except ImportError:
    HAS_PACKAGING = False

def check_requirements(req_file):
    results = []
    if not os.path.exists(req_file):
        return results

    with open(req_file, 'r') as f:
        lines = f.readlines()

    for line in lines:
        line = line.strip()
        if not line or line.startswith('#') or line.startswith('-r'):
            continue
        
        # Simple requirement parsing if packaging is not available
        req_str = line
        name = req_str
        spec = ''
        
        if HAS_PACKAGING:
            try:
                r = Requirement(req_str)
                name = r.name
                spec = str(r.specifier)
            except:
                # Fallback for complex lines that packaging might fail on (like URLs)
                pass
        else:
            # Very basic fallback parser
            for op in ['==', '>=', '<=', '>', '<', '!=', '~=']:
                if op in req_str:
                    parts = req_str.split(op, 1)
                    name = parts[0].strip()
                    spec = op + parts[1].strip()
                    break

        try:
            installed_version = importlib.metadata.version(name)
            is_installed = True
            
            if HAS_PACKAGING:
                try:
                    r = Requirement(req_str)
                    meets = r.specifier.contains(installed_version, prereleases=True)
                except:
                    meets = True # Can't check, assume true if installed
            else:
                # Basic check for == and >= if packaging is missing
                meets = True
                if '==' in spec:
                    target = spec.split('==')[1].strip()
                    meets = (installed_version == target)
                elif '>=' in spec:
                    target = spec.split('>=')[1].strip()
                    # Crude version comparison
                    try:
                        from packaging.version import Version
                        meets = (Version(installed_version) >= Version(target))
                    except ImportError:
                        try:
                            from distutils.version import LooseVersion
                            meets = (LooseVersion(installed_version) >= LooseVersion(target))
                        except:
                            # Fallback if both are missing
                            meets = (installed_version >= target)
            
            results.append({
                'spec': req_str,
                'installed': True,
                'meets': bool(meets),
                'version': installed_version,
                'required': spec
            })
        except importlib.metadata.PackageNotFoundError:
            results.append({
                'spec': req_str,
                'installed': False,
                'meets': False,
                'version': None,
                'required': spec
            })
    return results

try:
    req_file = "{0}"
    results = check_requirements(req_file)
    print(json.dumps(results))
except Exception as e:
    print(json.dumps({'error': str(e), 'stacktrace': traceback.format_exc()}))
    sys.exit(1)
""";
}
