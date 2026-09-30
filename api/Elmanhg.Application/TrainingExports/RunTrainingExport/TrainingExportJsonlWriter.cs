using Elmanhg.Application.TrainingExports.Shared;
using System.Security.Cryptography;
using System.Text.Json;

namespace Elmanhg.Application.TrainingExports.RunTrainingExport;

public sealed class TrainingExportJsonlWriter(Stream destination) : IDisposable
{
    private static readonly byte[] NewLine = "\n"u8.ToArray();
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    public long RowCount { get; private set; }

    public async Task WriteAsync<TLine>(TLine line, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(line, TrainingExportJson.SerializerOptions);
        _hash.AppendData(bytes);
        _hash.AppendData(NewLine);
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await destination.WriteAsync(NewLine, cancellationToken).ConfigureAwait(false);
        RowCount++;
    }

    public string Sha256Hex() => Convert.ToHexStringLower(_hash.GetHashAndReset());

    public void Dispose() => _hash.Dispose();
}
