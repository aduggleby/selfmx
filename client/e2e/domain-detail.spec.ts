import { test, expect, createMockDomain, createMockDnsRecords } from './fixtures';

test.describe('Domain Detail Page', () => {
  test.describe('Navigation', () => {
    test('navigates to domain detail page when clicking domain name', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-domain-1',
        name: 'example.com',
        status: 'verified',
      });
      apiMock.setDomains([domain]);
      await page.goto('/');

      await page.getByRole('link', { name: 'example.com' }).click();

      await expect(page).toHaveURL('/domains/test-domain-1');
      await expect(page.getByText('example.com')).toBeVisible();
    });

    test('redirects to domain detail page after creating a domain', async ({ page, apiMock }) => {
      apiMock.setDomains([]);
      await page.goto('/');

      await page.getByPlaceholder('example.com').fill('newdomain.com');
      await page.getByRole('button', { name: 'Add' }).click();

      // Should redirect to detail page
      await expect(page).toHaveURL(/\/domains\/.+/);
      await expect(page.getByRole('heading', { name: 'newdomain.com' })).toBeVisible();
    });

    test('shows back link to domains list', async ({ page, apiMock }) => {
      const domain = createMockDomain({ id: 'test-1', name: 'example.com' });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      const backLink = page.getByRole('link', { name: 'Back' });
      await expect(backLink).toBeVisible();

      await backLink.click();
      await expect(page).toHaveURL('/');
    });

    test('redirects to home for unknown routes', async ({ page, apiMock }) => {
      apiMock.setDomains([]);
      await page.goto('/unknown-route');

      await expect(page).toHaveURL('/');
    });
  });

  test.describe('Domain Information Display', () => {
    test('displays domain name and status', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'example.com',
        status: 'verified',
        verifiedAt: '2024-01-16T12:00:00.000Z',
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await expect(page.getByText('example.com')).toBeVisible();
      await expect(page.getByText('verified', { exact: true })).toBeVisible();
    });

    test('displays creation date', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'example.com',
        createdAt: '2024-01-15T10:30:00.000Z',
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await expect(page.getByText(/Added/)).toBeVisible();
    });

    test('displays verification date for verified domains', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'example.com',
        status: 'verified',
        verifiedAt: '2024-01-16T12:00:00.000Z',
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await expect(page.getByText(/Verified/)).toBeVisible();
    });

    test('displays verifying status message', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'verifying.com',
        status: 'verifying',
        dnsRecords: createMockDnsRecords('verifying.com'),
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await expect(page.getByText('DNS records are being verified')).toBeVisible();
    });

    test('displays pending status message', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'pending.com',
        status: 'pending',
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await expect(page.getByText('Waiting for DNS setup to begin')).toBeVisible();
    });

    test('displays failure reason for failed domains', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'failed.com',
        status: 'failed',
        failureReason: 'DNS verification timed out',
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await expect(page.getByText('Verification Failed')).toBeVisible();
      await expect(page.getByText('DNS verification timed out')).toBeVisible();
    });
  });

  test.describe('DNS Records Display', () => {
    test('displays DNS records table', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'example.com',
        status: 'verifying',
        dnsRecords: createMockDnsRecords('example.com'),
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      // DNS records should be visible directly (not hidden behind a toggle)
      await expect(page.getByRole('columnheader', { name: 'Type' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Name' })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Value' })).toBeVisible();

      // Check records are displayed
      await expect(page.getByText('token1._domainkey.example.com')).toBeVisible();
      await expect(page.getByText('token1.dkim.amazonses.com')).toBeVisible();
    });

    test('shows empty message when no DNS records', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'example.com',
        status: 'verified',
        dnsRecords: null,
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await expect(page.getByText('No DNS records available yet')).toBeVisible();
    });
  });

  test.describe('DNS Actions', () => {
    test('shows Download button', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'example.com',
        dnsRecords: createMockDnsRecords('example.com'),
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await expect(page.getByRole('button', { name: 'Download' })).toBeVisible();
    });

    test('shows Cloudflare button', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'example.com',
        dnsRecords: createMockDnsRecords('example.com'),
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await expect(page.getByRole('button', { name: 'Cloudflare' })).toBeVisible();
    });

    test('does not show DNS action buttons when no records', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'example.com',
        status: 'verified',
        dnsRecords: null,
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      // DNS action buttons should not be visible when there are no records
      await expect(page.getByRole('button', { name: 'Download' })).not.toBeVisible();
      await expect(page.getByRole('button', { name: 'Cloudflare' })).not.toBeVisible();
    });

    test('downloads BIND file when clicking Download', async ({ page, apiMock }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'example.com',
        dnsRecords: createMockDnsRecords('example.com'),
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      // Listen for download
      const downloadPromise = page.waitForEvent('download');
      await page.getByRole('button', { name: 'Download' }).click();
      const download = await downloadPromise;

      expect(download.suggestedFilename()).toBe('example.com-dns-records.txt');
    });

    test('opens Cloudflare in new tab when clicking Cloudflare button', async ({ page, apiMock, context }) => {
      const domain = createMockDomain({
        id: 'test-1',
        name: 'example.com',
        dnsRecords: createMockDnsRecords('example.com'),
      });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      // Listen for new page/tab
      const pagePromise = context.waitForEvent('page');
      await page.getByRole('button', { name: 'Cloudflare' }).click();
      const newPage = await pagePromise;

      expect(newPage.url()).toContain('dash.cloudflare.com');
      expect(newPage.url()).toContain('example.com');
      expect(newPage.url()).toContain('/dns');
    });
  });

  test.describe('Delete Domain', () => {
    test('shows delete button', async ({ page, apiMock }) => {
      const domain = createMockDomain({ id: 'test-1', name: 'example.com' });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await expect(page.getByRole('button', { name: 'Delete' })).toBeVisible();
    });

    test('clicking delete opens confirmation modal', async ({ page, apiMock }) => {
      const domain = createMockDomain({ id: 'test-1', name: 'example.com' });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await page.getByRole('button', { name: 'Delete' }).click();

      // Confirmation modal should appear
      await expect(page.getByRole('heading', { name: 'Delete domain' })).toBeVisible();
      await expect(page.getByText('This action cannot be undone')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Cancel' })).toBeVisible();
    });

    test('canceling delete closes modal without deleting', async ({ page, apiMock }) => {
      const domain = createMockDomain({ id: 'test-1', name: 'example.com' });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await page.getByRole('button', { name: 'Delete' }).click();
      await expect(page.getByRole('heading', { name: 'Delete domain' })).toBeVisible();

      await page.getByRole('button', { name: 'Cancel' }).click();

      // Modal should close
      await expect(page.getByRole('heading', { name: 'Delete domain' })).not.toBeVisible();
      // Should still be on detail page
      await expect(page).toHaveURL('/domains/test-1');
    });

    test('confirming delete removes domain and redirects to home', async ({ page, apiMock }) => {
      const domain = createMockDomain({ id: 'test-1', name: 'example.com' });
      apiMock.setDomains([domain]);
      await page.goto('/domains/test-1');

      await page.getByRole('button', { name: 'Delete' }).click();
      await expect(page.getByRole('heading', { name: 'Delete domain' })).toBeVisible();

      // Click the destructive Delete button in the modal
      const modal = page.locator('.fixed.inset-0');
      await modal.getByRole('button', { name: 'Delete' }).click();

      // Should redirect to home after deletion
      await expect(page).toHaveURL('/');
      await expect(page.getByText('Domain deleted')).toBeVisible();
    });
  });

  test.describe('Error States', () => {
    test('shows error when domain not found', async ({ page, apiMock }) => {
      // Don't set any domains - the mock will return 404 for unknown IDs
      apiMock.setDomains([]);
      await page.goto('/');
      await page.evaluate(() => {
        window.history.pushState({}, '', '/domains/non-existent');
        window.dispatchEvent(new PopStateEvent('popstate'));
      });

      await expect(page.getByRole('heading', { name: 'Domain not found' })).toBeVisible();
      await expect(page.getByRole('link', { name: 'Back' })).toBeVisible();
    });

    test('back link works on error page', async ({ page, apiMock }) => {
      apiMock.setDomains([]);
      await page.goto('/');
      await page.evaluate(() => {
        window.history.pushState({}, '', '/domains/non-existent');
        window.dispatchEvent(new PopStateEvent('popstate'));
      });

      await expect(page.getByRole('heading', { name: 'Domain not found' })).toBeVisible();
      await page.getByRole('link', { name: 'Back' }).click();
      await expect(page).toHaveURL('/');
    });
  });
});
