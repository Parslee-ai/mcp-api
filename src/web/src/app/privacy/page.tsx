import { Navbar } from '@/components/layout/navbar';
import { Footer } from '@/components/layout/footer';

export default function PrivacyPolicyPage() {
  return (
    <div className="flex min-h-screen flex-col">
      <Navbar />
      <main className="flex-1 container py-12">
        <div className="max-w-3xl mx-auto prose prose-gray dark:prose-invert">
          <h1>Privacy Policy</h1>
          <p className="text-muted-foreground">Last updated: January 2026</p>

          <h2>1. Information We Collect</h2>
          <p>
            When you use MCP-API, we collect the following types of information:
          </p>
          <ul>
            <li>
              <strong>Account Information:</strong> Email address and profile information
              provided through OAuth authentication (Google or GitHub).
            </li>
            <li>
              <strong>API Registrations:</strong> Information about the APIs you register,
              including OpenAPI specifications, endpoint configurations, and authentication settings.
            </li>
            <li>
              <strong>Usage Data:</strong> Information about your use of the service, including
              API calls made through the MCP server, timestamps, and request/response metadata.
            </li>
          </ul>

          <h2>2. How We Use Your Information</h2>
          <p>We use the information we collect to:</p>
          <ul>
            <li>Provide, maintain, and improve the MCP-API service</li>
            <li>Authenticate your identity and manage your account</li>
            <li>Track API usage for billing and analytics purposes</li>
            <li>Send service-related communications</li>
            <li>Detect and prevent fraud, abuse, or security incidents</li>
          </ul>

          <h2>3. Data Retention</h2>
          <p>
            We retain your data for as long as your account is active. When you delete your
            account, we will delete your personal information and API registrations within
            30 days. Some data may be retained in backups for a limited period or as required
            by law.
          </p>

          <h2>4. Third-Party Services</h2>
          <p>
            MCP-API uses the following third-party services:
          </p>
          <ul>
            <li>
              <strong>Microsoft Azure:</strong> For hosting, data storage (Cosmos DB), and
              infrastructure services.
            </li>
            <li>
              <strong>OAuth Providers:</strong> Google and GitHub for authentication. These
              providers may collect information in accordance with their own privacy policies.
            </li>
            <li>
              <strong>Application Insights:</strong> For application monitoring and telemetry.
            </li>
          </ul>

          <h2>5. Data Security</h2>
          <p>
            We implement appropriate technical and organizational measures to protect your
            data, including encryption of sensitive information at rest and in transit. API
            credentials and secrets are encrypted using industry-standard encryption.
          </p>

          <h2>6. Your Rights</h2>
          <p>You have the right to:</p>
          <ul>
            <li>Access your personal data</li>
            <li>Request correction of inaccurate data</li>
            <li>Delete your account and associated data</li>
            <li>Export your API registrations</li>
          </ul>
          <p>
            To exercise these rights, you can manage your data through your account settings
            or contact us directly.
          </p>

          <h2>7. Changes to This Policy</h2>
          <p>
            We may update this privacy policy from time to time. We will notify you of any
            material changes by posting the new policy on this page and updating the
            &ldquo;Last updated&rdquo; date.
          </p>

          <h2>8. Contact Us</h2>
          <p>
            If you have questions about this privacy policy or our data practices, please
            contact us at:{' '}
            <a href="mailto:privacy@parslee.ai" className="text-primary hover:underline">
              privacy@parslee.ai
            </a>
          </p>
        </div>
      </main>
      <Footer />
    </div>
  );
}
