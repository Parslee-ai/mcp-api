import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { ArrowRight, Zap, Shield, BarChart3 } from 'lucide-react';

export function Hero() {
  return (
    <section className="relative overflow-hidden py-24 lg:py-32">
      <div className="container">
        <div className="mx-auto max-w-3xl text-center">
          <div className="mb-6 inline-flex items-center rounded-full border px-4 py-1.5 text-sm">
            <span className="mr-2">🚀</span>
            <span>Now supporting GraphQL and Postman Collections</span>
          </div>

          <h1 className="mb-6 text-4xl font-bold tracking-tight sm:text-5xl lg:text-6xl">
            Turn any REST API into an{' '}
            <span className="text-primary">AI Tool</span>
          </h1>

          <p className="mb-10 text-lg text-muted-foreground sm:text-xl">
            Register your APIs with OpenAPI specs and let AI agents call them through a unified MCP interface.
            Multi-tenant, secure, with built-in usage tracking.
          </p>

          <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
            <Button size="lg" asChild>
              <Link href="/auth/register">
                Start Free
                <ArrowRight className="ml-2 h-4 w-4" />
              </Link>
            </Button>
            <Button size="lg" variant="outline" asChild>
              <Link href="https://github.com/Parslee-ai/mcp-api">
                View on GitHub
              </Link>
            </Button>
          </div>
        </div>

        {/* Feature highlights */}
        <div className="mt-20 grid gap-8 sm:grid-cols-3">
          <div className="flex flex-col items-center text-center">
            <div className="mb-4 rounded-full bg-primary/10 p-3">
              <Zap className="h-6 w-6 text-primary" />
            </div>
            <h3 className="mb-2 font-semibold">Instant Setup</h3>
            <p className="text-sm text-muted-foreground">
              Point to any OpenAPI spec and we handle the rest.
              Works with Swagger, GraphQL, and Postman too.
            </p>
          </div>

          <div className="flex flex-col items-center text-center">
            <div className="mb-4 rounded-full bg-primary/10 p-3">
              <Shield className="h-6 w-6 text-primary" />
            </div>
            <h3 className="mb-2 font-semibold">Secure by Design</h3>
            <p className="text-sm text-muted-foreground">
              Per-user encryption, isolated data, and secure token management.
              Your API keys stay safe.
            </p>
          </div>

          <div className="flex flex-col items-center text-center">
            <div className="mb-4 rounded-full bg-primary/10 p-3">
              <BarChart3 className="h-6 w-6 text-primary" />
            </div>
            <h3 className="mb-2 font-semibold">Usage Tracking</h3>
            <p className="text-sm text-muted-foreground">
              Monitor API calls, enforce limits, and upgrade when you need more.
              Start free, scale as you grow.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}
