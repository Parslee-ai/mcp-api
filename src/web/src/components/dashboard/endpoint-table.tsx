'use client';

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Switch } from '@/components/ui/switch';
import { cn } from '@/lib/utils';

interface Endpoint {
  id: string;
  method: string;
  path: string;
  summary?: string;
  isEnabled: boolean;
}

interface EndpointTableProps {
  endpoints: Endpoint[];
  onToggle: (endpointId: string, enabled: boolean) => void;
}

const methodColors: Record<string, string> = {
  GET: 'bg-blue-500/10 text-blue-600 border-blue-500/20',
  POST: 'bg-green-500/10 text-green-600 border-green-500/20',
  PUT: 'bg-yellow-500/10 text-yellow-600 border-yellow-500/20',
  PATCH: 'bg-orange-500/10 text-orange-600 border-orange-500/20',
  DELETE: 'bg-red-500/10 text-red-600 border-red-500/20',
};

export function EndpointTable({ endpoints, onToggle }: EndpointTableProps) {
  if (endpoints.length === 0) {
    return (
      <div className="text-center py-8 text-muted-foreground">
        No endpoints found
      </div>
    );
  }

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-[100px]">Method</TableHead>
            <TableHead>Path</TableHead>
            <TableHead className="hidden md:table-cell">Description</TableHead>
            <TableHead className="w-[100px] text-right">Enabled</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {endpoints.map((endpoint) => (
            <TableRow key={endpoint.id}>
              <TableCell>
                <Badge
                  variant="outline"
                  className={cn(
                    'font-mono text-xs',
                    methodColors[endpoint.method] || 'bg-gray-500/10 text-gray-600'
                  )}
                >
                  {endpoint.method}
                </Badge>
              </TableCell>
              <TableCell className="font-mono text-sm">{endpoint.path}</TableCell>
              <TableCell className="hidden md:table-cell text-muted-foreground">
                {endpoint.summary || '-'}
              </TableCell>
              <TableCell className="text-right">
                <Switch
                  checked={endpoint.isEnabled}
                  onCheckedChange={(checked) => onToggle(endpoint.id, checked)}
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
