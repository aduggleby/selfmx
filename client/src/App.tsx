import { useState, useEffect } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { DomainsPage } from './pages/DomainsPage';
import { ThemeProvider } from './components/theme-provider';
import { ThemeToggle } from './components/ui/theme-toggle';
import { Toaster } from './components/ui/toaster';
import { api } from './lib/api';
import { cn } from './lib/utils';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000,
      retry: 2,
      refetchOnWindowFocus: false,
    },
  },
});

// Set API key from environment or prompt
const apiKey = import.meta.env.VITE_API_KEY;
if (apiKey) {
  api.setApiKey(apiKey);
}

function App() {
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const handleScroll = () => setScrolled(window.scrollY > 10);
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <div className="relative min-h-screen">
          <header
            className={cn(
              'sticky top-0 z-50 border-b border-border/70',
              'bg-background',
              'transition-shadow duration-200',
              scrolled && 'shadow-[var(--shadow-elevation-low)]'
            )}
          >
            <div className="container mx-auto flex flex-col gap-3 py-5 px-4 md:flex-row md:items-center md:justify-between">
              <div className="flex items-center gap-3">
                <div>
                  <h1 className="font-display text-2xl leading-none">SelfMX</h1>
                  <p className="text-xs text-muted-foreground">
                    Self Hosted ESP Layer
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-4">
                <ThemeToggle />
              </div>
            </div>
          </header>
          <main className="relative">
            <DomainsPage />
          </main>
        </div>
        <Toaster />
      </QueryClientProvider>
    </ThemeProvider>
  );
}

export default App;
