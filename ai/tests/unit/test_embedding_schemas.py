import pytest
from pydantic import ValidationError

from elmanhg_ai.api.embeddings.schemas import EmbeddingInputType, EmbeddingsIn


def test_embeddings_in_valid_payload_parses() -> None:
    payload = EmbeddingsIn.model_validate({"inputType": "query", "texts": ["قانون أوم", "b"]})

    assert payload.input_type is EmbeddingInputType.QUERY
    assert payload.texts == ["قانون أوم", "b"]


def test_embeddings_in_empty_texts_raises_at_texts() -> None:
    with pytest.raises(ValidationError) as error:
        EmbeddingsIn.model_validate({"inputType": "document", "texts": []})

    assert error.value.errors()[0]["loc"] == ("texts",)


def test_embeddings_in_blank_text_raises_at_texts_index() -> None:
    with pytest.raises(ValidationError) as error:
        EmbeddingsIn.model_validate({"inputType": "document", "texts": [""]})

    assert error.value.errors()[0]["loc"] == ("texts", 0)


def test_embeddings_in_unknown_input_type_raises_at_input_type() -> None:
    with pytest.raises(ValidationError) as error:
        EmbeddingsIn.model_validate({"inputType": "passage", "texts": ["a"]})

    assert error.value.errors()[0]["loc"] == ("inputType",)


def test_embeddings_in_extra_field_raises_forbidden() -> None:
    with pytest.raises(ValidationError) as error:
        EmbeddingsIn.model_validate({"inputType": "query", "texts": ["a"], "model": "x"})

    assert error.value.errors()[0]["loc"] == ("model",)
    assert error.value.errors()[0]["type"] == "extra_forbidden"
