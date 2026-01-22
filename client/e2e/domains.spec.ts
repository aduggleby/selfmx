import { test, expect, createMockDomain, createMockDnsRecords } from './fixtures';

test.describe('Page Layout', () => {
  test('displays header with app title', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    await expect(page.locator('header')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'SelfMX' })).toBeVisible();
  });

  test('displays main domains heading', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    await expect(page.getByRole('heading', { name: 'Domains' })).toBeVisible();
  });

  test('displays add domain form', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    await expect(page.getByRole('heading', { name: 'Add Domain' })).toBeVisible();
    await expect(page.getByPlaceholder('example.com')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add Domain' })).toBeVisible();
  });
});

test.describe('Empty State', () => {
  test('displays empty state message when no domains exist', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    await expect(page.getByText('No domains yet. Add your first domain above.')).toBeVisible();
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

    await expect(page.getByText(/Created:/)).toBeVisible();
  });

  test('displays verified date for verified domains', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({
        id: '1',
        name: 'example.com',
        status: 'verified',
        verifiedAt: '2024-01-16T12:00:00.000Z',
      }),
    ]);
    await page.goto('/');

    await expect(page.getByText(/Verified:/)).toBeVisible();
  });

  test('displays delete button for each domain', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'example.com' }),
      createMockDomain({ id: '2', name: 'test.org' }),
    ]);
    await page.goto('/');

    const deleteButtons = page.getByRole('button', { name: 'Delete' });
    await expect(deleteButtons).toHaveCount(2);
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

    const badgeText = page.getByText('verifying', { exact: true });
    await expect(badgeText).toBeVisible();
    // For verifying status, the text is wrapped in a relative span, so check parent for styling
    const badge = badgeText.locator('..');
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

    await expect(page.getByText('DNS records are being verified. This may take a few minutes.')).toBeVisible();
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
    await page.getByRole('button', { name: 'Add Domain' }).click();

    // Wait for the domain to appear (use heading to avoid matching toast)
    await expect(page.getByRole('heading', { name: 'newdomain.com' })).toBeVisible();
    await expect(page.getByText('No domains yet')).not.toBeVisible();
  });

  test('clears input after successful domain creation', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    const input = page.getByPlaceholder('example.com');
    await input.fill('newdomain.com');
    await page.getByRole('button', { name: 'Add Domain' }).click();

    // Input should be cleared
    await expect(input).toHaveValue('');
  });

  test('shows loading state while creating domain', async ({ page, apiMock }) => {
    apiMock.setDomains([]);

    // Add delay to see loading state
    await page.route('**/v1/domains', async (route) => {
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

    const addButton = page.getByRole('button', { name: 'Add Domain' });
    await addButton.click();

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
    await page.getByPlaceholder('example.com').fill('existing.com');
    await page.getByRole('button', { name: 'Add Domain' }).click();

    await expect(page.getByText('Domain already exists')).toBeVisible();
  });

  test('does not submit empty domain name', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    // Try to submit with empty input
    await page.getByRole('button', { name: 'Add Domain' }).click();

    // Should still show empty state
    await expect(page.getByText('No domains yet')).toBeVisible();
  });

  test('trims whitespace from domain name', async ({ page, apiMock }) => {
    apiMock.setDomains([]);
    await page.goto('/');

    await page.getByPlaceholder('example.com').fill('  trimmed.com  ');
    await page.getByRole('button', { name: 'Add Domain' }).click();

    await expect(page.getByRole('heading', { name: 'trimmed.com' })).toBeVisible();
  });
});

test.describe('Delete Domain', () => {
  test('successfully deletes a domain', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'todelete.com' }),
    ]);
    await page.goto('/');

    // Confirm domain is visible
    await expect(page.getByText('todelete.com')).toBeVisible();

    // Click delete
    await page.getByRole('button', { name: 'Delete' }).click();

    // Domain should be removed (optimistic update)
    await expect(page.getByText('todelete.com')).not.toBeVisible();
    await expect(page.getByText('No domains yet')).toBeVisible();
  });

  test('removes domain from list after delete (optimistic update)', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'optimistic-delete.com' }),
    ]);
    await page.goto('/');

    // Confirm domain exists
    await expect(page.getByText('optimistic-delete.com')).toBeVisible();

    // Click delete
    await page.getByRole('button', { name: 'Delete' }).click();

    // Domain removed immediately (optimistic update)
    await expect(page.getByText('optimistic-delete.com')).not.toBeVisible();
  });

  test('deletes correct domain when multiple exist', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'keep-this.com' }),
      createMockDomain({ id: '2', name: 'delete-this.com' }),
    ]);
    await page.goto('/');

    // Find and click delete for the second domain
    const domainCard = page.locator('[class*="card"]').filter({ hasText: 'delete-this.com' });
    await domainCard.getByRole('button', { name: 'Delete' }).click();

    // First domain should remain
    await expect(page.getByText('keep-this.com')).toBeVisible();
    // Second domain should be gone
    await expect(page.getByText('delete-this.com')).not.toBeVisible();
  });
});

