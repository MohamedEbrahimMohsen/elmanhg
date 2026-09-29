using Microsoft.AspNetCore.Http;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.RecordVoiceDraft;

public static class TeacherVoiceSignatures
{
    public static readonly byte[] Webm = [0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01, 0x42, 0xF7, 0x81];
    public static readonly byte[] Ogg = [.. "OggS"u8, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    public static readonly byte[] M4a = [0x00, 0x00, 0x00, 0x20, .. "ftyp"u8, .. "M4A "u8];
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    public static IFormFile FormFile(byte[] bytes, string fileName = "voice.webm", string contentType = "audio/webm")
    {
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "audio", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }
}
