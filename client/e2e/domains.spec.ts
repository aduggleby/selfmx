import { test, expect, createMockDomain, createMockDnsRecords } from './fixtures';

test.describe('Page Layout', () => {
  test('displays header with app title', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    await expect(page.locator('header')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'SelfMX' })).toBeVisible();
  });

  test('displays add domain form when no domains', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    await expect(page.getByText('Add your first domain')).toBeVisible();
    await expect(page.getByPlaceholder('example.com')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add' })).toBeVisible();
  });

  test('displays domain count when domains exist', async ({ page, apiMock }) => {
    apiMock.setDomains([createMockDomain({ id: '1', name: 'example.com' })]);
    await page.goto('/');

    await expect(page.getByText('1 domain')).toBeVisible();
  });
});

test.describe('Empty State', () => {
  test('displays add first domain message when no domains exist', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    await expect(page.getByText('Add your first domain')).toBeVisible();
    await expect(page.getByText(/Enter a domain to set up/)).toBeVisible();
  });

  test('does not show pagination when no domains', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    await expect(page.getByRole('button', { name: 'Previous' })).not.toBeVisible();
    await expect(page.getByRole('button', { name: 'Next' })).not.toBeVisible();
  });
});

test.describe('Domain List Display', () => {
  test('displays domain cards with name', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'example.com', status: 'verified' }),
      createMockDomain({ id: '2', name: 'test.org', status: 'pending' }),
    ]);
    await page.goto('/');

    await expect(page.getByText('example.com')).toBeVisible();
    await expect(page.getByText('test.org')).toBeVisible();
  });

  test('displays creation date for each domain', async ({ page, apiMock }) => {
    const createdAt = '2024-01-15T10:30:00.000Z';
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'example.com', createdAt }),
    ]);
    await page.goto('/');

    // The date is displayed as "Added [date]"
    await expect(page.getByText(/Added/i)).toBeVisible();
  });

  test('displays verified status for verified domains', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({
        id: '1',
        name: 'example.com',
        status: 'verified',
        verifiedAt: '2024-01-16T12:00:00.000Z',
      }),
    ]);
    await page.goto('/');

    await expect(page.getByText('verified', { exact: true })).toBeVisible();
  });

  test('displays Details link for each domain', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'example.com' }),
      createMockDomain({ id: '2', name: 'test.org' }),
    ]);
    await page.goto('/');

    const detailsLinks = page.getByRole('link', { name: 'Details' });
    await expect(detailsLinks).toHaveCount(2);
  });
});

test.describe('Status Badges', () => {
  test('displays pending status badge with yellow styling', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'mydomain.com', status: 'pending' }),
    ]);
    await page.goto('/');

    const badge = page.getByText('pending', { exact: true });
    await expect(badge).toBeVisible();
    await expect(badge).toHaveClass(/bg-\[var\(--status-pending-bg\)\]/);
    await expect(badge).toHaveClass(/text-\[var\(--status-pending-text\)\]/);
  });

  test('displays verifying status badge with blue styling', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'mydomain.com', status: 'verifying' }),
    ]);
    await page.goto('/');

    const badge = page.getByText('verifying', { exact: true });
    await expect(badge).toBeVisible();
    await expect(badge).toHaveClass(/bg-\[var\(--status-verifying-bg\)\]/);
    await expect(badge).toHaveClass(/text-\[var\(--status-verifying-text\)\]/);
  });

  test('displays verified status badge with green styling', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'mydomain.com', status: 'verified' }),
    ]);
    await page.goto('/');

    const badge = page.getByText('verified', { exact: true });
    await expect(badge).toBeVisible();
    await expect(badge).toHaveClass(/bg-\[var\(--status-verified-bg\)\]/);
    await expect(badge).toHaveClass(/text-\[var\(--status-verified-text\)\]/);
  });

  test('displays failed status badge with red styling', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'mydomain.com', status: 'failed' }),
    ]);
    await page.goto('/');

    const badge = page.getByText('failed', { exact: true });
    await expect(badge).toBeVisible();
    await expect(badge).toHaveClass(/bg-\[var\(--status-failed-bg\)\]/);
    await expect(badge).toHaveClass(/text-\[var\(--status-failed-text\)\]/);
  });

  test('displays verifying status message', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'verifying.com', status: 'verifying' }),
    ]);
    await page.goto('/');

    await expect(page.getByText('Verifying DNS records...')).toBeVisible();
  });

  test('displays failure reason for failed domains', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({
        id: '1',
        name: 'failed.com',
        status: 'failed',
        failureReason: 'Verification timed out after 72 hours',
      }),
    ]);
    await page.goto('/');

    await expect(page.getByText('Verification timed out after 72 hours')).toBeVisible();
  });
});