test.describe('DNS Records', () => {
  test('shows DNS toggle button for domains with records', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({
        id: '1',
        name: 'withdns.com',
        status: 'verifying',
        dnsRecords: createMockDnsRecords('withdns.com'),
      }),
    ]);
    await page.goto('/');

    await expect(page.getByRole('button', { name: 'Show DNS Records' })).toBeVisible();
  });

  test('does not show DNS toggle for domains without records', async ({ page, apiMock }) => {
    apiMock.setDomains([
      createMockDomain({ id: '1', name: 'nodns.com', dnsRecords: null }),
    ]);
    await page.goto('/');

    await expect(page.getByRole('button', { name: 'Show DNS Records' })).not.toBeVisible();
  });

  test('toggles DNS records visibility', async ({ page, apiMock }) => {
    const dnsRecords = createMockDnsRecords('example.com');
    apiMock.setDomains([
      createMockDomain({
        id: '1',
        name: 'example.com',
        status: 'verifying',
        dnsRecords,
      }),
    ]);
    await page.goto('/');

    // Initially hidden
    await expect(page.getByText('token1._domainkey.example.com')).not.toBeVisible();

    // Click to show
    await page.getByRole('button', { name: 'Show DNS Records' }).click();
    await expect(page.getByText('token1._domainkey.example.com')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Hide DNS Records' })).toBeVisible();

    // Click to hide
    await page.getByRole('button', { name: 'Hide DNS Records' }).click();
    // Wait for animation to complete (200ms transition + buffer)
    await page.waitForTimeout(300);
    await expect(page.getByText('token1._domainkey.example.com')).not.toBeVisible();
    await expect(page.getByRole('button', { name: 'Show DNS Records' })).toBeVisible();
  });

  test('displays DNS records table with correct columns', async ({ page, apiMock }) => {
    const dnsRecords = createMockDnsRecords('example.com');
    apiMock.setDomains([
      createMockDomain({
        id: '1',
        name: 'example.com',
        dnsRecords,
      }),
    ]);
    await page.goto('/');

    await page.getByRole('button', { name: 'Show DNS Records' }).click();

    // Check table headers
    await expect(page.getByRole('columnheader', { name: 'Type' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Name' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Value' })).toBeVisible();
  });

  test('displays all DNS records in table', async ({ page, apiMock }) => {
    const dnsRecords = createMockDnsRecords('example.com');
    apiMock.setDomains([
      createMockDomain({
        id: '1',
        name: 'example.com',
        dnsRecords,
      }),
    ]);
    await page.goto('/');

    await page.getByRole('button', { name: 'Show DNS Records' }).click();

    // Check all 3 DNS records are displayed
    await expect(page.getByText('token1._domainkey.example.com')).toBeVisible();
    await expect(page.getByText('token2._domainkey.example.com')).toBeVisible();
    await expect(page.getByText('token3._domainkey.example.com')).toBeVisible();

    // Check record type badges
    const cnameLabels = page.locator('code').filter({ hasText: 'CNAME' });
    await expect(cnameLabels).toHaveCount(3);

    // Check values
    await expect(page.getByText('token1.dkim.amazonses.com')).toBeVisible();
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
    await expect(page.getByText('Page 1 of 2')).toBeVisible();
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

    await expect(page.getByText('Page 2 of 2')).toBeVisible();
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
    await expect(page.getByText('Page 2 of 2')).toBeVisible();

    // Go back to page 1
    await page.getByRole('button', { name: 'Previous' }).click();
    await expect(page.getByText('Page 1 of 2')).toBeVisible();
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
  test('shows skeleton loaders while fetching domains', async ({ page }) => {
    // Add delay to domain list response
    await page.route('**/v1/domains?*', async (route) => {
      await new Promise(resolve => setTimeout(resolve, 1000));
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: [], page: 1, limit: 10, total: 0 }),
      });
    });

    await page.goto('/');

    // Check for skeleton loaders (they use shimmer animation with gradient)
    await expect(page.locator('[class*="animate-"]').first()).toBeVisible();
  });
});

test.describe('Error Handling', () => {
  test('displays error when domain list fails to load', async ({ page }) => {
    await page.route('**/v1/domains?*', async (route) => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: { code: 'server_error', message: 'Internal server error' } }),
      });
    });

    await page.goto('/');

    await expect(page.getByText(/Error loading domains/)).toBeVisible();
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
    const grid = page.locator('.grid.gap-4');
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

    // Check for the domain card heading (not the toast)
    await expect(page.getByRole('heading', { name: 'enter-submit.com' })).toBeVisible();
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

    // All domains visible (use heading to avoid matching DNS records)
    await expect(page.getByRole('heading', { name: 'domain-a.com' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'domain-b.com' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'domain-c.com' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'domain-d.com' })).toBeVisible();

    // All status badges visible (using exact match to avoid matching domain names)
    await expect(page.getByText('pending', { exact: true })).toBeVisible();
    await expect(page.getByText('verifying', { exact: true })).toBeVisible();
    await expect(page.getByText('verified', { exact: true })).toBeVisible();
    await expect(page.getByText('failed', { exact: true })).toBeVisible();

    // Failure reason visible
    await expect(page.getByText('DNS verification failed')).toBeVisible();

    // DNS records button only for verifying domain
    await expect(page.getByRole('button', { name: 'Show DNS Records' })).toHaveCount(1);
  });
});
