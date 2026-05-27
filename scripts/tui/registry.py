"""Decorator-based self-registration system for TUI menu items.

New agents are added by creating a module in :mod:`scripts.tui.items` with a
``@register`` decorator call — no core TUI code changes required.

Usage::

    from scripts.tui.registry import register

    @register("OpenCode", "Register mcp-cad in opencode.json", category="agent")
    class OpenCodeItem(MenuItem):
        ...

Global registry
---------------
- :data:`_REGISTRY` — dict of category → list of ``MenuItem`` instances.
- :func:`get_all`   — flat list of all registered items.
- :func:`get_by_category` — items filtered by category.
- :func:`reset`     — clear the registry (for testing).
"""

from __future__ import annotations

from typing import Callable

# Category → list of registered MenuItem *instances*.
_REGISTRY: dict[str, list] = {}

# Allowed category values.
VALID_CATEGORIES = {"agent", "system", "future"}


def register(
    name: str,
    description: str,
    category: str = "agent",
) -> Callable[[type], type]:
    """Decorator that registers a :class:`MenuItem` subclass in the global registry.

    The decorated class is instantiated once (parameterless ``__init__`` assumed)
    and the instance is stored under the given *category*.

    The decorator **preserves** the original class — it does not replace it.

    Parameters
    ----------
    name:
        Display title for the menu item.
    description:
        Short explanation shown in the menu.
    category:
        Grouping category — one of ``"agent"``, ``"system"``, ``"future"``.

    Raises
    ------
    ValueError
        If *category* is not in :data:`VALID_CATEGORIES`.
    """
    if category not in VALID_CATEGORIES:
        raise ValueError(
            f"Invalid category '{category}'. "
            f"Must be one of: {', '.join(sorted(VALID_CATEGORIES))}"
        )

    def decorator(cls: type) -> type:
        instance = cls()
        _REGISTRY.setdefault(category, []).append(instance)
        # Store registration metadata on the class for reference.
        cls._register_name = name          # noqa: SLF001
        cls._register_description = description  # noqa: SLF001
        cls._register_category = category   # noqa: SLF001
        return cls

    return decorator


def get_all() -> list:
    """Return a flat list of all registered menu-item instances."""
    return [item for items in _REGISTRY.values() for item in items]


def get_by_category(category: str) -> list:
    """Return menu-item instances filtered by *category*.

    Returns an empty list if no items are registered for the category.
    """
    return _REGISTRY.get(category, []).copy()


def reset() -> None:
    """Clear the global registry.

    Primarily useful in test teardown to avoid cross-test pollution.
    """
    _REGISTRY.clear()