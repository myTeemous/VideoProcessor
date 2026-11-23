# Uploaded files are validated by FFprobe inside the confirm request

When a client confirms an upload, the API runs FFprobe against a short-lived read-only SAS URL for the blob, and the Video becomes Ready or Rejected before the response returns. FFprobe reads only headers and indexes via HTTP range requests, so this is fast even for multi-GB files. This keeps "Job" meaning only user-requested processing and gives the client an immediate answer. The consequence is that the API container, not just the Worker, ships with FFprobe installed.
