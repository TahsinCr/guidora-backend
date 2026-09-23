# Infrastructure integration tests

These tests live in the one root `Tests/Tests.csproj`; they do not create a
second test project or solution. They are skipped unless their explicit test
connection environment variable is supplied.

Supported variables:

- `GUIDORA_TEST_CONNECTION_STRING`
- `GUIDORA_TEST_REDIS_CONFIGURATION`
- `GUIDORA_TEST_RABBITMQ_HOST` and `GUIDORA_TEST_RABBITMQ_PASSWORD` (plus
  optional port, username, and virtual-host variables)

The PostgreSQL check is read-only. The Redis check inspects server configuration
and ping health. The RabbitMQ check opens an authenticated connection/channel;
it does not publish test messages or create queues.
