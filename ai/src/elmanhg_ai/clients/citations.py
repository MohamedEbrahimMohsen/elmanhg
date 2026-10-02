import json
import re
from collections.abc import Collection, Sequence
from typing import Final

from elmanhg_ai.clients.model import ModelSource
from elmanhg_ai.prompts.delimiters import delimiter_pattern, strip_tags

SOURCES_TAG: Final = "lesson_sources"
SOURCES_DELIMITER: Final = delimiter_pattern(SOURCES_TAG)
CITATION_SEPARATOR: Final = re.compile(r"[,\u060c]")
CITATION_MARKER: Final = re.compile(r" ?\[ *+([a-z0-9-]++(?: *+[,\u060c] *+[a-z0-9-]++)*+) *+\]")


def sources_json(sources: Sequence[ModelSource]) -> str:
    return json.dumps(
        [
            {
                "reference": source.reference,
                "title": strip_tags(source.title, SOURCES_DELIMITER),
                "content": strip_tags(source.content, SOURCES_DELIMITER),
            }
            for source in sources
        ],
        ensure_ascii=False,
        separators=(",", ":"),
    )


def extract_citations(text: str, references: Collection[str]) -> tuple[str, tuple[str, ...]]:
    known = set(references)
    cited: dict[str, None] = {}

    def replace(match: re.Match[str]) -> str:
        ids = [item.strip() for item in CITATION_SEPARATOR.split(match.group(1))]
        hits = [item for item in ids if item in known]
        if not hits:
            return match.group(0)
        for hit in hits:
            cited.setdefault(hit, None)
        return ""

    clean = CITATION_MARKER.sub(replace, text).strip()
    return clean, tuple(cited)
