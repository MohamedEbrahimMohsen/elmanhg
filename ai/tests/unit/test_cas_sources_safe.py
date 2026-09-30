from pathlib import Path
from typing import Final

import elmanhg_ai.cas

CAS_SOURCES: Final = Path(elmanhg_ai.cas.__file__).parent
FORBIDDEN: Final = ("sympify", "parse_expr", "parse_latex", "eval(", "exec(", 'S("', "lambdify")


def test_cas_sources_never_evaluate_strings() -> None:
    sources = sorted(CAS_SOURCES.glob("*.py"))

    found = [
        (source.name, name)
        for source in sources
        for name in FORBIDDEN
        if name in source.read_text(encoding="utf-8")
    ]

    assert len(sources) >= 9
    assert found == []
