import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e-backend',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? 'list' : 'html',
  outputDir: process.env.CI ? '/tmp/test-results' : 'test-results',
  preserveOutput: process.env.CI ? 'never' : 'always',
  use: {
    baseURL: 'http://127.0.0.1:17400',
    trace: process.env.CI ? 'off' : 'on-first-retry',
    screenshot: process.env.CI ? 'off' : 'only-on-failure',
    video: process.env.CI ? 'off' : 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    // Build UI for /ui and copy to backend wwwroot, then run backend in Test env (SQLite).
    command:
      'bash -lc "npm run build && rm -rf ../src/SelfMX.Api/wwwroot && mkdir -p ../src/SelfMX.Api/wwwroot && cp -a dist/. ../src/SelfMX.Api/wwwroot/ && ASPNETCORE_ENVIRONMENT=Test dotnet run --no-launch-profile --project ../src/SelfMX.Api --urls http://127.0.0.1:17400"',
    url: 'http://127.0.0.1:17400/health',
    timeout: 120_000,
    reuseExistingServer: !process.env.CI,
  },
})

