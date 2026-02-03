# UserWithBalanceLib

A .NET library for managing user balances and transactions with Entity Framework Core integration.

## Overview

`Sibvic.UserWithBalanceLib` provides a simple and robust way to manage user account balances and transaction history in .NET applications. The library handles balance top-ups, withdrawals, and automatically maintains a transaction log for audit purposes.

**Key Features:**
- Add funds to user balances (top-up)
- Withdraw funds from user balances with validation
- Automatic transaction logging
- Entity Framework Core integration
- UTC timestamp handling
- Balance validation (prevents negative balances)

## Installation

### NuGet Package

```bash
dotnet add package Sibvic.UserWithBalanceLib --version 9.0.0
```

Or via Package Manager:
```
Install-Package Sibvic.UserWithBalanceLib -Version 9.0.0
```

### Prerequisites

- .NET 9.0 or later
- Entity Framework Core
- `Sibvic.AuthLib` version 9.0.0 (dependency)

## Setup

### 1. Database Context Configuration

The library extends `UserDBContext` from `Sibvic.AuthLib`. Configure your `UserWithBalanceContext` in your application's startup:

```csharp
using Microsoft.EntityFrameworkCore;
using Sibvic.UserWithBalanceLib.Data;

// In your Program.cs or Startup.cs
builder.Services.AddDbContext<UserWithBalanceContext>(options =>
    options.UseSqlServer(connectionString)); // or UseInMemoryDatabase, UseNpgsql, etc.
```

### 2. Database Migrations

Create and apply migrations to set up the database tables:

```bash
dotnet ef migrations add AddBalanceAndTransactionTables
dotnet ef database update
```

## API Reference

### BalanceManager

The main class for managing user balances and transactions.

#### Constructor

```csharp
public BalanceManager(UserWithBalanceContext context)
```

**Parameters:**
- `context` (UserWithBalanceContext): The Entity Framework database context

**Example:**
```csharp
using var context = new UserWithBalanceContext(options);
var balanceManager = new BalanceManager(context);
```

#### Methods

##### Topup

Adds funds to a user's balance. If the user doesn't have a balance record, one will be created automatically.

```csharp
public void Topup(long userId, decimal amount, string reason)
```

**Parameters:**
- `userId` (long): The unique identifier of the user
- `amount` (decimal): The amount to add to the balance (must be >= 0)
- `reason` (string): Description/reason for the top-up (stored in transaction log)

**Throws:**
- `ArgumentOutOfRangeException`: If `amount` is negative

**Example:**
```csharp
balanceManager.Topup(userId: 123, amount: 100.50m, reason: "Initial deposit");
context.SaveChanges();
```

**Behavior:**
- Creates a new `Balance` record if one doesn't exist for the user
- Updates existing balance by adding the amount
- Creates a `Transaction` record with positive amount
- Transaction date is set to UTC `DateTime.UtcNow`

##### Withdraw

Removes funds from a user's balance. Validates that the balance exists and has sufficient funds.

```csharp
public void Withdraw(long userId, decimal amount, string reason)
```

**Parameters:**
- `userId` (long): The unique identifier of the user
- `amount` (decimal): The amount to withdraw (must be >= 0)
- `reason` (string): Description/reason for the withdrawal (stored in transaction log)

**Throws:**
- `ArgumentOutOfRangeException`: If `amount` is negative
- `InvalidOperationException`: If the user's balance doesn't exist
- `InvalidOperationException`: If the balance is insufficient for the withdrawal

**Example:**
```csharp
balanceManager.Withdraw(userId: 123, amount: 50.25m, reason: "Purchase payment");
context.SaveChanges();
```

**Behavior:**
- Requires an existing `Balance` record for the user
- Validates sufficient balance before withdrawal
- Updates balance by subtracting the amount
- Creates a `Transaction` record with negative amount
- Transaction date is set to UTC `DateTime.UtcNow`

## Data Models

### Balance

Represents a user's account balance.

```csharp
public class Balance
{
    public long Id { get; set; }              // Primary key
    public long UserId { get; set; }           // Foreign key to User
    public virtual User User { get; set; }     // Navigation property (from AuthLib)
    public decimal Total { get; set; }         // Current balance amount
}
```

### Transaction

Represents a balance transaction (top-up or withdrawal).

```csharp
public class Transaction
{
    public long Id { get; set; }              // Primary key
    public DateTime Date { get; set; }        // Transaction timestamp (UTC)
    public string Description { get; set; }   // Transaction reason/description
    public decimal Amount { get; set; }       // Positive for top-ups, negative for withdrawals
    public long UserId { get; set; }          // Foreign key to User
    public virtual User User { get; set; }    // Navigation property (from AuthLib)
}
```

### UserWithBalanceContext

The Entity Framework Core database context that extends `UserDBContext` from `Sibvic.AuthLib`.

```csharp
public class UserWithBalanceContext : UserDBContext
{
    public virtual DbSet<Transaction> Transactions { get; set; }
    public virtual DbSet<Balance> Balances { get; set; }
}
```

## Usage Examples

### Basic Top-up

