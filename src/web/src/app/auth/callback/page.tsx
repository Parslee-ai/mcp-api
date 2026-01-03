'use client';

import { useEffect, useState, Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { useAuth } from '@/providers/auth-provider';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Zap, Loader2, CheckCircle2, XCircle, AlertCircle } from 'lucide-react';

/**
 * Parses the URL fragment (hash) to extract token.
 * Token is passed via fragment (#token=xxx) instead of query params (?token=xxx)
 * to prevent the token from being logged, cached, or leaked via Referer header.
 */
function getTokenFromFragment(): string | null {
  if (typeof window === 'undefined') return null;

  const hash = window.location.hash;
  if (!hash || hash.length <= 1) return null;

  // Remove the leading # and parse as URLSearchParams
  const params = new URLSearchParams(hash.substring(1));
  return params.get('token');
}

function CallbackContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { setUserFromToken } = useAuth();
  const [status, setStatus] = useState<'processing' | 'success' | 'error'>('processing');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    // Check for error in query params (errors can still be in query params as they're not sensitive)
    const errorParam = searchParams.get('error');

    if (errorParam) {
      // Wrap in microtask to avoid synchronous setState in effect
      queueMicrotask(() => {
        setStatus('error');
        setErrorMessage(decodeURIComponent(errorParam));
      });
      return;
    }

    // Read token from URL fragment (not query params) for security
    // Fragment is never sent to the server, preventing token leakage
    const token = getTokenFromFragment();

    if (!token) {
      queueMicrotask(() => {
        setStatus('error');
        setErrorMessage('No authentication token received');
      });
      return;
    }

    // Clear the fragment from the URL to prevent token exposure in browser history
    if (typeof window !== 'undefined') {
      window.history.replaceState(null, '', window.location.pathname + window.location.search);
    }

    // Set the token in auth context and update status
    queueMicrotask(() => {
      setUserFromToken(token);
      setStatus('success');
      // Redirect to dashboard
      router.push('/dashboard');
    });
  }, [searchParams, setUserFromToken, router]);

  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="text-2xl text-center">
          {status === 'processing' && 'Signing you in...'}
          {status === 'success' && 'Welcome!'}
          {status === 'error' && 'Authentication Failed'}
        </CardTitle>
        <CardDescription className="text-center">
          {status === 'processing' && 'Please wait while we complete your sign-in.'}
          {status === 'success' && 'You have been signed in successfully.'}
          {status === 'error' && 'We could not complete your sign-in.'}
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col items-center py-8">
        {status === 'processing' && (
          <Loader2 className="h-12 w-12 animate-spin text-primary" />
        )}
        {status === 'success' && (
          <>
            <CheckCircle2 className="h-12 w-12 text-green-600 mb-4" />
            <p className="text-sm text-muted-foreground">Redirecting to dashboard...</p>
          </>
        )}
        {status === 'error' && (
          <>
            <XCircle className="h-12 w-12 text-destructive mb-4" />
            {errorMessage && (
              <Alert variant="destructive" className="mb-4">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{errorMessage}</AlertDescription>
              </Alert>
            )}
            <Button asChild>
              <Link href="/auth/login">Try Again</Link>
            </Button>
          </>
        )}
      </CardContent>
    </Card>
  );
}

export default function AuthCallbackPage() {
  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <div className="w-full max-w-sm">
        <Link href="/" className="flex items-center justify-center gap-2 mb-8">
          <Zap className="h-6 w-6 text-primary" />
          <span className="font-bold text-xl">MCP-API</span>
        </Link>

        <Suspense fallback={
          <Card>
            <CardContent className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </CardContent>
          </Card>
        }>
          <CallbackContent />
        </Suspense>
      </div>
    </div>
  );
}
