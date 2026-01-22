import { useState, useEffect } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ThemeProvider } from './components/theme-provider';
import { ThemeToggle } from './components/ui/theme-toggle';
import { Toaster } from './components/ui/toaster';
import { AuthProvider } from './contexts/AuthContext';
import { useAuth } from './hooks/useAuth';
import { DomainsPage } from './pages/DomainsPage';
import { LoginPage } from './pages/LoginPage';
import { Button } from './components/ui/button';
import { LogOut } from 'lucide-react';
import { cn } from './lib/utils';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000,
      retry: (failureCount, error) => {
        // Don't retry on 401/403
        if ((error as Error & { status?: number }).status === 401) return false;
        if ((error as Error & { status?: number }).status === 403) return false;
        return failureCount < 2;
      },
      refetchOnWindowFocus: false,
    },
  },
});

function LoadingScreen() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="animate-pulse text-muted-foreground">Loading...</div>
    </div>
  );
}

function AuthenticatedApp() {
  const { logout } = useAuth();
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const handleScroll = () => setScrolled(window.scrollY > 10);
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return (
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
              <p className="text-xs text-muted-foreground">Self Hosted ESP Layer</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <ThemeToggle />
            <Button
              variant="ghost"
              size="icon"
              onClick={logout}
              title="Logout"
              aria-label="Logout"
            >
              <LogOut className="h-5 w-5" />
            </Button>
          </div>
        </div>
      </header>
      <main className="relative">
        <DomainsPage />
      </main>
    </div>
  );
}

function AppContent() {
  const { state } = useAuth();

  if (state === 'loading') {
    return <LoadingScreen />;
  }

  if (state === 'unauthenticated') {
    return <LoginPage />;
  }

  return <AuthenticatedApp />;
}

function App() {
  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <AppContent />
          <Toaster />
        </AuthProvider>
      </QueryClientProvider>
    </ThemeProvider>
  );
}

export default App;
