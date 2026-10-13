import { useState, useEffect, useRef } from 'react';
import { Routes, Route, Navigate, Link } from 'react-router-dom';
import { QueryClient, QueryClientProvider, QueryCache, MutationCache, useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import { ThemeProvider } from './components/theme-provider';
import { ThemeToggle } from './components/ui/theme-toggle';
import { Toaster } from './components/ui/toaster';
import { AuthProvider } from './contexts/AuthContext';
import { useAuth } from './hooks/useAuth';
import { DomainsPage } from './pages/DomainsPage';
import { DomainDetailPage } from './pages/DomainDetailPage';
import { ApiKeysPage } from './pages/ApiKeysPage';
import { DataPage } from './pages/DataPage';
import { SentEmailsPage } from './pages/SentEmailsPage';
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

interface SystemVersion {
  version: string;
  informationalVersion: string;
  buildDate: string;
  environment: string;
}

// Handle 401 errors globally at the React Query level
const handleQueryError = (error: unknown) => {
  if ((error as Error & { status?: number }).status === 401) {
    window.dispatchEvent(new Event('selfmx:unauthorized'));
  }
};

const queryClient = new QueryClient({
  queryCache: new QueryCache({
    onError: handleQueryError,
  }),
  mutationCache: new MutationCache({
    onError: handleQueryError,
  }),
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000,
      retry: (failureCount, error) => {
        // Don't retry on 401/403
        if ((error as Error & { status?: number }).status === 401) return false;
        if ((error as Error & { status?: number }).status === 403) return false;
        return failureCount < 2;
      },
      refetchOnWindowFocus: true, // stale data refreshes when returning to the tab
    },
    mutations: {
      retry: false,
    },
  },
});

async function fetchSystemStatus(): Promise<SystemStatus> {
  const response = await fetch('/system/status');
  if (!response.ok) {
    throw new Error('Failed to fetch system status');
  }
  return response.json();
}

async function fetchSystemVersion(): Promise<SystemVersion> {
  const response = await fetch('/system/version');
  if (!response.ok) {
    throw new Error('Failed to fetch system version');
  }
  return response.json();
}

// Reloads the page once the server reports a different build than the one this page loaded with
function useReloadOnNewVersion(versionInfo: SystemVersion | undefined) {
  const loadedBuild = useRef<string | null>(null);
  useEffect(() => {
    if (!versionInfo) return;
    const build = `${versionInfo.version}|${versionInfo.buildDate}`;
    if (loadedBuild.current === null) {
      loadedBuild.current = build;
      return;
    }
    if (build !== loadedBuild.current) {
      toast.info(`SelfMX ${versionInfo.version} is available, reloading…`);
      const timer = setTimeout(() => window.location.reload(), 2000);
      return () => clearTimeout(timer);
    }
  }, [versionInfo]);
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

  // Fetch version info; re-checked so an open page picks up a new deployment
  const { data: versionInfo } = useQuery({
    queryKey: ['system-version'],
    queryFn: fetchSystemVersion,
    staleTime: 0,
    refetchInterval: 60 * 1000,
    refetchOnWindowFocus: true,
    retry: false,
  });
  useReloadOnNewVersion(versionInfo);

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
        <div className="w-full flex items-center justify-between py-3 px-4 sm:px-6 lg:px-8">
          <Link to="/" className="flex items-center gap-2 hover:opacity-80 transition-opacity">
            <h1 className="font-display text-lg font-semibold">SelfMX</h1>
            {versionInfo && (
              <span
                className="text-xs text-muted-foreground font-mono hidden sm:inline"
                title={`Build: ${versionInfo.buildDate}\nEnv: ${versionInfo.environment}`}
              >
                v{versionInfo.version}
              </span>
            )}
          </Link>
          <div className="flex items-center gap-1">
            <Link
              to="/api-keys"
              className="inline-flex items-center gap-1 px-2 py-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
            >
              API Keys
            </Link>
            <Link
              to="/sent-emails"
              className="inline-flex items-center gap-1 px-2 py-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
            >
              Sent Emails
            </Link>
            <Link
              to="/data"
              className="inline-flex items-center gap-1 px-2 py-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
            >
              Data
            </Link>
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
          <Route path="/api-keys" element={<ApiKeysPage />} />
          <Route path="/sent-emails" element={<SentEmailsPage />} />
          <Route path="/data" element={<DataPage />} />
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