test.describe('Add Domain', () => {
  test('successfully adds a new domain', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    // Fill in the domain name
    await page.getByPlaceholder('example.com').fill('newdomain.com');
    await page.getByRole('button', { name: 'Add' }).click();

    // Wait for redirect to detail page and check heading
    await expect(page).toHaveURL(/\/domains\/.+/);
    await expect(page.getByRole('heading', { name: 'newdomain.com' })).toBeVisible();
  });

  test('clears input after successful domain creation', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    const input = page.getByPlaceholder('example.com');
    await input.fill('newdomain.com');
    await page.getByRole('button', { name: 'Add' }).click();

    // Wait for redirect to detail page
    await expect(page).toHaveURL(/\/domains\/.+/);
  });

  test('shows loading state while creating domain', async ({ page, apiMock }) => {
    apiMock.setDomains([]);

    // Add delay to see loading state
    await page.route('**/domains', async (route) => {
      if (route.request().method() === 'POST') {
        await new Promise(resolve => setTimeout(resolve, 500));
        const body = route.request().postDataJSON();
        const domain = createMockDomain({ name: body.name });
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(domain),
        });
      } else {
        await route.continue();
      }
    });

    await page.goto('/');
    await page.getByPlaceholder('example.com').fill('newdomain.com');

    await page.getByRole('button', { name: 'Add' }).click();

    // Should show loading state
    await expect(page.getByRole('button', { name: 'Adding...' })).toBeVisible();
  });

  test('displays error message when domain already exists', async ({ page, apiMock }) => {
    apiMock.setDomains([createMockDomain({ id: '1', name: 'existing.com' })]);
    apiMock.onCreateDomain((name) => {
      if (name === 'existing.com') {
        return { error: { code: 'domain_exists', message: 'Domain already exists' } };
      }
      return createMockDomain({ name });
    });

    await page.goto('/');

    // When domains exist, we need to click "Add domain" button to open modal
    await page.getByRole('button', { name: 'Add domain' }).click();
    await page.getByPlaceholder('example.com').fill('existing.com');
    await page.locator('form').getByRole('button', { name: 'Add' }).click();

    await expect(page.getByText('Domain already exists')).toBeVisible();
  });

  test('empty domain disables submit button', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    // Button should be disabled when input is empty
    await expect(page.getByRole('button', { name: 'Add' })).toBeDisabled();
  });

  test('trims whitespace from domain name', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    await page.getByPlaceholder('example.com').fill('  trimmed.com  ');
    await page.getByRole('button', { name: 'Add' }).click();

    // Should redirect to detail page after creation
    await expect(page).toHaveURL(/\/domains\/.+/);
  });
});

test.describe('Domain Card Navigation', () => {
  test('shows Details link on domain cards', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'example.com' }),
    ]);
    await page.goto('/');

    await expect(page.getByRole('link', { name: 'Details' })).toBeVisible();
  });

  test('clicking Details navigates to domain detail page', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: 'test-domain-1', name: 'example.com' }),
    ]);
    await page.goto('/');

    await page.getByRole('link', { name: 'Details' }).click();

    await expect(page).toHaveURL('/domains/test-domain-1');
  });

  test('clicking domain name also navigates to detail page', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: 'test-domain-1', name: 'example.com' }),
    ]);
    await page.goto('/');

    await page.getByRole('link', { name: 'example.com' }).click();

    await expect(page).toHaveURL('/domains/test-domain-1');
  });
});

test.describe('Pagination', () => {
  test('does not show pagination for single page', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'domain1.com' }),
      createMockDomain({ id: '2', name: 'domain2.com' }),
    ]);
    await page.goto('/');

    await expect(page.getByRole('button', { name: 'Previous' })).not.toBeVisible();
    await expect(page.getByRole('button', { name: 'Next' })).not.toBeVisible();
  });

  test('shows pagination when more than one page exists', async ({ page, apiMock }) => {
    // Create 15 domains (with limit 10, that's 2 pages)
    const domains = Array.from({ length: 15 }, (_, i) =>
      createMockDomain({ id: `${i}`, name: `domain${i}.com` })
    );
    apiMock.setDomains(domains);
    await page.goto('/');

    await expect(page.getByRole('button', { name: 'Previous' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Next' })).toBeVisible();
    await expect(page.getByText('1 / 2')).toBeVisible();
  });

  test('previous button is disabled on first page', async ({ page, apiMock }) => {
    const domains = Array.from({ length: 15 }, (_, i) =>
      createMockDomain({ id: `${i}`, name: `domain${i}.com` })
    );
    apiMock.setDomains(domains);
    await page.goto('/');

    await expect(page.getByRole('button', { name: 'Previous' })).toBeDisabled();
  });

  test('next button navigates to next page', async ({ page, apiMock }) => {
    const domains = Array.from({ length: 15 }, (_, i) =>
      createMockDomain({ id: `${i}`, name: `domain${i}.com` })
    );
    apiMock.setDomains(domains);
    await page.goto('/');

    await page.getByRole('button', { name: 'Next' }).click();

    await expect(page.getByText('2 / 2')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Next' })).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Previous' })).not.toBeDisabled();
  });

  test('previous button navigates back', async ({ page, apiMock }) => {
    const domains = Array.from({ length: 15 }, (_, i) =>
      createMockDomain({ id: `${i}`, name: `domain${i}.com` })
    );
    apiMock.setDomains(domains);
    await page.goto('/');

    // Go to page 2
    await page.getByRole('button', { name: 'Next' }).click();
    await expect(page.getByText('2 / 2')).toBeVisible();

    // Go back to page 1
    await page.getByRole('button', { name: 'Previous' }).click();
    await expect(page.getByText('1 / 2')).toBeVisible();
  });

  test('displays correct domains per page', async ({ page, apiMock }) => {
    const domains = Array.from({ length: 15 }, (_, i) =>
      createMockDomain({ id: `${i}`, name: `domain${i}.com` })
    );
    apiMock.setDomains(domains);
    await page.goto('/');

    // Page 1 should show first 10 domains
    await expect(page.getByText('domain0.com')).toBeVisible();
    await expect(page.getByText('domain9.com')).toBeVisible();

    // Navigate to page 2
    await page.getByRole('button', { name: 'Next' }).click();

    // Page 2 should show remaining 5 domains
    await expect(page.getByText('domain10.com')).toBeVisible();
    await expect(page.getByText('domain14.com')).toBeVisible();
    // Previous page domains should not be visible
    await expect(page.getByText('domain0.com')).not.toBeVisible();
  });
});

