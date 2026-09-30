using MediatR;

namespace Elmanhg.Application.LoadTesting.SeedLoadTestData;

public sealed record SeedLoadTestDataCommand(string Key, int StudentCount, string StudentPassword) : IRequest<SeedLoadTestDataResult>;
