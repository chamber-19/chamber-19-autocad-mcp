"""
autocad/client.py
-----------------
Thin wrapper around an AutoCAD COM connection (pyautocad) with a
DXF-only fallback via ezdxf for platforms without AutoCAD installed.
"""

from __future__ import annotations

import os
import logging
from typing import Optional

logger = logging.getLogger(__name__)

_COM_ENABLED = os.environ.get("AUTOCAD_COM_ENABLED", "true").lower() != "false"


class AutoCADClient:
    """
    Manages the connection to AutoCAD.

    On Windows with AutoCAD running, it uses COM automation via
    *pyautocad*.  On other platforms (or when ``AUTOCAD_COM_ENABLED=false``)
    it operates in DXF-only mode using *ezdxf*.
    """

    def __init__(self) -> None:
        self._acad = None       # pyautocad.Autocad instance
        self._dxf_doc = None    # ezdxf.document.Drawing instance
        self._dxf_path: Optional[str] = None
        self._com_available = False

        if _COM_ENABLED:
            self._connect_com()

    # ------------------------------------------------------------------
    # COM connection
    # ------------------------------------------------------------------

    def _connect_com(self) -> None:
        try:
            from pyautocad import Autocad  # type: ignore
            self._acad = Autocad(create_if_not_exists=False)
            # Probe the connection
            _ = self._acad.doc.Name
            self._com_available = True
            logger.info("Connected to AutoCAD via COM automation.")
        except Exception as exc:
            logger.warning("COM connection unavailable (%s). Falling back to DXF-only mode.", exc)
            self._com_available = False

    # ------------------------------------------------------------------
    # Public helpers
    # ------------------------------------------------------------------

    @property
    def is_com(self) -> bool:
        """True when connected to a live AutoCAD instance via COM."""
        return self._com_available

    def open_file(self, path: str) -> None:
        """Open a DWG or DXF file."""
        if self._com_available and self._acad is not None:
            self._acad.app.Documents.Open(path)
        else:
            import ezdxf  # type: ignore
            self._dxf_doc = ezdxf.readfile(path)
            self._dxf_path = path
            logger.info("Opened DXF file: %s", path)

    def save_file(self, path: Optional[str] = None) -> None:
        """Save the current drawing, optionally to a new path."""
        if self._com_available and self._acad is not None:
            if path:
                self._acad.doc.SaveAs(path)
            else:
                self._acad.doc.Save()
        else:
            if self._dxf_doc is None:
                raise RuntimeError("No drawing is currently open.")
            save_path = path or self._dxf_path
            if save_path is None:
                raise ValueError("A file path must be supplied when saving a new drawing.")
            self._dxf_doc.saveas(save_path)
            self._dxf_path = save_path
            logger.info("Saved DXF file: %s", save_path)

    def new_drawing(self) -> None:
        """Create a new empty drawing."""
        if self._com_available and self._acad is not None:
            self._acad.app.Documents.Add()
        else:
            import ezdxf  # type: ignore
            self._dxf_doc = ezdxf.new()
            self._dxf_path = None
            logger.info("Created new in-memory DXF drawing.")

    def get_modelspace(self):
        """Return the modelspace object (COM or ezdxf)."""
        if self._com_available and self._acad is not None:
            return self._acad.model
        if self._dxf_doc is not None:
            return self._dxf_doc.modelspace()
        raise RuntimeError("No drawing is currently open.")

    def get_doc(self):
        """Return the raw document object for advanced use."""
        if self._com_available and self._acad is not None:
            return self._acad.doc
        return self._dxf_doc
