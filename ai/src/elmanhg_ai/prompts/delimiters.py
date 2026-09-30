import re

from pydantic import JsonValue


def delimiter_pattern(*names: str) -> re.Pattern[str]:
    return re.compile(
        rf"<\s*(?:/\s*)?(?:{'|'.join(map(re.escape, names))})\b[^<>]*>?", re.IGNORECASE
    )


def strip_tags(text: str, pattern: re.Pattern[str]) -> str:
    # Repeat until stable: removing an inner tag can join its neighbours into a new tag.
    stripped = pattern.sub("", text)
    while stripped != text:
        text = stripped
        stripped = pattern.sub("", text)
    return stripped


def strip_fields(value: JsonValue, pattern: re.Pattern[str]) -> JsonValue:
    match value:
        case str():
            return strip_tags(value, pattern)
        case list():
            return [strip_fields(item, pattern) for item in value]
        case dict():
            return {key: strip_fields(item, pattern) for key, item in value.items()}
        case _:
            return value