```csharp
using Microsoft.EntityFrameworkCore;
using Sibvic.UserWithBalanceLib;
using Sibvic.UserWithBalanceLib.Data;

// Setup context
var options = new DbContextOptionsBuilder<UserWithBalanceContext>()
    .UseSqlServer(connectionString)
    .Options;

using var context = new UserWithBalanceContext(options);
var balanceManager = new BalanceManager(context);

// Add funds to user's account
long userId = 123;
balanceManager.Topup(userId, amount: 100.00m, reason: "Initial deposit");
context.SaveChanges();

// Check the balance
var balance = context.Balances.FirstOrDefault(b => b.UserId == userId);
Console.WriteLine($"User {userId} balance: {balance?.Total}");
```

### Basic Withdrawal

```csharp
using var context = new UserWithBalanceContext(options);
var balanceManager = new BalanceManager(context);

long userId = 123;
try
{
    balanceManager.Withdraw(userId, amount: 25.50m, reason: "Purchase item #456");
    context.SaveChanges();
    Console.WriteLine("Withdrawal successful");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Withdrawal failed: {ex.Message}");
}
```

### Complete Workflow

```csharp
using var context = new UserWithBalanceContext(options);
var balanceManager = new BalanceManager(context);

long userId = 456;

// Initial top-up
balanceManager.Topup(userId, 200.00m, "Welcome bonus");
context.SaveChanges();

// Make a purchase
balanceManager.Withdraw(userId, 75.50m, "Purchase: Premium subscription");
context.SaveChanges();

// Add more funds
balanceManager.Topup(userId, 50.00m, "Additional deposit");
context.SaveChanges();

// Check final balance
var balance = context.Balances.FirstOrDefault(b => b.UserId == userId);
Console.WriteLine($"Final balance: {balance?.Total}"); // Should be 174.50

// View transaction history
var transactions = context.Transactions
    .Where(t => t.UserId == userId)
    .OrderBy(t => t.Date)
    .ToList();

foreach (var transaction in transactions)
{
    Console.WriteLine($"{transaction.Date}: {transaction.Amount:C} - {transaction.Description}");
}
```

### Error Handling

```csharp
using var context = new UserWithBalanceContext(options);
var balanceManager = new BalanceManager(context);

long userId = 789;

try
{
    // Attempt to withdraw from non-existent balance
    balanceManager.Withdraw(userId, 100m, "Test");
    context.SaveChanges();
}
catch (InvalidOperationException ex) when (ex.Message.Contains("non-existent balance"))
{
    Console.WriteLine("User balance not found. Top up first.");
    balanceManager.Topup(userId, 100m, "Initial balance");
    context.SaveChanges();
}
catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient balance"))
{
    Console.WriteLine("Not enough funds available.");
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine($"Invalid amount: {ex.Message}");
}
```

### Multiple Users

```csharp
using var context = new UserWithBalanceContext(options);
var balanceManager = new BalanceManager(context);

// Top up for multiple users
balanceManager.Topup(1, 100m, "User 1 deposit");
balanceManager.Topup(2, 200m, "User 2 deposit");
balanceManager.Topup(3, 150m, "User 3 deposit");
context.SaveChanges();

// Each user has their own separate balance
var user1Balance = context.Balances.FirstOrDefault(b => b.UserId == 1);
var user2Balance = context.Balances.FirstOrDefault(b => b.UserId == 2);
var user3Balance = context.Balances.FirstOrDefault(b => b.UserId == 3);
```

## Important Notes

### SaveChanges

**Always call `context.SaveChanges()`** after calling `Topup()` or `Withdraw()` to persist changes to the database. The `BalanceManager` methods only modify the context; they don't save automatically.

### Transaction Dates

All transaction dates are stored in UTC format using `DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)`.

### Amount Validation

- Both `Topup()` and `Withdraw()` require non-negative amounts (>= 0)
- Zero amounts are allowed and will create transaction records
- `Withdraw()` validates that sufficient balance exists before processing

### Balance Creation

- `Topup()` automatically creates a balance if one doesn't exist
- `Withdraw()` requires an existing balance (throws exception if not found)

### Thread Safety

The `BalanceManager` is not thread-safe. Use appropriate synchronization mechanisms if accessing from multiple threads, or use separate context instances per thread.

## Best Practices

1. **Always use transactions** for critical operations:
   ```csharp
   using var transaction = context.Database.BeginTransaction();
   try
   {
       balanceManager.Withdraw(userId, amount, reason);
       // Other related operations
       context.SaveChanges();
       transaction.Commit();
   }
   catch
   {
       transaction.Rollback();
       throw;
   }
   ```

2. **Handle exceptions appropriately** - Check for insufficient balance before attempting withdrawals in user-facing code.

3. **Use meaningful reason strings** - These are stored in the transaction log for audit purposes.

4. **Query balances efficiently** - Use `FirstOrDefault()` or `SingleOrDefault()` with proper filtering to avoid loading unnecessary data.

5. **Consider using async methods** - If your context supports async operations, use `SaveChangesAsync()` for better performance.

## Dependencies

- **Sibvic.AuthLib** (v9.0.0): Provides the base `UserDBContext` and `User` entity
- **Microsoft.EntityFrameworkCore**: Core Entity Framework functionality

## License

MIT License - See package metadata for details.

## Version

Current version: **9.0.0**

## Support

For issues, questions, or contributions, please refer to the project repository.

