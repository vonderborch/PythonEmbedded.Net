""" --------------------------------------------------------------------
Imports
-------------------------------------------------------------------- """
import os
import platform
import shutil
import subprocess
import sys
import zipfile

from datetime import datetime, timedelta
from functools import cached_property
from importlib import util


class VirtualEnvironmentManager:
    """Manages a virtual environment"""

    def __init__(self, directory: str) -> None:
        """Initializes the Virtual Environment Manager.
        
        Args:
            directory (str): The base directory for FNA installation and related files.
        """
        self._base_directory = directory
        self._virtual_environment_path = os.path.join(directory, ".venv")
        
    def setup_environment(self) -> None:
        """Sets up the Python Virtual Environment"""
        self.install_package("virtualenv")
        self._setup_environment()
        

    @staticmethod
    def _is_virtual_environment() -> bool:
        """Checks if the current Python environment is a virtual environment.
        
        Determines if the script is running within a virtual environment by examining sys attributes.
        
        Returns:
            bool: True if the current environment is a virtual environment, False otherwise.
        """
        return hasattr(sys, "real_prefix") or (hasattr(sys, "base_prefix") and sys.base_prefix != sys.prefix)

    def _setup_environment(self) -> None:
        """Sets up a Python virtual environment if one is not already active.
        
        Checks if already running within a virtual environment. If not, creates and activates a new virtual environment
        at a predefined path.
        """
        print("Setting up environment...")
        if self._is_virtual_environment():
            print("  Already in virtual environment!")
            return

        # setup virtual environment
        setupVirtualEnvironment = not self._manage_directory(
            directory=self._virtual_environment_path,
            delete_directory_if_exists=True,
            create_directory_if_not_exists=False,
        )

        print("  Setting up virtual environment...")
        if setupVirtualEnvironment:
            subprocess.call([sys.executable, "-m", "virtualenv", self._virtual_environment_path])

        print("  Activating virtual environment...")
        if platform.system() == "Windows":
            activation_file = os.path.join(self._virtual_environment_path, "Scripts", "activate_this.py")
        else:
            activation_file = os.path.join(self._virtual_environment_path, "bin", "activate_this.py")
        exec(open(activation_file).read(), {'__file__': activation_file})

    def _manage_directory(
            self, *, directory: str, delete_directory_if_exists: bool, create_directory_if_not_exists: bool
    ) -> None:
        """Manages a directory, ensuring it exists and is a directory, not a file.
        
        Args:
            directory (str): The path to the directory to manage.
            create_directory_if_not_exists (bool): Whether to create the directory if it doesn't exist.
        """
        if os.path.exists(directory):
            if os.path.isfile(directory):
                raise Exception(f"Directory {directory} already exists and is a file?")

            if delete_directory_if_exists:
                self._remove_file_system_entry(directory)

        if create_directory_if_not_exists and not os.path.exists(directory):
            os.makedirs(directory)

    @staticmethod
    def _remove_file_system_entry(path: str) -> None:
        """Removes a file or directory from the file system.
        
        Deletes the specified file or directory.  If the path does not exist, no action is taken.
                
        Args:
            path (str): The path to the file or directory to remove.
        """
        if not os.path.exists(path):
            return

        if os.path.isfile(path):
            os.remove(path)
        else:
            shutil.rmtree(path)

    @staticmethod
    def install_package(package: str, version: str = "") -> None:
        """Installs a Python package if it is not already installed.

        Uses pip to install the specified package. If the package is already installed, no action is taken.
        
        Args:
            package (str): The name of the package to install.
            version (str, optional): The version of the package to install. Defaults to "".
        """
        if util.find_spec(package) is None:
            if version != "":
                package = f"{package}=={version}"
            subprocess.call([sys.executable, "-m", "pip", "install", package])
