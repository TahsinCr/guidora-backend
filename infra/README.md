# Local infrastructure

The Compose stack provisions PostgreSQL, Redis, and RabbitMQ for local
development. Redis is a disposable cache: AOF and RDB persistence are disabled,
and the eviction policy is `volatile-lfu`. Cache entries carry expiry times;
non-expiring profile-generation counters cannot be evicted. PostgreSQL is the
source of truth.
Compose gives Redis 256 MiB of cache data by default and a 320 MiB container
limit; set `REDIS_MAXMEMORY` lower than `REDIS_CONTAINER_MEMORY_LIMIT` when
overriding them.

Copy `.env.example` to `.env`, replace the placeholder secrets, and start the
stack with:

```bash
docker compose up -d
docker compose ps
```

Do not commit `.env` or production credentials.
