// <copyright file="SubmissionEndpointsTest.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Livestock.Be4Fe.Api.Tests.Submissions;

using System.Net;
using System.Text.Json;
using Defra.Livestock.Be4Fe.Api.Authentication;
using Defra.Livestock.Be4Fe.Api.Tests.Authentication;
using Defra.Livestock.Sdk.Core.Middleware.Headers;
using Defra.Livestock.Sdk.Submissions.Exceptions;
using Defra.Livestock.Sdk.Submissions.Models;
using Defra.Livestock.Sdk.Submissions.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Checks the submission package is wired into the death host: the endpoints are mapped, call the registered service,
/// and the host maps the package's exceptions to status codes. The submission process itself is tested in the package.
/// </summary>
public sealed class SubmissionEndpointsTest
{
    private const string SubmissionId = "ABC-DEF-GHJ";

    [Fact]
    public async Task StartShouldReturnTheDraftForTheCph()
    {
        await using var factory = new SubmissionFactory(new FakeSubmissionService());

        var response = await factory.CreateAuthenticatedClient().GetAsync("/submission/12/345/6789/start", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("submission-id").GetString().ShouldBe(SubmissionId);
    }

    [Fact]
    public async Task SubmitShouldReturn503UntilTheTransferIsBuilt()
    {
        await using var factory = new SubmissionFactory(new FakeSubmissionService { SubmitFailure = new SubmissionTransferNotConfiguredException() });

        var response = await factory.CreateAuthenticatedClient().PostAsync($"/submission/12/345/6789/{SubmissionId}", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task SubmitShouldReturn409ForASubmissionAlreadySubmitted()
    {
        await using var factory = new SubmissionFactory(new FakeSubmissionService { SubmitFailure = new SubmissionAlreadySubmittedException(SubmissionId) });

        var response = await factory.CreateAuthenticatedClient().PostAsync($"/submission/12/345/6789/{SubmissionId}", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetShouldReturn404ForAnUnknownSubmission()
    {
        await using var factory = new SubmissionFactory(new FakeSubmissionService());

        var response = await factory.CreateAuthenticatedClient().GetAsync("/submission/12/345/6789/XXX-XXX-XXX", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheHostShouldRegisterTheStandInTransfer()
    {
        await using var factory = new SubmissionFactory(new FakeSubmissionService());
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISubmissionTransfer>().ShouldBeOfType<NotConfiguredSubmissionTransfer>();
    }

    private sealed class SubmissionFactory(ISubmissionService service) : ApiFactory
    {
        public HttpClient CreateAuthenticatedClient()
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add(RequestHeaderNames.ApiKey, TestAuthentication.ApiKey);
            client.DefaultRequestHeaders.Add(RequestHeaderNames.CorrelationId, "corr-submission-test");
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting($"{ApiKeyAuthenticationOptions.SectionName}:Keys:0", TestAuthentication.ApiKey);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISubmissionService>();
                services.AddScoped(_ => service);

                // The package's retention cleanup reads Mongo, which these tests do not run.
                services.RemoveAll<IHostedService>();
            });
        }
    }

    private sealed class FakeSubmissionService : ISubmissionService
    {
        public Exception? SubmitFailure { get; init; }

        public Task<SubmissionResponse> StartAsync(string cph, CancellationToken cancellationToken = default) =>
            StartAsync(cph, null, cancellationToken);

        public Task<SubmissionResponse> StartAsync(string cph, string? key, CancellationToken cancellationToken) =>
            Task.FromResult(new SubmissionResponse
            {
                SubmissionId = SubmissionId,
                AnimalCount = 0,
                Status = SubmissionStatus.Draft,
                CreatedDate = DateTimeOffset.UtcNow,
            });

        public Task<SubmissionResponse> GetSubmissionAsync(string cph, string submissionId, CancellationToken cancellationToken = default) =>
            throw new SubmissionNotFoundException(submissionId);

        public Task SubmitAsync(string cph, string submissionId, CancellationToken cancellationToken = default) =>
            SubmitFailure is null ? Task.CompletedTask : Task.FromException(SubmitFailure);

        public Task<AnimalResponse> GetAnimalAsync(string cph, string submissionId, string animalId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpsertAnimalAsync(string cph, string submissionId, string animalId, AnimalRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAnimalAsync(string cph, string submissionId, string animalId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SubmissionListResponse> ListAsync(string cph, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
