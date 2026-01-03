import { Navbar } from '@/components/layout/navbar';
import { Footer } from '@/components/layout/footer';
import { Hero } from '@/components/landing/hero';
import { Features } from '@/components/landing/features';
import { Demo } from '@/components/landing/demo';
import { Pricing } from '@/components/landing/pricing';

export default function HomePage() {
  return (
    <div className="flex min-h-screen flex-col">
      <Navbar />
      <main className="flex-1">
        <Hero />
        <Features />
        <Demo />
        <Pricing />
      </main>
      <Footer />
    </div>
  );
}
