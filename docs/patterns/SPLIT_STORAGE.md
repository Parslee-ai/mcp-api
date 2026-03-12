# Split Storage Pattern

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Problem

Large APIs (e.g., GitHub with 900+ endpoints) produce documents that exceed Cosmos DB's 2MB document size limit when endpoints are stored inline in the `ApiRegistration` document.

## Solution

Split API data across two Cosmos DB containers:

| Container | Partition Key | Content |
|-----------|--------------|---------|
| `api-registrations` | `/id` | API metadata with `endpoints: []` (always empty) |
| `api-endpoints` | `/apiId` | Individual endpoint documents |

### Write Path

```
CosmosApiRegistrationStore.UpsertAsync(registration)
  → Clears registration.Endpoints to []
  → Upserts to api-registrations container

CosmosApiRegistrationStore.SaveEndpointsAsync(apiId, userId, endpoints)
  → Batch upserts individual endpoint documents to api-endpoints
  → Throttled via SemaphoreSlim(Constants.Cosmos.BatchConcurrency = 10)
```

### Read Path

```
CosmosApiRegistrationStore.GetAsync(id, userId)
  → Reads from api-registrations
  → Does NOT load endpoints (loaded separately on demand)

CosmosApiRegistrationStore.GetEndpointsAsync(apiId, userId)
  → Queries api-endpoints where apiId matches
  → Returns all endpoints for the API
```

## Implementation

**File:** `src/McpApi.Core/Storage/CosmosApiRegistrationStore.cs`

Key behaviors:
- `UpsertAsync` always clears the `Endpoints` list before saving to avoid exceeding the size limit
- `SaveEndpointsAsync` uses parallel upserts with semaphore throttling (10 concurrent operations)
- `DeleteAsync` deletes both the registration and all associated endpoints, with graceful handling if endpoint cleanup fails
- `SearchEndpointsAsync` performs full-text search across operation ID, path, and summary fields within the `api-endpoints` container

## Partition Key Design

- `api-registrations` uses `/id` — each registration is its own partition, enabling direct point reads
- `api-endpoints` uses `/apiId` — all endpoints for one API share a partition, enabling efficient queries and co-located storage

## Trade-offs

| Advantage | Disadvantage |
|-----------|-------------|
| No document size limits | Two containers to manage |
| Efficient endpoint queries per API | Deletion requires cross-container cleanup |
| Endpoints load on demand | No transactional consistency across containers |
| Parallel batch writes | Semaphore throttling adds complexity |

## See Also

- [DATA_MODEL.md](../architecture/DATA_MODEL.md) - Full data model reference
- [cosmos-2mb-document-limit.md](../solutions/cosmos-2mb-document-limit.md) - Problem context and solution details
