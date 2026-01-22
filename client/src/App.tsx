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
        <div className="min-h-screen bg-gradient-to-b from-background via-background to-muted/30">
          <header
            className={cn(
              'sticky top-0 z-50 border-b bg-background/80 backdrop-blur-sm transition-shadow duration-200',
              scrolled && 'shadow-[var(--shadow-elevation-low)]'
            )}
          >
            <div className="container mx-auto flex items-center justify-between py-4 px-4">
              <h1 className="text-xl font-semibold tracking-tight">Selfmx</h1>
              <ThemeToggle />
            </div>
          </header>
          <main>
            <DomainsPage />
          </main>
        </div>
        <Toaster />
      </QueryClientProvider>
    </ThemeProvider>
  );
}

export default App;
