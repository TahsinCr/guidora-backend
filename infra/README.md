# Guidora local infrastructure

Copy `.env.example` to `.env`, replace both passwords, then start the local infrastructure:

```bash
cp .env.example .env
docker compose up -d
docker compose ps
```

Services:

- PostgreSQL: `localhost:5432`
- Redis: `localhost:6379`
- RabbitMQ AMQP: `localhost:5672`
- RabbitMQ management UI: `http://localhost:15672`

The containers share the private `guidora` network. Persistent data is stored in named Docker volumes. The application will be connected to these services in the next migration step; this file intentionally does not run database migrations automatically.
