'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useApi, useApiEndpoints, useToggleApi, useToggleEndpoint, useDeleteApi, useRefreshApi } from '@/hooks/use-apis';
import { EndpointTable } from '@/components/dashboard/endpoint-table';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Switch } from '@/components/ui/switch';
import { Skeleton } from '@/components/ui/skeleton';
import { Input } from '@/components/ui/input';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import {
  ArrowLeft,
  ExternalLink,
  RefreshCw,
  Trash2,
  Search,
  Loader2,
} from 'lucide-react';

export default function ApiDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const { data: api, isLoading: apiLoading } = useApi(id);
  const { data: endpoints, isLoading: endpointsLoading } = useApiEndpoints(id);
  const toggleApi = useToggleApi();
  const toggleEndpoint = useToggleEndpoint();
  const deleteApi = useDeleteApi();
  const refreshApi = useRefreshApi();

  const [search, setSearch] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);

  const filteredEndpoints = endpoints?.filter(
    (endpoint) =>
      endpoint.path.toLowerCase().includes(search.toLowerCase()) ||
      endpoint.method.toLowerCase().includes(search.toLowerCase()) ||
      endpoint.summary?.toLowerCase().includes(search.toLowerCase())
  );

  const handleToggleApi = (enabled: boolean) => {
    toggleApi.mutate({ id, enabled });
  };

  const handleToggleEndpoint = (endpointId: string, enabled: boolean) => {
    toggleEndpoint.mutate({ apiId: id, endpointId, enabled });
  };

  const handleDelete = async () => {
    setIsDeleting(true);
    try {
      await deleteApi.mutateAsync(id);
      router.push('/apis');
    } catch (error) {
      setIsDeleting(false);
    }
  };

  const handleRefresh = async () => {
    setIsRefreshing(true);
    try {
      await refreshApi.mutateAsync(id);
    } finally {
      setIsRefreshing(false);
    }
  };

  if (apiLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-[200px]" />
        <Skeleton className="h-[400px]" />
      </div>
    );
  }

  if (!api) {
    return (
      <div className="text-center py-12">
        <h2 className="text-xl font-semibold mb-2">API not found</h2>
        <p className="text-muted-foreground mb-4">
          The API you&apos;re looking for doesn&apos;t exist or has been deleted.
        </p>
        <Button asChild>
          <Link href="/apis">Back to APIs</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/apis">
            <ArrowLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div className="flex-1">
          <h1 className="text-3xl font-bold">{api.displayName}</h1>
          {api.description && (
            <p className="text-muted-foreground">{api.description}</p>
          )}
        </div>
        <div className="flex items-center gap-2">
          <span className="text-sm text-muted-foreground">Enabled</span>
          <Switch
            checked={api.isEnabled}
            onCheckedChange={handleToggleApi}
          />
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>API Configuration</CardTitle>
          <CardDescription>Manage your API settings</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <div>
              <p className="text-sm font-medium text-muted-foreground">Base URL</p>
              <div className="flex items-center gap-2">
                <p className="font-mono text-sm">{api.baseUrl}</p>
                <a
                  href={api.baseUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-muted-foreground hover:text-foreground"
                >
                  <ExternalLink className="h-4 w-4" />
                </a>
              </div>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Authentication</p>
              <Badge variant="outline">{api.authType}</Badge>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Endpoints</p>
              <p>{api.endpoints?.length || 0} endpoints</p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Registered</p>
              <p>{new Date(api.createdAt).toLocaleDateString()}</p>
            </div>
          </div>

          <div className="flex gap-2 pt-4 border-t">
            <Button variant="outline" onClick={handleRefresh} disabled={isRefreshing}>
              {isRefreshing ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="mr-2 h-4 w-4" />
              )}
              Refresh Spec
            </Button>
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button variant="destructive">
                  <Trash2 className="mr-2 h-4 w-4" />
                  Delete API
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Delete {api.displayName}?</AlertDialogTitle>
                  <AlertDialogDescription>
                    This action cannot be undone. This will permanently delete the API
                    registration and all associated endpoints.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Cancel</AlertDialogCancel>
                  <AlertDialogAction
                    onClick={handleDelete}
                    className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                    disabled={isDeleting}
                  >
                    {isDeleting ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : null}
                    Delete
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Endpoints</CardTitle>
          <CardDescription>
            Enable or disable individual API endpoints
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search endpoints..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-10"
            />
          </div>

          {endpointsLoading ? (
            <Skeleton className="h-[300px]" />
          ) : (
            <EndpointTable
              endpoints={filteredEndpoints?.map((e) => ({
                id: e.id,
                method: e.method,
                path: e.path,
                summary: e.summary,
                isEnabled: e.isEnabled,
              })) || []}
              onToggle={handleToggleEndpoint}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
