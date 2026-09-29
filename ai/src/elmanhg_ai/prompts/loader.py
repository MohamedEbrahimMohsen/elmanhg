import importlib.resources
import re
from collections.abc import Mapping
from dataclasses import dataclass
from typing import Final

PLACEHOLDER: Final = re.compile(r"\{\{(\w+)\}\}")


@dataclass(frozen=True, slots=True)
class Prompt:
    name: str
    version: str
    text: str


class PromptNotFoundError(LookupError):
    pass


def load_prompt(name: str, version: str) -> Prompt:
    resource = importlib.resources.files("elmanhg_ai.prompts") / f"{name}.{version}.md"
    if not resource.is_file():
        raise PromptNotFoundError(f"prompt {name}.{version} not found")
    return Prompt(name=name, version=version, text=resource.read_text(encoding="utf-8"))


def render(template: str, values: Mapping[str, str]) -> str:
    return PLACEHOLDER.sub(lambda match: values[match.group(1)], template)
