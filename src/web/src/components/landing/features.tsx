import { Code2, Key, Users, RefreshCw, FileJson, Layers } from 'lucide-react';

const features = [
  {
    icon: FileJson,
    title: 'Multiple Spec Formats',
    description:
      'Support for OpenAPI 3.x, Swagger 2.0, GraphQL introspection, and Postman Collections v2.1.',
  },
  {
    icon: Key,
    title: 'Flexible Auth',
    description:
      'API Key, Bearer Token, Basic Auth, or OAuth2 Client Credentials. We handle the authentication for you.',
  },
  {
    icon: Users,
    title: 'Multi-Tenant',
    description:
      'Each user gets isolated storage. Your APIs and secrets are completely separate from others.',
  },
  {
    icon: Code2,
    title: 'MCP Native',
    description:
      'Built for the Model Context Protocol. Connect Claude, GPT, or any MCP-compatible AI agent.',
  },
  {
    icon: RefreshCw,
    title: 'Auto-Refresh',
    description:
      'Specs can be refreshed on demand. Keep your tool definitions in sync with your APIs.',
  },
  {
    icon: Layers,
    title: 'Endpoint Control',
    description:
      'Enable or disable individual endpoints. Only expose what your AI agent needs.',
  },
];

export function Features() {
  return (
    <section id="features" className="py-24 bg-muted/50">
      <div className="container">
        <div className="mx-auto max-w-2xl text-center mb-16">
          <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">
            Everything you need to connect APIs to AI
          </h2>
          <p className="mt-4 text-lg text-muted-foreground">
            A complete platform for managing API-to-AI integrations with enterprise-grade security.
          </p>
        </div>

        <div className="grid gap-8 sm:grid-cols-2 lg:grid-cols-3">
          {features.map((feature) => (
            <div
              key={feature.title}
              className="rounded-lg border bg-card p-6 shadow-sm"
            >
              <div className="mb-4 inline-flex rounded-lg bg-primary/10 p-2">
                <feature.icon className="h-5 w-5 text-primary" />
              </div>
              <h3 className="mb-2 font-semibold">{feature.title}</h3>
              <p className="text-sm text-muted-foreground">{feature.description}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
