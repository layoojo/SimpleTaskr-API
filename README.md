SimpleTaskr is a minimal API for creating and assigning tasks. 

## Features
- Create and manage tasks with minimal setup
- Assign tasks to users
- Track task completion status
- Minimal API with JWT authentication

## Installation

1. Clone the repository:
git clone https://github.com/yourusername/SimpleTaskrAPI.git
cd SimpleTaskrAPI

2. Install the .NET 10 SDK if not already installed.

3. Set up the JWT secret key as described in the main documentation.

4. Run the API:
dotnet run --launch-profile http

## Authentication

SimpleTaskr uses JWT authentication.

```
Authorization: Bearer <your-jwt-token>
```

## Endpoints

POST /auth/register - Register a new user
POST /auth/login - Log in and receive a JWT access token

GET /tasks - List all tasks
GET /tasks/complete - List completed tasks
GET /tasks/{id} - Get a task by ID
GET /users - Get all users
GET /users/{id} - Get a user by ID

POST /tasks - Create a new task
PUT /tasks/{id}/assign - Assign a task to a user
PATCH /tasks/{id} - Partially update task fields
DELETE /tasks/{id} - Delete a task


