---
Repository: mcp-api
Analyzed: 2026-03-12T21:11:54Z
Commit: 4031b980b1062cefc6392901d0b028b1055064b3
Prior Report:
---

# MCP-API Product Summary

## 1. Product Overview

MCP-API (branded publicly as **AnyAPI**) is a multi-tenant SaaS platform that converts REST APIs into AI-compatible tools using the Model Context Protocol (MCP). Users point the platform at an API specification — OpenAPI 3.x, Swagger 2.0, GraphQL schema, or Postman Collection — and MCP-API automatically parses the spec, registers available endpoints, and exposes them as callable tools for AI agents such as Claude, GPT, and any MCP-compatible client.

**Target users:** Developers and teams who want their existing APIs to be immediately usable by AI agents without writing custom integration code.

**Role in the Parslee platform:** MCP-API is product #11 ("AnyAPI") in the Parslee AI Platform suite. It serves as the universal API bridge layer, enabling any Parslee AI employee — or any external MCP-compatible agent — to call arbitrary third-party APIs through a single, authenticated interface.

**Product URL:** https://mcp-api.ai

**Pricing tiers:**

| Tier | Price | Monthly API Calls | APIs | Endpoints per API |
|------|-------|-------------------|------|-------------------|
| Free | $0 | 1,000 | 3 | 50 |
| Pro | $29/month | 50,000 | 25 | 500 |
| Enterprise | Custom | Unlimited | Unlimited | Unlimited |

## 2. User-Facing Capabilities

| Feature | What Users Can Do | Where |
|---------|-------------------|-------|
| **GitHub Sign-In** | Sign up and log in using their GitHub account. No password required. | https://mcp-api.ai/auth/login |
| **Register an API** | Provide an API specification URL (OpenAPI, Swagger, GraphQL, or Postman Collection) and MCP-API automatically discovers and registers all available endpoints. | Dashboard > APIs > Register New API |
| **Configure API Authentication** | Attach credentials to registered APIs: API Key, Bearer Token, Basic Auth, or OAuth2 Client Credentials. Secrets are encrypted at rest with per-user keys. | API registration flow |
| **Browse Endpoints** | View all parsed endpoints for a registered API, including method, path, parameters, and descriptions. Search and filter endpoints. | Dashboard > APIs > [API Detail] |
| **Enable/Disable Endpoints** | Toggle individual endpoints on or off to control which operations AI agents can call. Toggle entire APIs on or off. | Dashboard > APIs > [API Detail] |
| **Refresh API Specs** | Re-parse an API specification on demand to pick up changes the API provider has made. | Dashboard > APIs > [API Detail] > Refresh |
| **Create MCP Tokens** | Generate authentication tokens that AI agents use to connect to the MCP server. Tokens can have names and optional expiration dates. | Dashboard > Tokens |
| **Revoke/Delete Tokens** | Immediately invalidate a token (revoke) or permanently remove it (delete). | Dashboard > Tokens |
| **Monitor Usage** | View current month's API call count vs. tier limit, percentage consumed, and historical usage over the past 12 months. | Dashboard > Usage |
| **Interactive Demo** | Try a live chat demo where an AI agent creates, lists, and searches GitHub issues through MCP-API in real time. | https://mcp-api.ai (landing page) |
| **Delete Account** | Permanently delete account and all associated data (APIs, tokens, usage records). GDPR-compliant. | Account settings |

## 3. Data Handling

### Personal Data Collected

| Data Element | Source | Purpose | Storage |
|-------------|--------|---------|---------|
| Email address | GitHub OAuth | Account identification, communications | Azure Cosmos DB (encrypted at rest) |
| Display name | GitHub OAuth | Profile display | Azure Cosmos DB |
| Avatar URL | GitHub OAuth | Profile display | Azure Cosmos DB |
| GitHub user ID | GitHub OAuth | Authentication binding | Azure Cosmos DB |
| Account timestamps | System | Created/last login tracking | Azure Cosmos DB |

### Application Data Stored

| Data Element | Purpose | Retention |
|-------------|---------|-----------|
| API registration metadata | Store API name, base URL, spec URL, description | Until user deletes |
| Parsed endpoint definitions | Enable AI agents to discover and call API operations | Until user deletes |
| Authentication credentials (API keys, tokens, passwords) | Apply user-provided credentials when calling APIs | Encrypted with AES-256-GCM using per-user key salt; until user deletes |
| MCP tokens | Authenticate AI agent connections | SHA-256 hashed; until user revokes/deletes |
| Monthly usage records | Track API call volume against tier limits | Indefinite |
| Refresh tokens | Maintain user sessions | 7-day rolling expiration; hashed in database |

