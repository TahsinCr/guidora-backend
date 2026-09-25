# Infrastructure integration tests

These tests live in the one root `Tests/Tests.csproj`; they do not create a
second test project or solution. They are skipped unless their explicit test
connection environment variable is supplied.

Supported variables:

- `GUIDORA_TEST_CONNECTION_STRING`
- `GUIDORA_TEST_REDIS_CONFIGURATION`
- `GUIDORA_TEST_RABBITMQ_HOST` and `GUIDORA_TEST_RABBITMQ_PASSWORD` (plus
  optional port, username, and virtual-host variables)

The PostgreSQL tests use rollback transactions or clean up committed fixtures;
use an isolated migrated test database. Redis tests create and delete
temporary keys as well as checking configuration and ping health. The RabbitMQ
check opens an authenticated connection/channel without publishing messages or
creating queues.
