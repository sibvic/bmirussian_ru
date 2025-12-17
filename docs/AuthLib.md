# AuthLib Documentation

## Overview

AuthLib is a .NET 9.0 authentication library that provides user authentication, JWT token generation, user agreement management, and role-based access control. It's designed to work with PostgreSQL databases using Entity Framework Core and supports authentication via Telegram bots.

**Package:** `Sibvic.AuthLib`  
**Version:** 9.0.0  
**Target Framework:** .NET 9.0

## Features

- User registration and authentication
- JWT token generation with role-based claims
- Temporary token generation for bot authentication flows
- User agreement management (acceptance tracking)
- Multiple credential sources (currently Telegram)
- Role-based access control
- PostgreSQL database support via Entity Framework Core

## Installation

Install the NuGet package:

```bash
dotnet add package Sibvic.AuthLib
```

Or via Package Manager:

```
Install-Package Sibvic.AuthLib
```

## Dependencies

- `Npgsql.EntityFrameworkCore.PostgreSQL` (8.0.11)
- `System.IdentityModel.Tokens.Jwt` (8.15.0)
- Entity Framework Core
- .NET 9.0

## Database Setup

### 1. Configure DbContext

In your `Program.cs` or `Startup.cs`, configure the `UserDBContext`:

```csharp
using Microsoft.EntityFrameworkCore;
using Sibvic.AuthLib;

// Configure PostgreSQL connection
builder.Services.AddDbContext<UserDBContext>(options =>
    options.UseNpgsql(connectionString));
```

### 2. Run Migrations

Create and apply Entity Framework migrations:

```bash
dotnet ef migrations add InitialAuthLibMigration
dotnet ef database update
```

## Configuration

### AuthOptions

Configure authentication options when creating `AuthLogic`:

```csharp
using Sibvic.AuthLib.Logic;

var authOptions = new AuthOptions(
    Key: "your-secret-key-at-least-32-characters-long-for-hmac-sha256",
    Issuer: "your-application-name"
);
```

**Parameters:**
- `Key` (string, required): Secret key for JWT token signing. Must be at least 32 characters for HMAC-SHA256.
- `Issuer` (string?, optional): JWT token issuer identifier.

### Dependency Injection Setup

```csharp
using Sibvic.AuthLib;
using Sibvic.AuthLib.Logic;

// Register AuthLogic
builder.Services.AddScoped<AuthLogic>(serviceProvider =>
{
    var context = serviceProvider.GetRequiredService<UserDBContext>();
    var authOptions = new AuthOptions(
        Key: builder.Configuration["Auth:Key"],
        Issuer: builder.Configuration["Auth:Issuer"]
    );
    // Optional: provide callback for user registration events
    IAuthLogicCallback? callback = serviceProvider.GetService<IAuthLogicCallback>();
    return new AuthLogic(context, authOptions, callback);
});
```

## Core Models

### User

Represents a user in the system.

```csharp
public class User
{
    public long Id { get; set; }
    public string Nickname { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
```

### UserWithRoles

Extends `User` with role information.

```csharp
public class UserWithRoles : User
{
    public List<string> Roles { get; set; }
}
```

### UserCredentianl

Stores user credentials from external sources.

```csharp
public class UserCredentianl
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public virtual User User { get; set; }
    public string SourceId { get; set; }
    public CredentialsSource Source { get; set; }
}
```

### UserToken

Stores temporary authentication tokens.

```csharp
public class UserToken
{
    public long Id { get; set; }
    public string Token { get; set; }
    public DateTime ValidTill { get; set; }
    public long UserId { get; set; }
    public virtual User User { get; set; }
}
```

### UserAgreement

Represents a user agreement that must be accepted.

```csharp
public class UserAgreement
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
}
```

### AcceptedUserAgreement

Tracks which agreements a user has accepted.

```csharp
public class AcceptedUserAgreement
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public virtual User User { get; set; }
    public int UserAgreementId { get; set; }
    public virtual UserAgreement UserAgreement { get; set; }
    public DateTime AcceptedAt { get; set; }
}
```

### UserRoles

Stores user roles for role-based access control.

```csharp
public class UserRoles
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public virtual User User { get; set; }
    public string Role { get; set; }
}
```

### UserClaim

Stores custom claims for users (for future use).

```csharp
public class UserClaim
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public virtual User? User { get; set; }
    public string ClaimType { get; set; }
}
```

