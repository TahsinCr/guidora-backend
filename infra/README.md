# Local infrastructure

The Compose stack provisions PostgreSQL, Redis, and RabbitMQ for local
development. Redis uses the checked-in configuration for durable AOF writes and
an `allkeys-lru` eviction policy; cache entries remain disposable because
PostgreSQL is the source of truth.

Copy `.env.example` to `.env`, replace the placeholder secrets, and start the
stack with:

```bash
docker compose up -d
docker compose ps
```

Do not commit `.env` or production credentials.
