import { useState, useEffect } from 'react';
import { Routes, Route, Navigate, Link } from 'react-router-dom';
import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { ThemeProvider } from './components/theme-provider';
import { ThemeToggle } from './components/ui/theme-toggle';
import { Toaster } from './components/ui/toaster';
import { AuthProvider } from './contexts/AuthContext';
import { useAuth } from './hooks/useAuth';
import { DomainsPage } from './pages/DomainsPage';
import { DomainDetailPage } from './pages/DomainDetailPage';
import { LoginPage } from './pages/LoginPage';
import { Button } from './components/ui/button';
import { Card } from './components/ui/card';
import { LogOut, AlertTriangle, ExternalLink } from 'lucide-react';
import { cn } from './lib/utils';

interface SystemStatus {
  healthy: boolean;
  issues: string[];
  timestamp: string;
}

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

async function fetchSystemStatus(): Promise<SystemStatus> {
  const response = await fetch('/v1/system/status');
  if (!response.ok) {
    throw new Error('Failed to fetch system status');
  }
  return response.json();
}

function LoadingScreen() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="text-sm text-muted-foreground">Loading...</div>
    </div>
  );
}

function SystemStatusModal({ issues }: { issues: string[] }) {
  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/60 p-4">
      <Card className="w-full max-w-lg">
        <div className="p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-destructive/10">
              <AlertTriangle className="h-5 w-5 text-destructive" />
            </div>
            <div>
              <h2 className="font-display text-lg font-semibold">Configuration Error</h2>
              <p className="text-sm text-muted-foreground">
                SelfMX cannot start due to configuration issues
              </p>
            </div>
          </div>

          <div className="space-y-2 mb-6">
            {issues.map((issue, i) => (
              <div
                key={i}
                className="rounded border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm font-mono"
              >
                {issue}
              </div>
            ))}
          </div>

          <div className="text-sm text-muted-foreground space-y-2">
            <p>Please check your environment configuration and restart the application.</p>
            <p>
              Required environment variables:
            </p>
            <ul className="list-disc list-inside font-mono text-xs space-y-1">
              <li>Aws__Region</li>
              <li>Aws__AccessKeyId</li>
              <li>Aws__SecretAccessKey</li>
              <li>ConnectionStrings__DefaultConnection</li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
}

function AuthenticatedApp() {
  const { logout } = useAuth();
  const [scrolled, setScrolled] = useState(false);

  // Check system status on mount
  const { data: systemStatus } = useQuery({
    queryKey: ['system-status'],
    queryFn: fetchSystemStatus,
    staleTime: 60 * 1000, // Check every minute
    retry: false,
  });

  useEffect(() => {
    const handleScroll = () => setScrolled(window.scrollY > 10);
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  // Show blocking modal if system is unhealthy
  const showStatusModal = systemStatus && !systemStatus.healthy;

  return (
    <div className="relative min-h-screen">
      {showStatusModal && <SystemStatusModal issues={systemStatus.issues} />}
      <header
        className={cn(
          'sticky top-0 z-50 border-b bg-background',
          'transition-shadow duration-150',
          scrolled && 'shadow-[var(--shadow-elevation-low)]'
        )}
      >
        <div className="container mx-auto max-w-4xl flex items-center justify-between py-3 px-4">
          <Link to="/" className="flex items-center gap-2 hover:opacity-80 transition-opacity">
            <h1 className="font-display text-lg font-semibold">SelfMX</h1>
            <span className="text-xs text-muted-foreground hidden sm:inline">
              Email API
            </span>
          </Link>
          <div className="flex items-center gap-1">
            <a
              href="/hangfire"
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1 px-2 py-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
              title="Hangfire Dashboard"
            >
              Jobs
              <ExternalLink className="h-3 w-3" />
            </a>
            <ThemeToggle />
            <Button
              variant="ghost"
              size="icon"
              onClick={logout}
              title="Logout"
              aria-label="Logout"
              className="h-8 w-8"
            >
              <LogOut className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<DomainsPage />} />
          <Route path="/domains/:id" element={<DomainDetailPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
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
