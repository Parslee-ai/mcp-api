'use client';

import { Suspense, useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { authApi } from '@/lib/api';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Zap, CheckCircle2, XCircle, Loader2 } from 'lucide-react';

function VerifyEmailContent() {
  const searchParams = useSearchParams();
  const token = searchParams.get('token');
  const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading');
  const [message, setMessage] = useState('');

  useEffect(() => {
    if (!token) {
      setStatus('error');
      setMessage('No verification token provided');
      return;
    }

    authApi.verifyEmail(token)
      .then(() => {
        setStatus('success');
        setMessage('Your email has been verified successfully!');
      })
      .catch((err) => {
        setStatus('error');
        setMessage(err.response?.data?.error || 'Verification failed. The token may be expired or invalid.');
      });
  }, [token]);

  return (
    <CardContent className="flex flex-col items-center py-8">
      {status === 'loading' && (
        <>
          <Loader2 className="h-12 w-12 animate-spin text-primary mb-4" />
          <p className="text-muted-foreground">Verifying your email...</p>
        </>
      )}
      {status === 'success' && (
        <>
          <CheckCircle2 className="h-12 w-12 text-green-600 mb-4" />
          <p className="text-center">{message}</p>
        </>
      )}
      {status === 'error' && (
        <>
          <XCircle className="h-12 w-12 text-destructive mb-4" />
          <p className="text-center text-muted-foreground">{message}</p>
        </>
      )}
    </CardContent>
  );
}

export default function VerifyEmailPage() {
  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <div className="w-full max-w-sm">
        <Link href="/" className="flex items-center justify-center gap-2 mb-8">
          <Zap className="h-6 w-6 text-primary" />
          <span className="font-bold text-xl">MCP-API</span>
        </Link>

        <Card>
          <CardHeader className="space-y-1">
            <CardTitle className="text-2xl text-center">Email Verification</CardTitle>
            <CardDescription className="text-center">
              Verifying your email address
            </CardDescription>
          </CardHeader>
          <Suspense fallback={
            <CardContent className="flex flex-col items-center py-8">
              <Loader2 className="h-12 w-12 animate-spin text-primary mb-4" />
              <p className="text-muted-foreground">Loading...</p>
            </CardContent>
          }>
            <VerifyEmailContent />
          </Suspense>
          <CardFooter className="flex justify-center">
            <Button asChild>
              <Link href="/auth/login">Go to Login</Link>
            </Button>
          </CardFooter>
        </Card>
      </div>
    </div>
  );
}
