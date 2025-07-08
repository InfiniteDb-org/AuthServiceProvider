# AuthServiceProvider

## Overview
AuthServiceProvider is an Azure Functions-based authentication microservice for modern .NET solutions. It handles user registration, sign-in, token generation, and account management with robust error handling and clear HTTP semantics.


## Architecture
- **Functions:** HTTP-triggered endpoints for sign-up, sign-in, sign-out, token, and registration flows
- **Services:** AuthService, TokenServiceClient, helpers for error wrapping and request validation
- **Error Handling:** Centralized via `FunctionErrorWrapper` and `ProblemException` (returns ProblemDetails with correct HTTP status)
- **External Integration:** Communicates with AccountService and TokenService via HTTP

## API Endpoints

| Method | Route                          | Description               | Body/Params              |
|--------|-------------------------------|--------------------------|--------------------------|
| POST   | /auth/signup                   | Register new user         | SignUpFormDto (JSON)     |
| POST   | /auth/signin                   | Authenticate user         | SignInFormDto (JSON)     |
| POST   | /auth/signout                  | Sign out user             | SignOutRequest (JSON)    |
| POST   | /auth/generate-token           | Generate JWT token        | { userId, email } (JSON) |
| POST   | /auth/validate-token           | Validate JWT token        | { token } (JSON)         |
| GET    | /auth/account/{id}             | Get account by ID         | Path param               |
| GET    | /auth/account/email/{email}    | Get account by email      | Path param               |

## Error Handling
All validation and service errors are converted to `ProblemDetails` objects with correct HTTP status codes (400, 401, 500, etc).