### CredentialsSource

Enumeration of supported credential sources.

```csharp
public enum CredentialsSource
{
    Telegram
}
```

## API Reference

### AuthLogic Class

The main class for authentication operations.

#### Constructor

```csharp
public AuthLogic(UserDBContext context, AuthOptions options, IAuthLogicCallback? callback = null)
```

**Parameters:**
- `context`: The `UserDBContext` instance for database operations
- `options`: `AuthOptions` containing JWT signing key and issuer
- `callback`: Optional callback interface for handling user registration events

#### Methods

##### FindUser

Finds a user by their external source ID.

```csharp
public User FindUser(string? id, CredentialsSource source)
```

**Parameters:**
- `id`: The user's ID from the external source (e.g., Telegram ID)
- `source`: The credential source (e.g., `CredentialsSource.Telegram`)

**Returns:** `User` if found, `null` otherwise

**Example:**
```csharp
var user = authLogic.FindUser("123456789", CredentialsSource.Telegram);
if (user != null)
{
    Console.WriteLine($"Found user: {user.Nickname}");
}
```

##### RegisterUser

Registers a new user in the system.

```csharp
public async Task<User?> RegisterUser(
    string id, 
    string? first_name, 
    string? last_name, 
    string? username, 
    string? photo_url, 
    string? auth_date, 
    string? hash, 
    CredentialsSource source, 
    CancellationToken cancellationToken)
```

**Parameters:**
- `id`: External source user ID
- `first_name`: User's first name (optional)
- `last_name`: User's last name (optional)
- `username`: Username (optional, defaults to `id` if not provided)
- `photo_url`: Profile photo URL (optional, currently not stored)
- `auth_date`: Authentication date (optional, currently not stored)
- `hash`: Authentication hash (optional, currently not stored)
- `source`: Credential source
- `cancellationToken`: Cancellation token

**Returns:** `Task<User?>` - The newly created user

**Example:**
```csharp
var newUser = await authLogic.RegisterUser(
    id: "123456789",
    first_name: "John",
    last_name: "Doe",
    username: "johndoe",
    photo_url: null,
    auth_date: null,
    hash: null,
    source: CredentialsSource.Telegram,
    cancellationToken: cancellationToken
);
```

##### GenerateTemporaryToken

Generates a temporary token valid for 3 minutes, used for bot authentication flows.

```csharp
public async Task<string> GenerateTemporaryToken(User user, CancellationToken cancellationToken)
```

**Parameters:**
- `user`: The user for whom to generate the token
- `cancellationToken`: Cancellation token

**Returns:** `Task<string>` - The temporary token (GUID format)

**Example:**
```csharp
var tempToken = await authLogic.GenerateTemporaryToken(user, cancellationToken);
// Send this token to the user via Telegram bot
```

##### GenerateToken

Generates a JWT bearer token for authenticated users. **Requires all active agreements to be accepted.**

```csharp
public string GenerateToken(User? user)
```

**Parameters:**
- `user`: The user for whom to generate the token

**Returns:** `string` - JWT token

**Throws:**
- `ArgumentNullException`: If user is null
- `AgreementsNotAcceptedException`: If user hasn't accepted all active agreements

**Token Details:**
- **Expiration:** 7 days from generation
- **Claims:**
  - `id`: User ID (long)
  - `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`: User roles (multiple claims, one per role)
- **Algorithm:** HMAC-SHA256
- **Issuer:** Set from `AuthOptions.Issuer`

**Example:**
```csharp
try
{
    var jwtToken = authLogic.GenerateToken(user);
    // Return token to client
}
catch (AgreementsNotAcceptedException)
{
    // Handle case where user needs to accept agreements
}
```

##### AuthenticateFromTelegramBot

Authenticates a user using a Telegram ID and temporary token, returning a JWT token.

```csharp
public string AuthenticateFromTelegramBot(string telegramId, string temporaryToken)
```

**Parameters:**
- `telegramId`: The user's Telegram ID
- `temporaryToken`: The temporary token generated earlier

**Returns:** `string` - JWT bearer token

**Throws:**
- `UserNotFoundException`: If user with the Telegram ID is not found
- `InvalidTokenException`: If the temporary token is invalid
- `TokenExpiredException`: If the temporary token has expired (older than 3 minutes)
- `AgreementsNotAcceptedException`: If user hasn't accepted all active agreements

