import { test, expect, createMockDomain } from './fixtures';

test.describe('Authentication', () => {
  test.describe('Login Flow', () => {
    test('shows login page when not authenticated', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);
      await page.goto('/');

      await expect(page.getByRole('heading', { name: 'SelfMX' })).toBeVisible();
      await expect(page.getByPlaceholder('Admin password')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
    });

    test('successful login shows main app', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);
      apiMock.setDomains([]);
      await page.goto('/');

      await page.getByPlaceholder('Admin password').fill('test-password');
      await page.getByRole('button', { name: 'Sign in' }).click();

      // Should now see the main app with header
      await expect(page.getByText('Add your first domain')).toBeVisible();
    });

    test('invalid password shows error', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);
      apiMock.onLogin((password) => password === 'correct-password');
      await page.goto('/');

      await page.getByPlaceholder('Admin password').fill('wrong-password');
      await page.getByRole('button', { name: 'Sign in' }).click();

      await expect(page.getByText(/invalid/i)).toBeVisible();
      await expect(page.getByPlaceholder('Admin password')).toBeVisible();
    });

    test('empty password disables submit button', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);
      await page.goto('/');

      await expect(page.getByRole('button', { name: 'Sign in' })).toBeDisabled();
    });

    test('shows loading state during login', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);

      // Add delay to login response
      await page.route('**/v1/admin/login', async (route) => {
        await new Promise((r) => setTimeout(r, 500));
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ message: 'Login successful' }),
        });
      });

      await page.goto('/');
      await page.getByPlaceholder('Admin password').fill('test-password');
      await page.getByRole('button', { name: 'Sign in' }).click();

      await expect(page.getByRole('button', { name: 'Signing in...' })).toBeVisible();
    });

    test('form submits on Enter key', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);
      apiMock.setDomains([]);
      await page.goto('/');

      const passwordInput = page.getByPlaceholder('Admin password');
      await passwordInput.fill('test-password');
      await passwordInput.press('Enter');

      // Should now see the main app
      await expect(page.getByText('Add your first domain')).toBeVisible();
    });
  });

  test.describe('Session Persistence', () => {
    test('authenticated user sees main app directly', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(true);
      apiMock.setDomains([]);
      await page.goto('/');

      // Should skip login and show main app
      await expect(page.getByPlaceholder('Admin password')).not.toBeVisible();
      await expect(page.getByText('Add your first domain')).toBeVisible();
    });

    test('authenticated user sees main app on refresh', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);
      apiMock.setDomains([]);
      await page.goto('/');

      // Login
      await page.getByPlaceholder('Admin password').fill('test-password');
      await page.getByRole('button', { name: 'Sign in' }).click();
      await expect(page.getByText('Add your first domain')).toBeVisible();

      // Refresh should stay authenticated (mock maintains state)
      await page.reload();
      await expect(page.getByText('Add your first domain')).toBeVisible();
    });
  });

  test.describe('Logout Flow', () => {
    test('logout button is visible when authenticated', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(true);
      apiMock.setDomains([]);
      await page.goto('/');

      await expect(page.getByRole('button', { name: 'Logout' })).toBeVisible();
    });

    test('logout returns to login page', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(true);
      apiMock.setDomains([]);
      await page.goto('/');

      // Click logout
      await page.getByRole('button', { name: 'Logout' }).click();

      // Should see login page
      await expect(page.getByPlaceholder('Admin password')).toBeVisible();
    });
  });

  test.describe('Error Handling', () => {
    test('401 on API call redirects to login', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(true);
      apiMock.setDomains([createMockDomain({ id: '1', name: 'test.com' })]);

      // After first successful load, simulate session expiry
      let domainsCallCount = 0;
      await page.route('**/v1/domains?*', async (route) => {
        domainsCallCount++;
        if (domainsCallCount === 1) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              data: [createMockDomain({ id: '1', name: 'test.com' })],
              page: 1,
              limit: 10,
              total: 1,
            }),
          });
        } else {
          await route.fulfill({
            status: 401,
            contentType: 'application/json',
            body: JSON.stringify({ error: { message: 'Session expired' } }),
          });
        }
      });

      await page.goto('/');
      await expect(page.getByText('test.com')).toBeVisible();

      // Trigger a refresh to cause another API call
      await page.reload();

      // Should redirect to login after 401
      await expect(page.getByPlaceholder('Admin password')).toBeVisible();
    });

    test('rate limiting shows appropriate error', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);

      await page.route('**/v1/admin/login', async (route) => {
        await route.fulfill({
          status: 429,
          contentType: 'application/json',
          body: JSON.stringify({ error: { message: 'Too many requests. Please try again later.' } }),
        });
      });

      await page.goto('/');
      await page.getByPlaceholder('Admin password').fill('test');
      await page.getByRole('button', { name: 'Sign in' }).click();

      await expect(page.getByText(/too many/i)).toBeVisible();
    });

    test('server error shows appropriate message', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);

      await page.route('**/v1/admin/login', async (route) => {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: { message: 'Internal server error' } }),
        });
      });

      await page.goto('/');
      await page.getByPlaceholder('Admin password').fill('test');
      await page.getByRole('button', { name: 'Sign in' }).click();

      await expect(page.getByText(/error/i)).toBeVisible();
    });

    test('network error shows appropriate message', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);

      await page.route('**/v1/admin/login', async (route) => {
        await route.abort('connectionrefused');
      });

      await page.goto('/');
      await page.getByPlaceholder('Admin password').fill('test');
      await page.getByRole('button', { name: 'Sign in' }).click();

      await expect(page.getByText(/unreachable|failed|error/i)).toBeVisible();
    });
  });

  test.describe('Loading States', () => {
    test('shows loading during initial auth check', async ({ page }) => {
      await page.route('**/v1/admin/me', async (route) => {
        await new Promise((r) => setTimeout(r, 500));
        await route.fulfill({ status: 401 });
      });

      await page.goto('/');
      await expect(page.getByText('Loading...')).toBeVisible();

      // Eventually shows login
      await expect(page.getByPlaceholder('Admin password')).toBeVisible();
    });
  });

  test.describe('Theme Support', () => {
    test('login page respects theme', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);
      await page.goto('/');

      // Login page should have bg-background class
      const container = page.locator('.min-h-screen.bg-background');
      await expect(container).toBeVisible();
    });
  });

  test.describe('Full Login-Logout Cycle', () => {
    test('can login, use app, and logout', async ({ page, apiMock }) => {
      apiMock.setAuthenticated(false);
      apiMock.setDomains([]);
      await page.goto('/');

      // Start on login page
      await expect(page.getByPlaceholder('Admin password')).toBeVisible();

      // Login
      await page.getByPlaceholder('Admin password').fill('test-password');
      await page.getByRole('button', { name: 'Sign in' }).click();

      // See main app
      await expect(page.getByText('Add your first domain')).toBeVisible();

      // Add a domain
      await page.getByPlaceholder('example.com').fill('mytest.com');
      await page.getByRole('button', { name: 'Add' }).click();
      // Wait for navigation to detail page
      await expect(page).toHaveURL(/\/domains\/.+/);

      // Logout
      await page.getByRole('button', { name: 'Logout' }).click();

      // Back on login page
      await expect(page.getByPlaceholder('Admin password')).toBeVisible();
    });
  });
});
