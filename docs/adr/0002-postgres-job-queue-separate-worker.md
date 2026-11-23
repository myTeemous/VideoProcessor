# Jobs are queued in PostgreSQL and run by a separate Worker process

Processing Jobs are rows in PostgreSQL. A separate Worker host (its own project and container, sharing the database) polls for Pending Jobs and claims them with `SELECT ... FOR UPDATE SKIP LOCKED`, so several Worker instances never claim the same Job. FFmpeg saturates CPU for minutes at a time; running it in its own process keeps the API responsive and lets the two scale independently.

## Considered Options

- **BackgroundService inside the API process**: one less deployable, but FFmpeg load starves HTTP requests.
- **In-memory `Channel<T>`**: loses Jobs on restart, contradicting the requirement to persist Jobs.
- **A message broker (Azure Service Bus, RabbitMQ)**: rejected for v1. The Job row must exist in PostgreSQL anyway; a broker adds infrastructure and a dual-write problem for no throughput we need.
