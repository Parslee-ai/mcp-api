'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useApis, useToggleApi } from '@/hooks/use-apis';
import { ApiCard } from '@/components/dashboard/api-card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Plus, Search, Plug } from 'lucide-react';

export default function ApisPage() {
  const { data: apis, isLoading } = useApis();
  const toggleApi = useToggleApi();
  const [search, setSearch] = useState('');

  const filteredApis = apis?.filter(
    (api) =>
      api.displayName.toLowerCase().includes(search.toLowerCase()) ||
      api.baseUrl.toLowerCase().includes(search.toLowerCase())
  );

  const handleToggle = (id: string, enabled: boolean) => {
    toggleApi.mutate({ id, enabled });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">APIs</h1>
          <p className="text-muted-foreground">
            Manage your registered API integrations
          </p>
        </div>
        <Button asChild>
          <Link href="/apis/register">
            <Plus className="h-4 w-4 mr-2" />
            Register API
          </Link>
        </Button>
      </div>

      <div className="relative">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          placeholder="Search APIs..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="pl-10"
        />
      </div>

      {isLoading ? (
        <div className="grid gap-4 md:grid-cols-2">
          <Skeleton className="h-[180px]" />
          <Skeleton className="h-[180px]" />
          <Skeleton className="h-[180px]" />
          <Skeleton className="h-[180px]" />
        </div>
      ) : filteredApis && filteredApis.length > 0 ? (
        <div className="grid gap-4 md:grid-cols-2">
          {filteredApis.map((api) => (
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
              onToggle={handleToggle}
            />
          ))}
        </div>
      ) : (
        <div className="rounded-lg border border-dashed p-8 text-center">
          <Plug className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
          <h3 className="font-semibold mb-2">
            {search ? 'No APIs found' : 'No APIs registered yet'}
          </h3>
          <p className="text-muted-foreground mb-4">
            {search
              ? 'Try a different search term'
              : 'Register your first API to get started'}
          </p>
          {!search && (
            <Button asChild>
              <Link href="/apis/register">
                <Plus className="h-4 w-4 mr-2" />
                Register API
              </Link>
            </Button>
          )}
        </div>
      )}
    </div>
  );
}
