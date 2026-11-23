# Uploads go directly to Blob Storage via SAS, not through the API

Videos can be several GB. Instead of streaming upload bytes through the API, the client requests an upload slot, receives a short-lived write-only SAS URL, uploads straight to Azure Blob Storage in blocks, and then confirms the upload with the API. The API never handles upload bytes, so request-size limits, timeouts and API bandwidth stop mattering, and the browser gets resumable block uploads for free. The cost is an extra confirm step, Blob CORS configuration, and the need to clean up uploads that are started but never confirmed.

## Considered Options

- **Streaming through the API**: simpler single endpoint, but every byte transits the API and large uploads tie up requests for minutes.
- **Custom chunked/resumable upload through the API (tus-style)**: reinvents what Blob block uploads already provide.
