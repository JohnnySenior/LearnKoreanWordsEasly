import { defineConfig } from '@playwright/test';

export default defineConfig({
    testDir: '.',
    testMatch: '*.spec.mjs',
    timeout: 180_000,
    workers: 1,
    use: {
        actionTimeout: 15_000,
        baseURL: process.env.LUGAT_BROWSER_URL || 'http://127.0.0.1:5248',
        viewport: { width: 390, height: 844 },
        trace: 'retain-on-failure',
        screenshot: 'only-on-failure',
    },
    projects: [
        { name: 'phone-light', use: { colorScheme: 'light' } },
        { name: 'phone-dark', use: { colorScheme: 'dark' } },
    ],
    webServer: process.env.LUGAT_BROWSER_URL ? undefined : {
        command: 'dotnet run --no-build -c Release --project ../../src/Lugat.Api --no-launch-profile',
        url: 'http://127.0.0.1:5248/health',
        timeout: 120_000,
        reuseExistingServer: false,
        env: {
            ASPNETCORE_ENVIRONMENT: 'Development',
            ASPNETCORE_URLS: 'http://127.0.0.1:5248',
            ConnectionStrings__Default: process.env.LUGAT_BROWSER_POSTGRES || 'Host=localhost;Database=lugat_browser;Username=lugat;Password=lugat',
            Telegram__BotToken: '',
            TelegramLogin__BotToken: '',
            Google__ClientId: '',
            Backup__Enabled: 'false',
            Catalog__SeedSample: process.env.Catalog__SeedSample || 'true',
        },
    },
});
