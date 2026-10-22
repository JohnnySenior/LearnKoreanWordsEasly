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
        // Learners (spec §11): published units with the learner's locks; a locked unit's words answer 403 (spec §13.4).
        var learner = app.MapGroup("/api").RequireAuthorization();
        learner.MapGet("/levels", (CatalogService catalog, CancellationToken ct) => catalog.LevelsAsync(ct));
        learner.MapGet("/levels/{level}/units", (CatalogLevel level, ClaimsPrincipal user, UnitProgressService units, CancellationToken ct) =>
            units.UnitsAsync(user.GetUserId(), level, ct));
        learner.MapGet("/units/{id:guid}", (Guid id, ClaimsPrincipal user, UnitProgressService units, CancellationToken ct) =>
            units.UnitAsync(user.GetUserId(), id, ct));
        // Idempotent: the first call creates the unit's cards (spec §5.3).
        learner.MapPost("/units/{id:guid}/open", (Guid id, ClaimsPrincipal user, UnitProgressService units, CancellationToken ct) =>
            units.OpenAsync(user.GetUserId(), id, ct));

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

    /// <summary>Every published unit has its learning items (L-22).</summary>
    public static async Task SyncLearningItemsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using (await services.GetRequiredService<Lugat.Application.Abstractions.IOperationLock>().AcquireAsync(CatalogAdminService.LockKey, CancellationToken.None))
        {
            await LearningItemSync.SyncMissingAsync(services.GetRequiredService<Lugat.Application.Abstractions.IAppDbContext>(),
                services.GetRequiredService<TimeProvider>().GetUtcNow(), CancellationToken.None);
        }
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
