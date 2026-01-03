'use client';

import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Plug, ExternalLink, Settings } from 'lucide-react';

interface ApiCardProps {
  api: {
    id: string;
    name: string;
    description?: string;
    baseUrl: string;
    endpointCount: number;
    isEnabled: boolean;
    authType: string;
  };
  onToggle: (id: string, enabled: boolean) => void;
}

export function ApiCard({ api, onToggle }: ApiCardProps) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between space-y-0">
        <div className="space-y-1">
          <CardTitle className="flex items-center gap-2">
            <Plug className="h-5 w-5" />
            {api.name}
          </CardTitle>
          {api.description && (
            <CardDescription className="line-clamp-2">
              {api.description}
            </CardDescription>
          )}
        </div>
        <Switch
          checked={api.isEnabled}
          onCheckedChange={(checked) => onToggle(api.id, checked)}
        />
      </CardHeader>
      <CardContent>
        <div className="flex items-center justify-between">
          <div className="space-y-2">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <ExternalLink className="h-4 w-4" />
              <span className="truncate max-w-[200px]">{api.baseUrl}</span>
            </div>
            <div className="flex gap-2">
              <Badge variant="secondary">{api.endpointCount} endpoints</Badge>
              <Badge variant="outline">{api.authType}</Badge>
            </div>
          </div>
          <Button variant="outline" size="sm" asChild>
            <Link href={`/apis/${api.id}`}>
              <Settings className="h-4 w-4 mr-2" />
              Manage
            </Link>
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
