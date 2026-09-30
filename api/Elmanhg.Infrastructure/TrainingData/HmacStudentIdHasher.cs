using Elmanhg.Application.Shared.TrainingData;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Elmanhg.Infrastructure.TrainingData;

public sealed class HmacStudentIdHasher(IOptions<TrainingDataOptions> options) : IStudentIdHasher
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(options.Value.StudentIdHashKey) ? TrainingDataOptions.DevelopmentStudentIdHashKey : options.Value.StudentIdHashKey);

    public string Hash(Guid studentId) => Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(studentId.ToString("D"))));

    public string HashSourceId(string scope, Guid sourceId) => Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"{scope}:{sourceId:D}")));
}
