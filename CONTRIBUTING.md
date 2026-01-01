# Contributing to AnyAPI

Thank you for your interest in contributing to AnyAPI! This document provides guidelines and information for contributors.

## Getting Started

1. Fork the repository
2. Clone your fork locally
3. Create a branch for your changes
4. Make your changes
5. Run tests to ensure nothing is broken
6. Submit a pull request

## Development Setup

### Prerequisites

- .NET 9.0 SDK
- Azure Cosmos DB Emulator (for local development) or Azure Cosmos DB account
- Azure Key Vault (optional)

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

## Code Style

- Follow existing code patterns in the repository
- Use meaningful variable and method names
- Keep methods focused and concise
- Add XML documentation for public APIs

## Pull Request Process

1. **Create a focused PR** - Each PR should address a single concern
2. **Write clear descriptions** - Explain what changes you made and why
3. **Include tests** - Add tests for new functionality
4. **Update documentation** - Update README or other docs if needed
5. **Ensure CI passes** - All tests must pass before merge

## Reporting Issues

When reporting issues, please include:

- A clear description of the problem
- Steps to reproduce
- Expected vs actual behavior
- Environment details (.NET version, OS, etc.)

## Feature Requests

Feature requests are welcome! Please:

- Check existing issues first to avoid duplicates
- Describe the use case and why the feature would be valuable
- Be open to discussion about implementation approaches

## Code of Conduct

Please read and follow our [Code of Conduct](CODE_OF_CONDUCT.md).

## Questions?

Feel free to open an issue for questions or discussions about the project.