### Multi-Tenant Isolation

All user data is partitioned by user ID in Cosmos DB. Users cannot access other users' data. Large API registrations (such as GitHub's 900+ endpoints) are split across two database containers to stay within document size limits.

### Account Deletion

When a user deletes their account, all associated data is permanently removed: API registrations, endpoints, MCP tokens, usage records, refresh tokens, and the user record. A 30-day backup retention period applies.

### Data Sent Externally

- **To registered APIs:** When AI agents call tools, MCP-API sends HTTP requests to the registered API's base URL, including any user-configured authentication credentials.
- **To GitHub:** OAuth authentication flow exchanges tokens with GitHub's authorization server.
- **To Anthropic (optional):** The interactive landing-page demo sends chat messages to Anthropic's Claude API for tool-calling demonstrations.

## 4. Third-Party Integrations

| Service | Purpose | Data Exchanged |
|---------|---------|----------------|
| **GitHub OAuth** | User authentication | OAuth tokens, user profile (email, name, avatar) |
| **Azure Cosmos DB** | Primary data store | All application data (encrypted at rest by Azure) |
| **Azure Key Vault** | Secret management | Connection strings, signing keys (optional) |
| **Azure Application Insights** | Monitoring and telemetry | Request metrics, error traces (optional) |
| **Anthropic Claude API** | Interactive demo chat | User demo messages, tool call results (optional; demo disabled if not configured) |
| **GitHub API (demo)** | Demo feature creates/lists GitHub issues | Issue data in the `Parslee-ai/mcp-api-demo` repository |
| **User-registered APIs** | Core product function — AI agents call these APIs | Varies by API; includes user-configured credentials |

## 5. FAQ Source Material

### Getting Started

**Q: How do I create an account?**
A: Click "Get Started" on mcp-api.ai and sign in with your GitHub account. Your account is created automatically on first login. No email or password registration is required.

**Q: How do I register my first API?**
A: From the dashboard, go to APIs and click "Register New API." Enter your API's OpenAPI specification URL. MCP-API will automatically parse the spec, discover all endpoints, and register them. You can also use Swagger 2.0, GraphQL introspection endpoints, or Postman Collection URLs.

**Q: How do I connect an AI agent to MCP-API?**
A: Go to the Tokens page and create a new MCP token. Copy the token (it is only shown once). Configure your MCP-compatible AI client (Claude Desktop, Cursor, etc.) to connect to MCP-API's server endpoint using that token.

**Q: What API specification formats are supported?**
A: OpenAPI 3.x, Swagger 2.0, GraphQL (via introspection query or SDL), and Postman Collections v2.1.

### Daily Use

**Q: Can I control which endpoints AI agents can call?**
A: Yes. On the API detail page, each endpoint has an enable/disable toggle. You can also disable an entire API registration to prevent all its endpoints from being called.

**Q: How do I add authentication to my API?**
A: During registration, select the authentication method your API requires: API Key, Bearer Token, Basic Auth, or OAuth2 Client Credentials. Enter the required credentials. All secrets are encrypted before storage.

**Q: What happens if my API specification changes?**
A: Click the "Refresh" button on the API detail page to re-parse the specification and update the endpoint list. New endpoints are added, removed endpoints are deleted, and existing endpoints are updated.

**Q: How do I check my usage?**
A: The Usage page shows your current month's API call count, your tier limit, a progress bar showing percentage consumed, and a 12-month usage history table.

### Administration

**Q: How do I upgrade from Free to Pro?**
A: The Usage page displays tier information and an upgrade button. Billing integration for self-service upgrades is planned but not yet available. Contact support for tier changes.

**Q: Can I export my data?**
A: Data export functionality is planned. Currently, you can view all your API registrations, endpoints, and tokens through the dashboard. Contact support for data portability requests.

**Q: How are my API credentials secured?**
A: All sensitive credentials (API keys, bearer tokens, passwords) are encrypted using AES-256-GCM with a per-user encryption key derived from a unique salt. Credentials are decrypted only at the moment of API execution.

### Troubleshooting

**Q: I registered an API but no endpoints appear. What happened?**
A: The specification URL may be unreachable, or the specification may have parsing errors. Try refreshing the API. Ensure the spec URL returns a valid OpenAPI, Swagger, GraphQL, or Postman Collection document.

