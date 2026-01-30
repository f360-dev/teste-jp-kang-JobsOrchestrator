# Jobs Orchestrator - Distributed Job Queue System

A high-availability distributed job orchestrator built with .NET 8, MongoDB, and RabbitMQ. Supports priority queuing, job scheduling, cancellation, and guaranteed message delivery via the Outbox pattern.

## Features

- **Priority Queue**: High, Medium, Low priority jobs
- **Scheduled Jobs**: Execute jobs at specific times
- **Idempotency**: Prevent duplicate job creation
- **Cancellation**: Cancel pending or processing jobs
- **Dead Letter Queue**: Automatic retry with DLQ after 3 failures
- **Circuit Breaker**: Resilience for external dependencies
- **Outbox Pattern**: Guaranteed message delivery
- **Distributed Locking**: Prevent duplicate processing
- **Structured Logging**: Correlation ID tracking with Serilog
- **Health Checks**: Monitor MongoDB and RabbitMQ

---

## Prerequisites

- Docker & Docker Compose
- curl or Postman (for testing)

---

## Running the project

docker-compose up --build (optionally, use -d flag to start detached)

### Swagger documentation can be found on http://localhost:8080/swagger/index.html
---

## Authentication

The API uses **JWT Bearer tokens**. You must authenticate before creating jobs.

### Get Access Token

curl -X POST http://localhost:8080/api/auth/token 
-H "Content-Type: application/json" 
-d '{ "clientId": "test-client", "clientSecret": "test-secret" }'

**Response:**

{ "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." }

---

## Jobs API - Sample Requests

### Create a Job

curl -X POST http://localhost:5000/api/jobs 
-H "Authorization: Bearer TOKEN" 
-H "Content-Type: application/json" 
-H "Idempotency-Key: $(uuidgen)" 
-d '{ "payload": "Process order #12345", "priority": "High", "scheduledAt": null }'

Where TOKEN is the previously generated authentication token.

**Response:**

{ "jobId": "6a8f3c2b-1234-5678-90ab-cdef12345678" }

### Get Job Status

curl -X GET http://localhost:8080/api/jobs/<job-id> 
-H "Authorization: Bearer $TOKEN"
