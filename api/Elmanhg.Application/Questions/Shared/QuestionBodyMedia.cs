using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Elmanhg.Application.Questions.Shared;

public static class QuestionBodyMedia
{
    public static JsonElement Resolve(QuestionType type, string body, IFileStorage fileStorage)
    {
        var node = JsonNode.Parse(body);
        if (type == QuestionType.DragDrop && node?["image"] is JsonObject image && image["key"] is JsonValue value && value.TryGetValue<string>(out var key) && DiagramImageKey.IsValid(key))
        {
            image["url"] = fileStorage.GetPublicUrl(key);
        }

        return JsonSerializer.SerializeToElement(node);
    }

    public static void EnsureLessonMedia(QuestionFields fields, Guid lessonId)
    {
        if (fields.Type == QuestionType.DragDrop && !DiagramImageKey.BelongsToLesson(QuestionSchemaReader.Read<DragDropBody>(fields.Body).Image?.Key, lessonId))
        {
            throw new ApplicationValidationCoreException(ErrorCodes.QuestionDiagramImageInvalid);
        }
    }
}