**Q: My MCP token stopped working. What should I check?**
A: Verify the token has not been revoked on the Tokens page. Check if the token has an expiration date that has passed. If needed, create a new token.

**Q: API calls from my AI agent are failing. What should I check?**
A: Verify the API registration is enabled, the target endpoints are enabled, and the authentication credentials are correct. Check your usage to ensure you have not exceeded your tier's monthly call limit.

## 6. Functionality Gaps

### Unwired Features

| Feature | Description | Location | Impact |
|---------|-------------|----------|--------|
| **Email verification flow** | User model has `EmailVerified` flag and `EmailVerificationToken` field. Email sending infrastructure (`IEmailService`, `AcsEmailService`) exists but is never called. All OAuth users are auto-verified. | User model, email service interfaces | No user impact — OAuth-only authentication makes this unnecessary for current auth model. Would be needed if password-based registration is added. |
| **Email notifications** | Email service infrastructure is implemented but no code path triggers emails (welcome emails, usage alerts, security notifications). | `AcsEmailService` | Users receive no proactive communications from the platform. |
| **Verify page** | Auth routing references `/auth/verify` but no corresponding page exists. | `src/web/src/app/auth/` | Dead reference. No user impact since email verification is not active. |

### Partial Implementations

| Feature | Description | Location | Impact |
|---------|-------------|----------|--------|
| **OAuth2 Client Credentials auth** | Backend model (`OAuth2AuthConfig`) supports OAuth2 flow configuration. Frontend registration form shows OAuth2 as an option but displays a "contact support" message instead of configuration fields. | Frontend register form, `OAuth2AuthConfig` model | Users cannot self-configure OAuth2 Client Credentials authentication for their APIs through the UI. Must use direct API calls or contact support. |
| **Post-registration auth updates** | API endpoint `PUT /api/apis/{id}/auth` exists for updating authentication configuration, but the frontend has no UI to change auth settings after initial registration. | API controller, frontend API detail page | Users cannot update API credentials through the UI after registration. Must re-register the API or call the API directly. |

### Deferred Roadmap Items

| Feature | Description | Location | Impact |
|---------|-------------|----------|--------|
| **Billing & tier upgrades** | Usage page shows tier info and an "Upgrade to Pro" button linking to `/pricing`. No `/pricing` page exists. No payment integration. Tier is a database field defaulting to "free" with no API to change it. | Usage page, user model | Users cannot self-service upgrade their subscription tier. Tier changes require manual database updates. |
| **Data export** | Privacy policy mentions "Export your API registrations" as a user capability. No export endpoint or UI exists. | Privacy policy page | Users cannot export their data through the product. |
| **Admin dashboard** | No administrative interface exists for platform operators (user management, usage analytics, tier management, content moderation). | N/A | Platform operations require direct database access. |

### Impact Assessment

The core product loop — register APIs, create tokens, connect AI agents, track usage — is fully functional. The gaps primarily affect **monetization** (no self-service billing), **operations** (no admin tools), and **edge-case API authentication** (OAuth2 setup requires manual intervention). None of the identified gaps prevent users from using the core product on the Free tier.

## 7. Issue Cross-References

No GitHub issues found in the repository. The issue tracker is empty.

## 8. Change Log Since Last Summary

**First run** — no prior summary exists. Full commit history captured below (56 commits on main):

| Date Range | Key Changes |
|-----------|-------------|
| **Initial release** | Core platform: dynamic MCP server for REST APIs, OpenAPI parsing, Cosmos DB storage |
| **Spec format expansion** | Added Postman Collection v2.1 support, GraphQL and Swagger 2.0 support |
| **Multi-tenant security** | Per-user AES-256-GCM secret encryption (Phase 4), usage tracking with tier-based limits (Phase 5), MCP token-based authentication (Phase 6) |
| **Frontend migration** | Replaced Blazor UI with Next.js SPA + .NET REST API, added interactive GitHub demo to landing page |
| **Auth simplification** | Simplified authentication to GitHub-only OAuth, prepared for public release with GDPR compliance and security hardening |
| **Open source prep** | MIT license, GitHub Actions CI, unit tests for security and core functionality, code quality fixes |
| **CI/CD** | GitHub Actions workflow, Docker builds, Azure Container Apps deployment pipeline, Dependabot for dependency updates |
| **Infrastructure** | Azure DNS management for mcp-api.ai domain, production domain configuration |
| **Recent** | Dependency bumps (ASP.NET, Application Insights, GitHub OAuth library), CI fixes for Dependabot PRs and deployment |