**Example:**
```csharp
try
{
    var jwtToken = authLogic.AuthenticateFromTelegramBot(telegramId, tempToken);
    // Return JWT token to client
}
catch (UserNotFoundException)
{
    // User not found
}
catch (InvalidTokenException)
{
    // Invalid temporary token
}
catch (TokenExpiredException)
{
    // Token expired, generate a new one
}
catch (AgreementsNotAcceptedException)
{
    // User needs to accept agreements
}
```

##### GetAgreementsToSign

Gets all active agreements that the user hasn't accepted yet.

```csharp
public List<UserAgreement> GetAgreementsToSign(User user)
```

**Parameters:**
- `user`: The user to check

**Returns:** `List<UserAgreement>` - List of agreements that need to be accepted

**Example:**
```csharp
var agreementsToSign = authLogic.GetAgreementsToSign(user);
if (agreementsToSign.Count > 0)
{
    // Show agreements to user
    foreach (var agreement in agreementsToSign)
    {
        Console.WriteLine($"{agreement.Title}: {agreement.Description}");
    }
}
```

##### AcceptAgreement

Records that a user has accepted an agreement.

```csharp
public void AcceptAgreement(User user, UserAgreement agreement)
```

**Parameters:**
- `user`: The user accepting the agreement
- `agreement`: The agreement being accepted

**Example:**
```csharp
var agreementsToSign = authLogic.GetAgreementsToSign(user);
foreach (var agreement in agreementsToSign)
{
    authLogic.AcceptAgreement(user, agreement);
}
// Now user can generate a token
var token = authLogic.GenerateToken(user);
```

## Exceptions

All exceptions are in the `Sibvic.AuthLib.Exceptions` namespace.

### AgreementsNotAcceptedException

Thrown when attempting to generate a JWT token for a user who hasn't accepted all active agreements.

```csharp
catch (AgreementsNotAcceptedException)
{
    // Handle: user needs to accept agreements first
}
```

### InvalidTokenException

Thrown when a temporary token is invalid or doesn't exist.

```csharp
catch (InvalidTokenException)
{
    // Handle: invalid temporary token
}
```

### TokenExpiredException

Thrown when a temporary token has expired (older than 3 minutes).

```csharp
catch (TokenExpiredException)
{
    // Handle: token expired, generate new one
}
```

### UserNotFoundException

Thrown when a user cannot be found by the provided credentials.

```csharp
catch (UserNotFoundException)
{
    // Handle: user not found
}
```

## IUserProvider Interface

Interface for providing user identity information. Implement this in your application.

```csharp
public interface IUserProvider
{
    User? FindUser(ClaimsPrincipal user);
    UserWithRoles? GetIdentity(string? id);
    string? FindTelegramId(long userId);
}
```

**Methods:**
- `FindUser(ClaimsPrincipal user)`: Finds a user from the claims principal (extracts user ID from various claim types)
- `GetIdentity(string? id)`: Returns user with roles by user ID
- `FindTelegramId(long userId)`: Finds Telegram ID for a user ID

### UserProvider Implementation

The library provides a default implementation `UserProvider` that can be used directly:

```csharp
using Sibvic.AuthLib;

// Register UserProvider
builder.Services.AddScoped<IUserProvider, UserProvider>();

// Use in your controllers
public class MyController : ControllerBase
{
    private readonly IUserProvider _userProvider;
    
    public MyController(IUserProvider userProvider)
    {
        _userProvider = userProvider;
    }
    
    [Authorize]
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var user = _userProvider.FindUser(User);
        if (user == null)
        {
            return Unauthorized();
        }
        return Ok(user);
    }
}
```

## IAuthLogicCallback Interface

Optional callback interface for handling events during user registration.

```csharp
public interface IAuthLogicCallback
{
    void BeforeUserAdded(User user);
}
```

**Methods:**
- `BeforeUserAdded(User user)`: Called before a new user is saved to the database during registration. Allows you to perform custom initialization or validation.

**Example:**
```csharp
public class MyAuthCallback : IAuthLogicCallback
{
    public void BeforeUserAdded(User user)
    {
        // Perform custom initialization
        // e.g., set default roles, send welcome email, etc.
    }
}

// Register callback
builder.Services.AddScoped<IAuthLogicCallback, MyAuthCallback>();
```

## Usage Examples

### Complete Authentication Flow (Telegram Bot)

