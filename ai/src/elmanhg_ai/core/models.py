from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel


class ApiInModel(BaseModel):
    model_config = ConfigDict(
        alias_generator=to_camel, populate_by_name=True, extra="forbid", frozen=True
    )


class ApiOutModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)
