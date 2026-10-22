using System.Security.Claims;
using Lugat.Api.Auth;
using Lugat.Application;
using Lugat.Application.Catalog;
using Lugat.Contracts;
using Lugat.Domain.Catalog;
using Lugat.Infrastructure.Catalog;

namespace Lugat.Api.Catalog;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        // Learners: levels and published units (spec §11). Locks and progress come in stage 3.
        var learner = app.MapGroup("/api").RequireAuthorization();
        learner.MapGet("/levels", (CatalogService catalog, CancellationToken ct) => catalog.LevelsAsync(ct));
        learner.MapGet("/levels/{level}/units", (CatalogLevel level, CatalogService catalog, CancellationToken ct) => catalog.UnitsAsync(level, ct));
        learner.MapGet("/units/{id:guid}", (Guid id, CatalogService catalog, CancellationToken ct) => catalog.UnitAsync(id, ct));

        // Content work (spec §2.4, §10): editors and admins; importing and splitting the list, the admin only.
        var editor = app.MapGroup("/api/admin").RequireAuthorization(Policies.Editor);
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Policies.Admin);

        editor.MapGet("/catalog/levels", (CatalogAdminService catalog, CancellationToken ct) => catalog.LevelsAsync(ct));

        admin.MapPost("/catalog/import", async (IFormFile? file, CatalogAdminService catalog, CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                throw new ValidationException("Fayl tanlanmagan.");
            }

            if (file.Length > CatalogFileReader.MaxBytes)
            {
                throw new PayloadTooLargeException("Fayl juda katta (20 MB gacha).");
            }

            await using var stream = file.OpenReadStream();
            return await catalog.ImportAsync(stream, file.FileName, ct);
        }).DisableAntiforgery(); // bearer tokens, no cookies: nothing to forge

        admin.MapPost("/catalog/levels/{level}/split", (CatalogLevel level, CatalogAdminService catalog, CancellationToken ct) =>
            catalog.SplitAsync(level, ct));

        editor.MapGet("/units", (CatalogLevel level, CatalogAdminService catalog, CancellationToken ct) => catalog.UnitsAsync(level, ct));
        editor.MapGet("/units/{id:guid}", (Guid id, CatalogAdminService catalog, CancellationToken ct) => catalog.UnitAsync(id, ct));
        editor.MapPost("/units/{id:guid}/draft", (Guid id, ClaimsPrincipal user, CatalogDraftService drafts, CancellationToken ct) =>
            drafts.DraftNextAsync(user.GetUserId(), id, ct));
        editor.MapPost("/units/{id:guid}/publish", (Guid id, CatalogAdminService catalog, CancellationToken ct) => catalog.PublishAsync(id, ct));

        editor.MapPut("/catalog/words/{id:guid}", (Guid id, UpdateCatalogWordRequest body, ClaimsPrincipal user, CatalogAdminService catalog, CancellationToken ct) =>
            catalog.UpdateWordAsync(user.GetUserId(), id, body, ct));
        editor.MapPost("/catalog/words/{id:guid}/review", (Guid id, ReviewCatalogWordRequest body, ClaimsPrincipal user, CatalogAdminService catalog, CancellationToken ct) =>
            catalog.ReviewAsync(user.GetUserId(), id, body.Version, ct));

        return app;
    }

    /// <summary>Development and tests only (Catalog:SeedSample): the 50-word sample as level A units 1–2.</summary>
    public static async Task SeedSampleAsync(this WebApplication app)
    {
        var options = app.Configuration.GetSection(CatalogOptions.SectionName).Get<CatalogOptions>() ?? new CatalogOptions();
        if (!options.SeedSample)
        {
            return;
        }

        var path = Path.IsPathRooted(options.SamplePath) ? options.SamplePath : Path.Combine(AppContext.BaseDirectory, options.SamplePath);
        await using var scope = app.Services.CreateAsyncScope();
        await using var file = File.OpenRead(path);
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync(CatalogSeeder.Read(file), CancellationToken.None);
    }
}