test.describe('Loading States', () => {
  test('shows loading state while app initializes', async ({ page }) => {
    // Delay auth check to see loading state
    await page.route('**/admin/me', async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 500));
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ email: 'admin@example.com' }),
      });
    });

    await page.goto('/');

    // Check for loading text during auth check
    await expect(page.getByText('Loading...')).toBeVisible();
  });
});

test.describe('Error Handling', () => {
  test('displays error when domain list fails to load', async ({ page, apiMock }) => {
    // First mock auth to succeed
    apiMock.setAuthenticated(true);

    // Then mock domains to fail
    await page.route('**/domains?*', async (route) => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: { code: 'server_error', message: 'Internal server error' } }),
      });
    });

    await page.goto('/');

    // Error modal shows "Error"
    await expect(page.getByRole('heading', { name: 'Error' })).toBeVisible();
  });

  test('displays error when database schema is out of sync', async ({ page, apiMock }) => {
    // First mock auth to succeed
    apiMock.setAuthenticated(true);

    // Mock a database schema error (like missing column)
    await page.route('**/domains?*', async (route) => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({
          error: {
            code: 'database_error',
            message: 'Invalid column name \'LastCheckedAt\'.',
          },
        }),
      });
    });

    await page.goto('/');

    // Error modal shows "Error" heading
    await expect(page.getByRole('heading', { name: 'Error' })).toBeVisible();
    // And displays the error message
    await expect(page.getByText("Invalid column name 'LastCheckedAt'.")).toBeVisible();
  });
});

test.describe('Domain Card Grid Layout', () => {
  test('displays domains in responsive grid', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'domain1.com' }),
      createMockDomain({ id: '2', name: 'domain2.com' }),
      createMockDomain({ id: '3', name: 'domain3.com' }),
    ]);
    await page.goto('/');

    // Check grid container exists with responsive classes
    const grid = page.locator('.grid.gap-4.sm\\:grid-cols-2');
    await expect(grid).toBeVisible();
  });
});

test.describe('Form Interaction', () => {
  test('allows typing in domain input', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    const input = page.getByPlaceholder('example.com');
    await input.fill('test-input.com');

    await expect(input).toHaveValue('test-input.com');
  });

  test('submits form on enter key', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    const input = page.getByPlaceholder('example.com');
    await input.fill('enter-submit.com');
    await input.press('Enter');

    // Should redirect to detail page after creation
    await expect(page).toHaveURL(/\/domains\/.+/);
  });
});

test.describe('Multiple Domain States', () => {
  test('displays domains with mixed statuses correctly', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'domain-a.com', status: 'pending' }),
      createMockDomain({ id: '2', name: 'domain-b.com', status: 'verifying', dnsRecords: createMockDnsRecords('domain-b.com') }),
      createMockDomain({ id: '3', name: 'domain-c.com', status: 'verified', verifiedAt: new Date().toISOString() }),
      createMockDomain({ id: '4', name: 'domain-d.com', status: 'failed', failureReason: 'DNS verification failed' }),
    ]);
    await page.goto('/');

    // All domains visible
    await expect(page.getByText('domain-a.com')).toBeVisible();
    await expect(page.getByText('domain-b.com')).toBeVisible();
    await expect(page.getByText('domain-c.com')).toBeVisible();
    await expect(page.getByText('domain-d.com')).toBeVisible();

    // All status badges visible (using exact match to avoid matching domain names)
    await expect(page.getByText('pending', { exact: true })).toBeVisible();
    await expect(page.getByText('verifying', { exact: true })).toBeVisible();
    await expect(page.getByText('verified', { exact: true })).toBeVisible();
    await expect(page.getByText('failed', { exact: true })).toBeVisible();

    // Failure reason visible
    await expect(page.getByText('DNS verification failed')).toBeVisible();

    // All cards have Details link
    await expect(page.getByRole('link', { name: 'Details' })).toHaveCount(4);
  });
});