```csharp
using Sibvic.AuthLib;
using Sibvic.AuthLib.Logic;
using Sibvic.AuthLib.Exceptions;

// 1. User initiates authentication via Telegram bot
// Bot receives Telegram user data

// 2. Check if user exists
var user = authLogic.FindUser(telegramUserId, CredentialsSource.Telegram);

if (user == null)
{
    // 3. Register new user
    user = await authLogic.RegisterUser(
        id: telegramUserId,
        first_name: telegramUser.FirstName,
        last_name: telegramUser.LastName,
        username: telegramUser.Username,
        photo_url: null,
        auth_date: null,
        hash: null,
        source: CredentialsSource.Telegram,
        cancellationToken: cancellationToken
    );
}

// 4. Generate temporary token
var tempToken = await authLogic.GenerateTemporaryToken(user, cancellationToken);

// 5. Send temporary token to user via Telegram bot
await botClient.SendMessageAsync(chatId, $"Your auth token: {tempToken}");

// 6. User provides temporary token to your API
// In your API endpoint:
try
{
    var jwtToken = authLogic.AuthenticateFromTelegramBot(telegramUserId, tempToken);
    return Ok(new { token = jwtToken });
}
catch (AgreementsNotAcceptedException)
{
    // Get agreements user needs to accept
    var agreements = authLogic.GetAgreementsToSign(user);
    return BadRequest(new { 
        error = "Agreements required",
        agreements = agreements.Select(a => new { a.Id, a.Title, a.Description })
    });
}
catch (TokenExpiredException)
{
    return BadRequest(new { error = "Token expired" });
}
catch (InvalidTokenException)
{
    return BadRequest(new { error = "Invalid token" });
}
```

### Managing User Agreements

```csharp
// Check what agreements user needs to accept
var agreementsToSign = authLogic.GetAgreementsToSign(user);

if (agreementsToSign.Count > 0)
{
    // Show agreements to user
    foreach (var agreement in agreementsToSign)
    {
        Console.WriteLine($"Please accept: {agreement.Title}");
        Console.WriteLine(agreement.Description);
        
        // User accepts
        if (userAccepts)
        {
            authLogic.AcceptAgreement(user, agreement);
        }
    }
}

// After all agreements are accepted, generate token
var token = authLogic.GenerateToken(user);
```

### Managing User Roles

```csharp
// Add a role to a user
var userRole = new UserRoles
{
    UserId = user.Id,
    Role = "Admin",
    User = user
};
context.UserRoles.Add(userRole);
await context.SaveChangesAsync(cancellationToken);

// Roles are automatically included in JWT token claims
var token = authLogic.GenerateToken(user);
// Token will contain role claims that can be used for authorization
```

### JWT Token Validation in API

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Sibvic.AuthLib;

// In Program.cs or Startup.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(authOptions.Key)
            )
        };
    });

// Register UserProvider for easy user lookup
builder.Services.AddScoped<IUserProvider, UserProvider>();

// In your controller
[Authorize]
[HttpGet("protected")]
public IActionResult ProtectedEndpoint()
{
    // Method 1: Use UserProvider to get user from claims
    var user = _userProvider.FindUser(User);
    if (user == null)
    {
        return Unauthorized();
    }
    
    // Method 2: Extract claims manually
    var userId = User.FindFirst("id")?.Value;
    var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
    
    return Ok(new { userId, roles, user });
}
```

## Database Schema

The library uses the following tables:

- `Users`: Core user information
- `UserCredentianls`: External credential mappings
- `UserToken`: Temporary authentication tokens
- `UserAgreements`: Available user agreements
- `AcceptedUserAgreements`: User agreement acceptance records
- `UserRoles`: User role assignments
- `UserClaims`: Custom user claims (for future use)

## Best Practices

1. **Secret Key Management**: Store the JWT signing key securely (e.g., in environment variables or Azure Key Vault). Never commit it to source control.

2. **Token Expiration**: JWT tokens expire after 7 days. Implement token refresh logic if needed.

3. **Agreement Management**: Always check for pending agreements before allowing access to protected resources.

4. **Error Handling**: Handle all exceptions appropriately and provide meaningful error messages to clients.

5. **Database Context**: Use dependency injection to manage `UserDBContext` lifecycle. Ensure proper disposal.

6. **Cancellation Tokens**: Always pass cancellation tokens to async methods for proper request cancellation support.

## License

MIT License

## Author

Victor Tereschenko (Sibvic)

