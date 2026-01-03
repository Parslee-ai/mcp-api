'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { useRegisterApi } from '@/hooks/use-apis';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { AlertCircle, ArrowLeft, Loader2 } from 'lucide-react';
import Link from 'next/link';

const registerSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  specUrl: z.string().url('Please enter a valid URL'),
  description: z.string().optional(),
  authType: z.enum(['none', 'apiKey', 'bearer', 'basic', 'oauth2']),
  apiKey: z.string().optional(),
  apiKeyHeader: z.string().optional(),
  bearerToken: z.string().optional(),
  basicUsername: z.string().optional(),
  basicPassword: z.string().optional(),
});

type RegisterFormData = z.infer<typeof registerSchema>;

interface RegisterApiPayload {
  name: string;
  specUrl: string;
  description?: string;
  authType: string;
  apiKey?: string;
  apiKeyHeader?: string;
  bearerToken?: string;
  basicUsername?: string;
  basicPassword?: string;
}

export default function RegisterApiPage() {
  const router = useRouter();
  const registerApi = useRegisterApi();
  const [error, setError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      authType: 'none',
    },
  });

  const authType = watch('authType');

  const onSubmit = async (data: RegisterFormData) => {
    setError(null);
    try {
      const payload: RegisterApiPayload = {
        name: data.name,
        specUrl: data.specUrl,
        description: data.description,
        authType: data.authType,
      };

      if (data.authType === 'apiKey') {
        payload.apiKey = data.apiKey;
        payload.apiKeyHeader = data.apiKeyHeader || 'X-API-Key';
      } else if (data.authType === 'bearer') {
        payload.bearerToken = data.bearerToken;
      } else if (data.authType === 'basic') {
        payload.basicUsername = data.basicUsername;
        payload.basicPassword = data.basicPassword;
      }

      await registerApi.mutateAsync(payload);
      router.push('/apis');
    } catch (err) {
      const axiosError = err as { response?: { data?: { error?: string } } };
      setError(axiosError.response?.data?.error || 'Failed to register API');
    }
  };

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/apis">
            <ArrowLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-3xl font-bold">Register API</h1>
          <p className="text-muted-foreground">
            Add a new REST API to your MCP server
          </p>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>API Details</CardTitle>
          <CardDescription>
            Provide the OpenAPI specification URL and authentication details
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
            {error && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <div className="space-y-2">
              <Label htmlFor="name">API Name</Label>
              <Input
                id="name"
                placeholder="My API"
                {...register('name')}
              />
              {errors.name && (
                <p className="text-sm text-destructive">{errors.name.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="specUrl">OpenAPI Specification URL</Label>
              <Input
                id="specUrl"
                placeholder="https://api.example.com/openapi.json"
                {...register('specUrl')}
              />
              {errors.specUrl && (
                <p className="text-sm text-destructive">{errors.specUrl.message}</p>
              )}
              <p className="text-xs text-muted-foreground">
                Supports OpenAPI 3.x, Swagger 2.0, and Postman Collections
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="description">Description (optional)</Label>
              <Textarea
                id="description"
                placeholder="A brief description of what this API does"
                {...register('description')}
              />
            </div>

            <div className="space-y-2">
              <Label>Authentication Type</Label>
              <Select
                value={authType}
                onValueChange={(value) => setValue('authType', value as RegisterFormData['authType'])}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select authentication type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No Authentication</SelectItem>
                  <SelectItem value="apiKey">API Key</SelectItem>
                  <SelectItem value="bearer">Bearer Token</SelectItem>
                  <SelectItem value="basic">Basic Auth</SelectItem>
                  <SelectItem value="oauth2">OAuth 2.0</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {authType === 'apiKey' && (
              <div className="space-y-4 p-4 rounded-lg border bg-muted/50">
                <div className="space-y-2">
                  <Label htmlFor="apiKey">API Key</Label>
                  <Input
                    id="apiKey"
                    type="password"
                    placeholder="Your API key"
                    {...register('apiKey')}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="apiKeyHeader">Header Name</Label>
                  <Input
                    id="apiKeyHeader"
                    placeholder="X-API-Key"
                    {...register('apiKeyHeader')}
                  />
                </div>
              </div>
            )}

            {authType === 'bearer' && (
              <div className="space-y-4 p-4 rounded-lg border bg-muted/50">
                <div className="space-y-2">
                  <Label htmlFor="bearerToken">Bearer Token</Label>
                  <Input
                    id="bearerToken"
                    type="password"
                    placeholder="Your bearer token"
                    {...register('bearerToken')}
                  />
                </div>
              </div>
            )}

            {authType === 'basic' && (
              <div className="space-y-4 p-4 rounded-lg border bg-muted/50">
                <div className="space-y-2">
                  <Label htmlFor="basicUsername">Username</Label>
                  <Input
                    id="basicUsername"
                    placeholder="Username"
                    {...register('basicUsername')}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="basicPassword">Password</Label>
                  <Input
                    id="basicPassword"
                    type="password"
                    placeholder="Password"
                    {...register('basicPassword')}
                  />
                </div>
              </div>
            )}

            {authType === 'oauth2' && (
              <Alert>
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>
                  OAuth 2.0 configuration requires additional setup. Please contact support.
                </AlertDescription>
              </Alert>
            )}

            <div className="flex gap-4">
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Register API
              </Button>
              <Button type="button" variant="outline" asChild>
                <Link href="/apis">Cancel</Link>
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
