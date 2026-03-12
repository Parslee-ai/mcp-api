# Solution: Cosmos DB 2MB Document Limit

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Problem

When registering large APIs (e.g., GitHub API with 900+ endpoints), the complete `ApiRegistration` document with all endpoints inline exceeds Cosmos DB's 2MB maximum document size.

## Symptoms

- `RequestEntityTooLarge` (413) errors when upserting API registrations
- Large APIs fail to register while small APIs work fine
- GitHub, Stripe, and other comprehensive APIs cannot be stored

## Root Cause

The original design stored endpoints as a nested array within the `ApiRegistration` document. Each endpoint contains parameters, request body schemas, response definitions, and descriptions. For APIs with hundreds of operations, this easily exceeds 2MB.

## Solution

Split storage across two Cosmos DB containers:

1. **`api-registrations`** — API metadata with `endpoints: []` (always empty)
2. **`api-endpoints`** — Individual endpoint documents with `apiId` partition key

Write operations clear the endpoints array before saving the registration, then batch-upsert endpoints separately with semaphore-throttled parallelism (10 concurrent operations).

## Key Files

- `src/McpApi.Core/Storage/CosmosApiRegistrationStore.cs` — Split storage implementation
- `src/McpApi.Core/Storage/CosmosContainerFactory.cs` — Container creation with indexing policy
- `src/McpApi.Core/Constants.cs` — Container names and batch concurrency constant

## Verification

After implementing, the GitHub API (900+ endpoints) registers successfully. Each endpoint is stored as an individual document (~2-10KB), well within limits.

## See Also

- [SPLIT_STORAGE.md](../patterns/SPLIT_STORAGE.md) — Full pattern documentation
- [DATA_MODEL.md](../architecture/DATA_MODEL.md) — Container schemas and partition keys
