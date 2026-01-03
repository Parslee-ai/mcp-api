import { Navbar } from '@/components/layout/navbar';
import { Footer } from '@/components/layout/footer';

export default function TermsOfServicePage() {
  return (
    <div className="flex min-h-screen flex-col">
      <Navbar />
      <main className="flex-1 container py-12">
        <div className="max-w-3xl mx-auto prose prose-gray dark:prose-invert">
          <h1>Terms of Service</h1>
          <p className="text-muted-foreground">Last updated: January 2026</p>

          <h2>1. Service Description</h2>
          <p>
            MCP-API is a dynamic Model Context Protocol (MCP) server that allows you to
            register REST APIs and expose them as MCP tools for AI agents. The service
            parses OpenAPI specifications and provides a unified interface for API access.
          </p>

          <h2>2. Account Registration</h2>
          <p>
            To use MCP-API, you must create an account using a supported OAuth provider
            (Google or GitHub). You are responsible for maintaining the security of your
            account credentials and for all activities that occur under your account.
          </p>

          <h2>3. Acceptable Use</h2>
          <p>You agree not to use MCP-API to:</p>
          <ul>
            <li>Register or expose APIs that facilitate illegal activities</li>
            <li>Distribute malware, viruses, or other harmful code</li>
            <li>Abuse, harass, or violate the rights of others</li>
            <li>Circumvent rate limits or abuse the service</li>
            <li>Interfere with or disrupt the service or its infrastructure</li>
            <li>Access the service through automated means without authorization</li>
            <li>Reverse engineer or attempt to extract source code from the service</li>
          </ul>

          <h2>4. API Registration Responsibilities</h2>
          <p>When registering APIs with MCP-API, you:</p>
          <ul>
            <li>
              Must have proper authorization to access and expose the registered APIs
            </li>
            <li>
              Are responsible for complying with the terms of service of any third-party APIs
            </li>
            <li>
              Must not register APIs that contain or transmit sensitive data without proper
              security measures
            </li>
            <li>
              Are responsible for managing and securing any API credentials stored in the service
            </li>
          </ul>

          <h2>5. Service Availability</h2>
          <p>
            We strive to maintain high availability but do not guarantee uninterrupted access
            to the service. We may perform maintenance, updates, or modifications that
            temporarily affect service availability. We will make reasonable efforts to
            provide advance notice of planned downtime.
          </p>

          <h2>6. Limitation of Liability</h2>
          <p>
            TO THE MAXIMUM EXTENT PERMITTED BY LAW, MCP-API AND ITS AFFILIATES SHALL NOT BE
            LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR PUNITIVE DAMAGES,
            INCLUDING BUT NOT LIMITED TO LOSS OF PROFITS, DATA, OR BUSINESS OPPORTUNITIES,
            ARISING OUT OF OR RELATED TO YOUR USE OF THE SERVICE.
          </p>
          <p>
            OUR TOTAL LIABILITY FOR ANY CLAIMS ARISING FROM YOUR USE OF THE SERVICE SHALL
            NOT EXCEED THE AMOUNT YOU PAID FOR THE SERVICE IN THE TWELVE MONTHS PRECEDING
            THE CLAIM.
          </p>

          <h2>7. Indemnification</h2>
          <p>
            You agree to indemnify and hold harmless MCP-API and its affiliates from any
            claims, damages, or expenses arising from your use of the service, your
            violation of these terms, or your violation of any rights of a third party.
          </p>

          <h2>8. Termination</h2>
          <p>
            We reserve the right to suspend or terminate your account at any time for
            violation of these terms, abusive behavior, or any other reason at our
            discretion. Upon termination, your right to use the service will cease
            immediately, and we may delete your data in accordance with our privacy policy.
          </p>
          <p>
            You may terminate your account at any time by deleting it through the account
            settings.
          </p>

          <h2>9. Changes to Terms</h2>
          <p>
            We may modify these terms at any time. We will notify you of material changes
            by posting the updated terms on this page and updating the &ldquo;Last
            updated&rdquo; date. Your continued use of the service after changes are posted
            constitutes acceptance of the modified terms.
          </p>

          <h2>10. Governing Law</h2>
          <p>
            These terms shall be governed by and construed in accordance with the laws of
            the State of Delaware, without regard to its conflict of law provisions.
          </p>

          <h2>11. Contact Us</h2>
          <p>
            If you have questions about these terms of service, please contact us at:{' '}
            <a href="mailto:legal@parslee.ai" className="text-primary hover:underline">
              legal@parslee.ai
            </a>
          </p>
        </div>
      </main>
      <Footer />
    </div>
  );
}
