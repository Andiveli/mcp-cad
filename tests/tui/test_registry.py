"""Tests for :mod:`scripts.tui.registry`.

Verifies the ``@register`` decorator, ``get_all``, and ``get_by_category``,
with proper isolation between tests via ``reset()``.
"""

from __future__ import annotations

import pytest

from scripts.tui.registry import VALID_CATEGORIES, get_all, get_by_category, register, reset


# ------------------------------------------------------------------
# Fixtures
# ------------------------------------------------------------------


@pytest.fixture(autouse=True)
def _clean_registry():
    """Clear the global registry before and after each test."""
    reset()
    yield
    reset()


# ------------------------------------------------------------------
# @register decorator
# ------------------------------------------------------------------


class TestRegisterDecorator:
    """Tests for the ``@register`` decorator."""

    def test_register_adds_item_to_registry(self) -> None:
        """A decorated class is instantiated and stored in the registry."""

        @register("test-item", "A test item", category="agent")
        class _TestItem:
            pass

        items = get_by_category("agent")
        assert len(items) == 1
        assert isinstance(items[0], _TestItem)

    def test_register_preserves_class(self) -> None:
        """The decorator returns the original class unchanged."""

        @register("preserve-test", "desc", category="agent")
        class _Preserved:
            value = 42

        assert _Preserved.value == 42
        assert _Preserved._register_name == "preserve-test"
        assert _Preserved._register_description == "desc"
        assert _Preserved._register_category == "agent"

    def test_register_stores_metadata_on_class(self) -> None:
        """Registration metadata is attached as class attributes."""

        @register("meta-item", "meta description", category="agent")
        class _MetaItem:
            pass

        assert _MetaItem._register_name == "meta-item"
        assert _MetaItem._register_description == "meta description"
        assert _MetaItem._register_category == "agent"

    def test_register_rejects_invalid_category(self) -> None:
        """Non-allowed categories raise ``ValueError``."""
        with pytest.raises(ValueError, match="Invalid category"):
            @register("bad", "desc", category="nonexistent")
            class _BadItem:
                pass

    def test_register_accepts_valid_categories(self) -> None:
        """Each valid category is accepted without error."""

        for cat in VALID_CATEGORIES:
            @register(f"item-{cat}", f"desc-{cat}", category=cat)
            class _CatItem:
                pass

        for cat in VALID_CATEGORIES:
            assert len(get_by_category(cat)) == 1


# ------------------------------------------------------------------
# get_all / get_by_category
# ------------------------------------------------------------------


class TestQueryFunctions:
    """Tests for ``get_all`` and ``get_by_category``."""

    def test_get_all_returns_empty_after_reset(self) -> None:
        """``get_all`` returns an empty list after clearing the registry."""
        assert get_all() == []

    def test_get_all_returns_flat_list(self) -> None:
        """``get_all`` flattens all categories into one list."""

        @register("a1", "desc", category="agent")
        class _A1:
            pass

        @register("s1", "desc", category="system")
        class _S1:
            pass

        all_items = get_all()
        names = {getattr(i, "_register_name", None) for i in all_items}
        assert names == {"a1", "s1"}

    def test_get_by_category_filters_correctly(self) -> None:
        """Items are returned only for the requested category."""

        @register("a1", "desc", category="agent")
        class _A1:
            pass

        @register("s1", "desc", category="system")
        class _S1:
            pass

        agents = get_by_category("agent")
        systems = get_by_category("system")

        assert len(agents) == 1
        assert len(systems) == 1
        assert agents[0]._register_name == "a1"  # noqa: SLF001
        assert systems[0]._register_name == "s1"  # noqa: SLF001

    def test_get_by_category_returns_copy(self) -> None:
        """Mutating the returned list does not affect the registry."""

        @register("copy-test", "desc", category="agent")
        class _CopyItem:
            pass

        result = get_by_category("agent")
        result.clear()
        assert len(get_by_category("agent")) == 1

    def test_get_by_category_returns_empty_for_unknown(self) -> None:
        """Unknown categories return an empty list."""
        assert get_by_category("nonexistent") == []