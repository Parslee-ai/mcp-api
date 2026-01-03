'use client';

import { useApis } from '@/hooks/use-apis';
import { useTokens } from '@/hooks/use-tokens';
import { useUsage } from '@/hooks/use-usage';
import { StatsCard } from '@/components/dashboard/stats-card';
import { ApiCard } from '@/components/dashboard/api-card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import Link from 'next/link';
import { Plug, Key, Activity, TrendingUp, Plus } from 'lucide-react';

export default function DashboardPage() {
  const { data: apis, isLoading: apisLoading } = useApis();
  const { data: tokens, isLoading: tokensLoading } = useTokens();
  const { data: usage, isLoading: usageLoading } = useUsage();

  const enabledApis = apis?.filter((api) => api.isEnabled) || [];
  const activeTokens = tokens?.filter((token) => !token.isRevoked) || [];
  const callsThisMonth = usage?.apiCallsUsed || 0;
  const callsLimit = usage?.apiCallsLimit || 1000;
  const usagePercent = callsLimit > 0 ? Math.round((callsThisMonth / callsLimit) * 100) : 0;

  return (
    <div className="space-y-8">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Dashboard</h1>
          <p className="text-muted-foreground">
            Overview of your API integrations
          </p>
        </div>
        <Button asChild>
          <Link href="/apis/register">
            <Plus className="h-4 w-4 mr-2" />
            Register API
          </Link>
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {apisLoading ? (
          <Skeleton className="h-[120px]" />
        ) : (
          <StatsCard
            title="Registered APIs"
            value={apis?.length || 0}
            description={`${enabledApis.length} enabled`}
            icon={Plug}
          />
        )}
        {tokensLoading ? (
          <Skeleton className="h-[120px]" />
        ) : (
          <StatsCard
            title="MCP Tokens"
            value={tokens?.length || 0}
            description={`${activeTokens.length} active`}
            icon={Key}
          />
        )}
        {usageLoading ? (
          <Skeleton className="h-[120px]" />
        ) : (
          <StatsCard
            title="API Calls This Month"
            value={callsThisMonth.toLocaleString()}
            description={`${usagePercent}% of ${callsLimit.toLocaleString()} limit`}
            icon={Activity}
          />
        )}
        {usageLoading ? (
          <Skeleton className="h-[120px]" />
        ) : (
          <StatsCard
            title="Current Tier"
            value={usage?.tier || 'Free'}
            description={usage?.tier === 'Free' ? 'Upgrade for more calls' : 'Premium features enabled'}
            icon={TrendingUp}
          />
        )}
      </div>

      <div>
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-xl font-semibold">Your APIs</h2>
          <Button variant="outline" asChild>
            <Link href="/apis">View all</Link>
          </Button>
        </div>

        {apisLoading ? (
          <div className="grid gap-4 md:grid-cols-2">
            <Skeleton className="h-[180px]" />
            <Skeleton className="h-[180px]" />
          </div>
        ) : apis && apis.length > 0 ? (
          <div className="grid gap-4 md:grid-cols-2">
            {apis.slice(0, 4).map((api) => (
              <ApiCard
                key={api.id}
                api={{
                  id: api.id,
                  name: api.displayName,
                  description: api.description,
                  baseUrl: api.baseUrl,
                  endpointCount: api.endpointCount,
                  isEnabled: api.isEnabled,
                  authType: api.authType,
                }}
                onToggle={() => {}}
              />
            ))}
          </div>
        ) : (
          <div className="rounded-lg border border-dashed p-8 text-center">
            <Plug className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
            <h3 className="font-semibold mb-2">No APIs registered yet</h3>
            <p className="text-muted-foreground mb-4">
              Register your first API to get started
            </p>
            <Button asChild>
              <Link href="/apis/register">
                <Plus className="h-4 w-4 mr-2" />
                Register API
              </Link>
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
