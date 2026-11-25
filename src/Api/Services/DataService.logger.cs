// <copyright file="DataService.logger.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Livestock.Be4Fe.Api.Services;

public partial class DataService
{
    [LoggerMessage(LogLevel.Information, "Retrieved {Count} unallocated eartags, leaving out {Excluded} used in the submission. Cph: {Cph}")]
    partial void LogRetrievedUnallocatedEartags(int count, int excluded, string cph);

    [LoggerMessage(LogLevel.Information, "Retrieved {Count} animals. Query: {Query}, Cph: {Cph}")]
    partial void LogRetrievedAnimals(int count, string query, string cph);
}
