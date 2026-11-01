using Lugat.Application.Abstractions;
using Lugat.Application.Ai;
using Lugat.Application.Reminders;
using Lugat.Application.Study;
using Lugat.Application.Users;
using Lugat.Application.Vocabulary;
using Lugat.Domain.Learning;
using Lugat.Infrastructure.Ai;
using Lugat.Infrastructure.Files;
using Lugat.Infrastructure.Persistence;
using Lugat.Infrastructure.Telegram;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lugat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is not configured (see .env.example).");
        }

        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IOperationLock, Persistence.PostgresOperationLock>();
        services.AddScoped<AiUsageService>();
        services.AddOptions<AiBudgetOptions>().Bind(config.GetSection("Ai:Budget"))
            .Validate(o => o.MonthlyLimitUsd >= 0 && o.WarningUsd >= 0 && o.HostingMonthlyUsd >= 0
                && o.Prices.All(p => p.InputPerMillionUsd >= 0 && p.OutputPerMillionUsd >= 0), "AI budget rates and limits must be nonnegative.")
            .ValidateOnStart();

        services.Configure<FileStoreOptions>(config.GetSection(FileStoreOptions.SectionName));
        services.AddSingleton<IFileStore>(sp =>
        {
            var root = sp.GetRequiredService<IOptions<FileStoreOptions>>().Value.Root;
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            return new LocalFileStore(Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root));
        });

        services.Configure<AdminOptions>(config.GetSection(AdminOptions.SectionName));
        services.AddScoped<UserService>();
        services.AddScoped<Application.Catalog.UnitProgressService>();
        services.Configure<Application.Catalog.UnitOptions>(config.GetSection(Application.Catalog.UnitOptions.SectionName));
        services.AddScoped<Application.Catalog.UnitTestService>();
        services.Configure<Application.Premium.PremiumOptions>(config.GetSection(Application.Premium.PremiumOptions.SectionName));
        services.Configure<Application.Premium.PaymentOptions>(config.GetSection(Application.Premium.PaymentOptions.SectionName));
        services.AddScoped<Application.Premium.PremiumService>();
        services.AddScoped<Application.Teaching.GroupService>();
        services.AddScoped<Application.Teaching.TeacherService>();
        services.AddScoped<Application.Admin.AdminService>();
        services.Configure<Application.Catalog.CatalogOptions>(config.GetSection(Application.Catalog.CatalogOptions.SectionName));
        services.AddSingleton<Application.Catalog.ICatalogFileReader, Catalog.CatalogFileReader>();
        services.AddScoped<Application.Catalog.CatalogService>();
        services.AddScoped<Application.Catalog.CatalogAdminService>();
        services.AddScoped<Application.Catalog.CatalogDraftService>();
        services.AddScoped<Application.Catalog.TrapSceneService>();
        services.AddScoped<Application.Catalog.IntroductionService>();
        services.AddScoped<Application.Catalog.IntroductionAudioService>();
        services.Configure<Speech.ElevenLabsOptions>(config.GetSection("ElevenLabs"));
        services.AddHttpClient<Application.Catalog.IIntroductionNarrator, Speech.ElevenLabsNarrator>(c => c.Timeout = TimeSpan.FromSeconds(65)).RemoveAllLoggers();
        services.AddScoped<Application.Catalog.CatalogSeeder>();
        services.AddScoped<StudyService>();
        services.AddScoped<Application.Vocabulary.PersonalWordService>();
        services.AddSingleton<ISpacedRepetitionScheduler, LadderSchedulerV1>();

        services.AddSingleton(TelegramBotOptions.From(config));
        services.AddHttpClient<ITelegramMessenger, TelegramBotMessenger>(c =>
            {
                c.BaseAddress = new Uri("https://api.telegram.org/");
                c.Timeout = TimeSpan.FromSeconds(15);
            })
            .RemoveAllLoggers(); // request URLs contain the bot token (spec SEC05)
        services.AddScoped<ReminderService>();

        // AI (D-21): both services are registered; which one is used is chosen per learner in settings.
        services.Configure<AiOptions>(config.GetSection(AiOptions.SectionName));
        services.Configure<Application.Speech.SpeechOptions>(config.GetSection(Application.Speech.SpeechOptions.SectionName));
        services.PostConfigure<AiOptions>(o =>
        {
            foreach (var p in new[] { o.Anthropic, o.OpenAi })
            {
                p.ApiKey = AiProviderOptions.Clean(p.ApiKey);
                p.Model = AiProviderOptions.Clean(p.Model) ?? "";
            }
        });
        services.PostConfigure<Application.Speech.SpeechOptions>(o =>
        {
            o.Azure.Key = AiProviderOptions.Clean(o.Azure.Key);
            o.Azure.Region = AiProviderOptions.Clean(o.Azure.Region);
        });
        services.AddHttpClient<Speech.OpenAiSpeechSynthesizer>(c => c.Timeout = TimeSpan.FromSeconds(60)).RemoveAllLoggers();
        services.AddHttpClient<Speech.AzureSpeechSynthesizer>(c => c.Timeout = TimeSpan.FromSeconds(60)).RemoveAllLoggers();
        services.AddTransient<Application.Speech.ISpeechSynthesizer, Speech.SpeechSynthesizerChooser>();
        services.AddScoped<Application.Speech.SpeechService>();
        services.AddHttpClient<Application.Speech.IPronunciationAssessor, Speech.AzurePronunciationAssessor>(c => c.Timeout = TimeSpan.FromSeconds(60)).RemoveAllLoggers();
        services.AddScoped<Application.Speech.SpeakingService>();
        services.AddHttpClient<AnthropicProvider>(c => c.Timeout = TimeSpan.FromMinutes(5)).RemoveAllLoggers();
        services.AddHttpClient<OpenAiProvider>(c => c.Timeout = TimeSpan.FromMinutes(5)).RemoveAllLoggers();
        services.AddTransient<IAiProvider>(sp => sp.GetRequiredService<AnthropicProvider>());
        services.AddTransient<IAiProvider>(sp => sp.GetRequiredService<OpenAiProvider>());
        services.AddScoped<AiGateway>();
        services.AddScoped<SentenceCheckService>();
        services.AddScoped<Application.Writing.WritingService>();
        services.AddScoped<Application.Vocabulary.UsageService>();
        services.AddScoped<Application.Ladder.LadderService>();
        services.AddScoped<Application.Listening.ListeningService>();
        services.AddScoped<Application.Games.CrosswordService>();
        services.AddScoped<Application.Seo.SeoService>();
        services.AddScoped<Application.Testing.TestService>();
        services.AddScoped<Application.Testing.QuestionReportService>();
        services.AddScoped<Application.Users.SourceService>();
        services.AddScoped<Application.Testing.TestQuestionGenerator>();

        services.Configure<Backups.BackupOptions>(config.GetSection(Backups.BackupOptions.SectionName));
        services.AddScoped<Backups.BackupService>();
        services.AddScoped<ProgressService>();
        services.AddScoped<Application.Mistakes.MistakeService>();
        services.AddScoped<Application.Mistakes.WeakPointService>();
        return services;
    }
}

