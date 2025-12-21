""" --------------------------------------------------------------------
Imports
-------------------------------------------------------------------- """
import os
import shutil
import sys
import zipfile

from datetime import datetime, timedelta
from functools import cached_property
from generate_virtual_environment import VirtualEnvironmentManager


class LogoGenerator:
    """Generates the logo for a project
    """

    def __init__(
        self,
        virtual_environment_directory: str,
        base_logo_file: str,
        logo_file: str,
        logo_icon_file: str,
        text_to_add: list[str],
        text_to_add_font: str,
        text_to_add_font_size: list[int],
        text_to_add_starting_y: int,
        text_to_add_color: str,
    ) -> None:
        """Initializes the LogoGenerator.
        
        Sets up the necessary directories and installs the virtualenv package.  It also sets up the virtual environment
        if one doesn't already exist.
        
        Args:
            directory (str): The base directory for FNA installation and related files.
        """
        self._virtual_environment_directory = virtual_environment_directory
        
        self._base_logo_file = base_logo_file
        self._logo_file = logo_file
        self._logo_icon_file = logo_icon_file
        self._text_to_add = text_to_add
        self._text_to_add_font = text_to_add_font
        self._text_to_add_font_size = text_to_add_font_size
        self._text_to_add_starting_y = text_to_add_starting_y
        self._text_to_add_color = text_to_add_color

        self._manage_directory(directory=self._virtual_environment_directory, delete_directory_if_exists=False,
                               create_directory_if_not_exists=True)
        env_manager = VirtualEnvironmentManager(directory=self._virtual_environment_directory)
        env_manager.setup_environment()
        env_manager.install_package("Pillow", version="11.1.0")

    def execute(self, generate_ico_files: bool) -> None:
        """Executes the Logo Generator logic.
        
        Args:
            generate_ico_files (bool): True to generate ICO files, False otherwise.
        """
        # Step 1: Update the logo file
        self._update_logo_with_text()
        
        # Step 2: Generate the ICO files
        if generate_ico_files:
            self._generate_icon_file()

        # Step 3: ???
        # Step 4: Done!
        print("Done!")
        sys.exit(0)
        
    def _update_logo_with_text(self) -> None:
        from PIL import Image, ImageDraw, ImageFont

        img = Image.open(self._base_logo_file)
        image_updator = ImageDraw.Draw(img)
        
        y_coords = self._text_to_add_starting_y
        for i, text in enumerate(self._text_to_add):
            recalculate_font_size = True
            font_size = self._text_to_add_font_size[i]
            width = 0
            font = ImageFont.truetype(self._text_to_add_font, font_size)
            while recalculate_font_size:
                width = image_updator.textlength(text, font=font)
                if width >= img.width - 10:
                    font_size -= 1
                    font = ImageFont.truetype(self._text_to_add_font, font_size)
                else:
                    recalculate_font_size = False
            
            image_updator.text(
                ((img.width - width) / 2, y_coords),
                text=text,
                fill=self._text_to_add_color,
                font=font
            )
            y_coords += font_size
        
        img.save(self._logo_file)
        
    def _generate_icon_file(self) -> None:
        from PIL import Image
        img = Image.open(self._logo_file)
        img.save(self._logo_icon_file, format="ICO", sizes=[(16, 16), (32, 32), (64, 64), (128, 128), (256, 256)])
    
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


# Get arguments
virtual_environment_directory = sys.argv[1].strip()  # the directory for the virtual environment
base_logo_file = sys.argv[2].strip()  # the base logo file
logo_file = sys.argv[3].strip()  # the logo file to be generated
logo_icon_file = sys.argv[4].strip()  # the logo icon file to be generated
text_to_add = sys.argv[5].strip()  # the text to add to the logo
text_to_add_font = sys.argv[6].strip()  # the font to use for the text
text_to_add_font_size = sys.argv[7].strip()  # the font size for the text
text_to_add_starting_y = int(sys.argv[8].strip())  # the starting y coordinate for the text
text_to_add_color = sys.argv[9].strip()  # the color of the text

text_to_add_parts = list(text_to_add.split(".")) if "." in text_to_add else [text_to_add]
text_to_add_font_size_parts = [int(val) for val in text_to_add_font_size.split(",")]
generator = LogoGenerator(
    virtual_environment_directory=virtual_environment_directory,
    base_logo_file=base_logo_file,
    logo_file=logo_file,
    logo_icon_file=logo_icon_file,
    text_to_add=text_to_add_parts,
    text_to_add_font=text_to_add_font,
    text_to_add_font_size=text_to_add_font_size_parts,
    text_to_add_starting_y=text_to_add_starting_y,
    text_to_add_color=text_to_add_color
)
generator.execute(generate_ico_files=True)
