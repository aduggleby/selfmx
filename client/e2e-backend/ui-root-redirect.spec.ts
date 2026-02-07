import { test, expect } from '@playwright/test'

test('GET / redirects to /ui/ (no redirect loop)', async ({ request }) => {
  const response = await request.get('/', { maxRedirects: 0 })
  expect(response.status()).toBeGreaterThanOrEqual(300)
  expect(response.status()).toBeLessThan(400)
  expect(response.headers()['location']).toBe('/ui/')
})

test('HEAD / redirects to /ui/', async ({ request }) => {
  const response = await request.head('/', { maxRedirects: 0 })
  expect(response.status()).toBeGreaterThanOrEqual(300)
  expect(response.status()).toBeLessThan(400)
  expect(response.headers()['location']).toBe('/ui/')
})

test('UI loads at / (via redirect)', async ({ page }) => {
  const response = await page.goto('/', { waitUntil: 'domcontentloaded' })
  expect(response, 'expected navigation to return a response').not.toBeNull()
  expect(response!.status()).toBe(200)
  await expect(page).toHaveURL(/\/ui\/?$/)
  await expect(page).toHaveTitle('SelfMX')
})

test('UI loads at /ui', async ({ page }) => {
  const response = await page.goto('/ui', { waitUntil: 'domcontentloaded' })
  expect(response, 'expected navigation to return a response').not.toBeNull()
  expect(response!.status()).toBe(200)
  await expect(page).toHaveURL(/\/ui\/?$/)
  await expect(page).toHaveTitle('SelfMX')
})
