"""Abstract base class for TUI menu items.

Every registration task shown in the TUI menu is a :class:`MenuItem` subclass.
Subclasses must implement the four abstract members: :attr:`name`,
:attr:`description`, :attr:`is_enabled`, and :meth:`run`.

Concrete items self-register via the :func:`~scripts.tui.registry.register`
decorator — no central registry file edits required.
"""

from __future__ import annotations

from abc import ABC, abstractmethod


class MenuItem(ABC):
    """Base class for selectable menu items in the TUI installer.

    Attributes
    ----------
    name:
        Short identifier used for state tracking and menu display.
    description:
        Human-readable explanation shown next to the menu entry.
    is_enabled:
        Whether the item is currently available for execution.
    """

    @property
    @abstractmethod
    def name(self) -> str:
        """Short identifier for this menu item."""

    @property
    @abstractmethod
    def description(self) -> str:
        """Human-readable description of what this item does."""

    @property
    @abstractmethod
    def is_enabled(self) -> bool:
        """Whether this item is currently available for execution."""

    @abstractmethod
    def run(self) -> str:
        """Execute the registration action and return a result message.

        Returns
        -------
        str
            A short success message (e.g. the path to the written config).

        Raises
        ------
        Exception
            If the registration fails — the TUI will display the error.
        """

    @property
    def category(self) -> str:
        """Category for grouping in the registry.  Default is ``"agent"``."""
        return "agent"