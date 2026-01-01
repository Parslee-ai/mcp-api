# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in AnyAPI, please report it responsibly:

1. **Do not** open a public GitHub issue for security vulnerabilities
2. Email the maintainers directly with details of the vulnerability
3. Include steps to reproduce the issue if possible
4. Allow reasonable time for the issue to be addressed before public disclosure

## What to Report

- Authentication or authorization bypass
- Injection vulnerabilities (SQL, command, etc.)
- Sensitive data exposure
- Security misconfigurations
- Denial of service vulnerabilities

## Security Best Practices for Users

When deploying AnyAPI:

### Secrets Management

- **Always** use Azure Key Vault for storing API credentials
- Never commit secrets to source control
- Rotate credentials regularly
- Use managed identities where possible

### Network Security

- Deploy behind a reverse proxy with TLS termination
- Use Azure Container Apps or App Service built-in HTTPS
- Restrict network access to Cosmos DB
- Enable Key Vault firewall rules

### Authentication

- Use strong, unique API keys for each registered API
- Prefer OAuth2 over static tokens where available
- Audit enabled APIs regularly
- Disable unused API registrations

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| Latest  | Yes                |

## Response Timeline

- **Acknowledgment**: Within 48 hours
- **Initial Assessment**: Within 1 week
- **Fix Timeline**: Depends on severity, typically 30-90 days
