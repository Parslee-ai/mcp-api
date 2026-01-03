'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { authApi } from '@/lib/api';
import { useAuth } from '@/providers/auth-provider';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Zap, AlertCircle, CheckCircle2, Loader2 } from 'lucide-react';

const phoneSchema = z.object({
  phoneNumber: z.string().min(10, 'Please enter a valid phone number'),
});

const codeSchema = z.object({
  code: z.string().length(6, 'Code must be 6 digits'),
});

type PhoneFormData = z.infer<typeof phoneSchema>;
type CodeFormData = z.infer<typeof codeSchema>;

export default function VerifyPhonePage() {
  const router = useRouter();
  const { user, refreshUser } = useAuth();
  const [step, setStep] = useState<'phone' | 'code' | 'success'>('phone');
  const [error, setError] = useState<string | null>(null);

  const phoneForm = useForm<PhoneFormData>({
    resolver: zodResolver(phoneSchema),
  });

  const codeForm = useForm<CodeFormData>({
    resolver: zodResolver(codeSchema),
  });

  const onPhoneSubmit = async (data: PhoneFormData) => {
    setError(null);
    try {
      await authApi.setPhone(data.phoneNumber);
      setStep('code');
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to send verification code');
    }
  };

  const onCodeSubmit = async (data: CodeFormData) => {
    setError(null);
    try {
      await authApi.verifyPhone(data.code);
      await refreshUser();
      setStep('success');
    } catch (err: any) {
      setError(err.response?.data?.error || 'Invalid verification code');
    }
  };

  if (!user) {
    return (
      <div className="flex min-h-screen items-center justify-center p-4">
        <Card className="w-full max-w-sm">
          <CardContent className="py-8">
            <p className="text-center text-muted-foreground">
              Please <Link href="/auth/login" className="text-primary hover:underline">log in</Link> to verify your phone number.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <div className="w-full max-w-sm">
        <Link href="/" className="flex items-center justify-center gap-2 mb-8">
          <Zap className="h-6 w-6 text-primary" />
          <span className="font-bold text-xl">MCP-API</span>
        </Link>

        <Card>
          <CardHeader className="space-y-1">
            <CardTitle className="text-2xl text-center">Phone Verification</CardTitle>
            <CardDescription className="text-center">
              {step === 'phone' && 'Add your phone number for extra security'}
              {step === 'code' && 'Enter the code sent to your phone'}
              {step === 'success' && 'Your phone has been verified'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {error && (
              <Alert variant="destructive" className="mb-4">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            {step === 'phone' && (
              <form onSubmit={phoneForm.handleSubmit(onPhoneSubmit)} className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="phoneNumber">Phone Number</Label>
                  <Input
                    id="phoneNumber"
                    type="tel"
                    placeholder="+1 (555) 123-4567"
                    {...phoneForm.register('phoneNumber')}
                  />
                  {phoneForm.formState.errors.phoneNumber && (
                    <p className="text-sm text-destructive">
                      {phoneForm.formState.errors.phoneNumber.message}
                    </p>
                  )}
                </div>
                <Button type="submit" className="w-full" disabled={phoneForm.formState.isSubmitting}>
                  {phoneForm.formState.isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Send Code
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  className="w-full"
                  onClick={() => router.push('/dashboard')}
                >
                  Skip for now
                </Button>
              </form>
            )}

            {step === 'code' && (
              <form onSubmit={codeForm.handleSubmit(onCodeSubmit)} className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="code">Verification Code</Label>
                  <Input
                    id="code"
                    type="text"
                    placeholder="123456"
                    maxLength={6}
                    {...codeForm.register('code')}
                  />
                  {codeForm.formState.errors.code && (
                    <p className="text-sm text-destructive">
                      {codeForm.formState.errors.code.message}
                    </p>
                  )}
                </div>
                <Button type="submit" className="w-full" disabled={codeForm.formState.isSubmitting}>
                  {codeForm.formState.isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Verify
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  className="w-full"
                  onClick={() => setStep('phone')}
                >
                  Use a different number
                </Button>
              </form>
            )}

            {step === 'success' && (
              <div className="flex flex-col items-center py-4">
                <CheckCircle2 className="h-12 w-12 text-green-600 mb-4" />
                <p className="text-center mb-4">Your phone number has been verified successfully!</p>
                <Button asChild className="w-full">
                  <Link href="/dashboard">Go to Dashboard</Link>
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
