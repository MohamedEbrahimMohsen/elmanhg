using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Elmanhg.Infrastructure.AiService;

public static class FakeEmbeddingVectors
{
    public static float[] Create(string text, int dimensions)
    {
        var vector = new float[dimensions];
        foreach (var token in Tokens(AnswerNormalizer.Normalize(text, AnswerNormalization.Default)))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = (int)(BinaryPrimitives.ReadUInt32BigEndian(hash) % (uint)dimensions);
            vector[index] += (hash[4] & 1) == 0 ? 1f : -1f;
        }

        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm == 0f)
        {
            vector[0] = 1f;
            return vector;
        }

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] /= norm;
        }

        return vector;
    }

    private static IEnumerable<string> Tokens(string text)
    {
        var start = -1;
        for (var index = 0; index <= text.Length; index++)
        {
            var inToken = index < text.Length && char.IsLetterOrDigit(text[index]);
            if (inToken && start < 0)
            {
                start = index;
            }
            else if (!inToken && start >= 0)
            {
                yield return text[start..index];
                start = -1;
            }
        }
    }
}
