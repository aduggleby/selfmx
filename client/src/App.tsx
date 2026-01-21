import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { DomainsPage } from './pages/DomainsPage';
import { api } from './lib/api';

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
  return (
    <QueryClientProvider client={queryClient}>
      <div className="min-h-screen bg-background">
        <header className="border-b">
          <div className="container mx-auto py-4 px-4">
            <h1 className="text-xl font-semibold">Selfmx</h1>
          </div>
        </header>
        <main>
          <DomainsPage />
        </main>
      </div>
    </QueryClientProvider>
  );
}

export default App;
