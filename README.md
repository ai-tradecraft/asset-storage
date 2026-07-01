# Asset Storage

Asset Storage is Tradecraft's append-only document and folder service. It uses
relational metadata plus immutable object content behind independent ports.
The first local adapter stores both concerns in SQLite without coupling the
domain to SQLite.

## Projects

```text
src/hosts/webapi       ASP.NET Core host and HTTP translation
src/hosts/aspire       Aspire AppHost and service defaults
src/services           Domain services and ports
src/infrastructure     Replaceable storage adapters
tests                  Domain, adapter, and API tests
```

The service targets .NET 10. Package versions are centrally managed in
`Directory.Packages.props` and locked per project.

## Run

Start the composed application and Aspire dashboard:

```sh
aspire start
```

Or run only the API:

```sh
dotnet run --project src/hosts/webapi/AssetStorage.WebHost
```

The API launch profile listens on `http://localhost:5187`. Use
`asset-storage.http` for example requests.

## Routes

Every resource is scoped by the first path segment, the team space:

```text
POST /{team}/_api/v1/documents
POST /{team}/_api/v1/documents/{documentId}/versions
GET  /{team}/_api/v1/documents/{documentId}
GET  /{team}/_api/v1/documents/{documentId}/content

POST /{team}/_api/v1/folders
POST /{team}/_api/v1/folders/{folderId}/versions
GET  /{team}/_api/v1/folders/{folderId}
GET  /{team}/_api/v1/folders/{folderId}/entries/{entryPath}

GET  /{team}/_folders/{folderId}/{entryPath}
GET  /{team}/{logicalPath}
```

Document upload requests stream the raw body. The request media type becomes
the stored content type; optional arbitrary JSON metadata is supplied in the
`X-Asset-Metadata` header.

Version selectors accept `major`, `major.minor`, or exact
`major.minor.patch` forms. Version writes require an expected exact version,
a `major`, `minor`, or `patch` bump, and an idempotency key.

Folder downloads are manifest-driven in V1. Clients fetch a manifest and then
download its exact document-version entries; archive/ZIP downloads are not
provided.

## Verify

```sh
dotnet restore
dotnet build --configuration Release -p:ContinuousIntegrationBuild=true
dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
```
