import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Check } from 'lucide-react';

const tiers = [
  {
    name: 'Free',
    price: '$0',
    description: 'Perfect for trying out MCP-API',
    features: [
      '1,000 API calls/month',
      '3 registered APIs',
      '50 endpoints per API',
      'Email support',
    ],
    cta: 'Start Free',
    href: '/auth/register',
    highlighted: false,
  },
  {
    name: 'Pro',
    price: '$29',
    description: 'For teams building AI-powered products',
    features: [
      '50,000 API calls/month',
      '25 registered APIs',
      '500 endpoints per API',
      'Priority support',
      'Advanced analytics',
    ],
    cta: 'Get Started',
    href: '/auth/register?plan=pro',
    highlighted: true,
  },
  {
    name: 'Enterprise',
    price: 'Custom',
    description: 'For organizations with advanced needs',
    features: [
      'Unlimited API calls',
      'Unlimited APIs',
      'Unlimited endpoints',
      'Dedicated support',
      'SLA guarantee',
      'Custom integrations',
    ],
    cta: 'Contact Sales',
    href: 'mailto:sales@mcp-api.ai',
    highlighted: false,
  },
];

export function Pricing() {
  return (
    <section id="pricing" className="py-24">
      <div className="container">
        <div className="mx-auto max-w-2xl text-center mb-16">
          <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">
            Simple, transparent pricing
          </h2>
          <p className="mt-4 text-lg text-muted-foreground">
            Start free and upgrade as your needs grow. No hidden fees.
          </p>
        </div>

        <div className="grid gap-8 lg:grid-cols-3 lg:gap-6">
          {tiers.map((tier) => (
            <div
              key={tier.name}
              className={`rounded-lg border ${
                tier.highlighted
                  ? 'border-primary shadow-lg scale-105'
                  : 'border-border'
              } bg-card p-8`}
            >
              {tier.highlighted && (
                <div className="mb-4 inline-block rounded-full bg-primary px-3 py-1 text-xs font-semibold text-primary-foreground">
                  Most Popular
                </div>
              )}
              <h3 className="text-xl font-bold">{tier.name}</h3>
              <div className="mt-4 flex items-baseline gap-1">
                <span className="text-4xl font-bold tracking-tight">{tier.price}</span>
                {tier.price !== 'Custom' && (
                  <span className="text-sm text-muted-foreground">/month</span>
                )}
              </div>
              <p className="mt-2 text-sm text-muted-foreground">{tier.description}</p>

              <ul className="mt-8 space-y-3">
                {tier.features.map((feature) => (
                  <li key={feature} className="flex items-center gap-3">
                    <Check className="h-4 w-4 text-primary" />
                    <span className="text-sm">{feature}</span>
                  </li>
                ))}
              </ul>

              <Button
                className="mt-8 w-full"
                variant={tier.highlighted ? 'default' : 'outline'}
                asChild
              >
                <Link href={tier.href}>{tier.cta}</Link>
              </Button>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
