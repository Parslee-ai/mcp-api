'use client';

import { useUsage, useUsageHistory } from '@/hooks/use-usage';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Activity, TrendingUp, Calendar, Zap } from 'lucide-react';
import Link from 'next/link';

const tierFeatures: Record<string, string[]> = {
  Free: ['1,000 API calls/month', 'Up to 3 APIs', 'Community support'],
  Pro: ['50,000 API calls/month', 'Unlimited APIs', 'Priority support', 'Custom domains'],
  Enterprise: ['Unlimited API calls', 'Unlimited APIs', 'Dedicated support', 'SLA guarantee', 'SSO'],
};

export default function UsagePage() {
  const { data: usage, isLoading: usageLoading } = useUsage();
  const { data: history, isLoading: historyLoading } = useUsageHistory();

  const callsThisMonth = usage?.apiCallsUsed || 0;
  const callsLimit = usage?.apiCallsLimit || 1000;
  const usagePercent = callsLimit > 0 ? Math.min(Math.round((callsThisMonth / callsLimit) * 100), 100) : 0;
  const tier = usage?.tier || 'Free';

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Usage</h1>
        <p className="text-muted-foreground">
          Monitor your API usage and manage your subscription
        </p>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Activity className="h-5 w-5" />
              Current Usage
            </CardTitle>
            <CardDescription>API calls this billing period</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {usageLoading ? (
              <>
                <Skeleton className="h-8 w-32" />
                <Skeleton className="h-4" />
              </>
            ) : (
              <>
                <div className="flex items-baseline gap-2">
                  <span className="text-4xl font-bold">{callsThisMonth.toLocaleString()}</span>
                  <span className="text-muted-foreground">/ {callsLimit.toLocaleString()}</span>
                </div>
                <Progress value={usagePercent} className="h-2" />
                <p className="text-sm text-muted-foreground">
                  {usagePercent}% of monthly limit used
                </p>
                {usagePercent >= 80 && (
                  <p className="text-sm text-yellow-600 dark:text-yellow-500">
                    You&apos;re approaching your monthly limit. Consider upgrading.
                  </p>
                )}
              </>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <TrendingUp className="h-5 w-5" />
              Current Plan
            </CardTitle>
            <CardDescription>Your subscription details</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {usageLoading ? (
              <>
                <Skeleton className="h-8 w-24" />
                <Skeleton className="h-20" />
              </>
            ) : (
              <>
                <div className="flex items-center gap-2">
                  <Badge variant={tier === 'Free' ? 'secondary' : 'default'} className="text-lg px-3 py-1">
                    {tier}
                  </Badge>
                </div>
                <ul className="space-y-2">
                  {tierFeatures[tier]?.map((feature) => (
                    <li key={feature} className="flex items-center gap-2 text-sm">
                      <Zap className="h-4 w-4 text-primary" />
                      {feature}
                    </li>
                  ))}
                </ul>
                {tier === 'Free' && (
                  <Button className="w-full mt-4" asChild>
                    <Link href="/pricing">Upgrade to Pro</Link>
                  </Button>
                )}
              </>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Calendar className="h-5 w-5" />
            Usage History
          </CardTitle>
          <CardDescription>Your API usage over the past months</CardDescription>
        </CardHeader>
        <CardContent>
          {historyLoading ? (
            <div className="space-y-4">
              <Skeleton className="h-12" />
              <Skeleton className="h-12" />
              <Skeleton className="h-12" />
            </div>
          ) : history && history.length > 0 ? (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Period</TableHead>
                    <TableHead className="text-right">API Calls</TableHead>
                    <TableHead className="text-right">Limit</TableHead>
                    <TableHead className="text-right">Usage</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {history.map((item) => {
                    const percent = callsLimit > 0 ? Math.round((item.apiCallCount / callsLimit) * 100) : 0;
                    return (
                      <TableRow key={item.yearMonth}>
                        <TableCell className="font-medium">{item.yearMonth}</TableCell>
                        <TableCell className="text-right">{item.apiCallCount.toLocaleString()}</TableCell>
                        <TableCell className="text-right">{callsLimit.toLocaleString()}</TableCell>
                        <TableCell className="text-right">
                          <Badge variant={percent >= 100 ? 'destructive' : percent >= 80 ? 'secondary' : 'outline'}>
                            {percent}%
                          </Badge>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          ) : (
            <div className="text-center py-8 text-muted-foreground">
              No usage history available yet
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
