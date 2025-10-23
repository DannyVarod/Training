# Code Injection

Code injection vulnerabilities occur when untrusted input is directly incorporated into code that is executed by the application. In database contexts, this typically manifests as SQL injection, but similar principles apply to NoSQL databases and other data stores.

## The Problem

When user input is directly concatenated into queries without proper sanitization or parameterization, attackers can inject malicious code that alters the intended query behavior.

## SQL Injection Example

Here's an enhanced async example demonstrating SQL injection vulnerabilities and their prevention:

### Vulnerable Code (DO NOT USE)

```csharp
[TestClass]
public class CodeInjectionExampleAsync
{
    private readonly string sqlConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=MyApp;Integrated Security=SSPI";
    
    // Malicious input that attempts SQL injection
    private readonly string maliciousInput = "'; DROP TABLE Accounts; --";
    private readonly string basicInjection = "' OR 1=1 --";
    private readonly string informationDisclosure = "' UNION SELECT table_name, column_name FROM information_schema.columns --";
    private readonly string validInput = "Test Account 1";

    [TestMethod]
    public async Task VulnerableQuery_WithMaliciousInput_DemonstratesSQLInjection()
    {
        using var context = new SqlDbContext(this.sqlConnectionString);
        
        // VULNERABLE: Direct string concatenation allows injection
        string vulnerableSql = $"SELECT * FROM dbo.Accounts WHERE Name = '{this.basicInjection}'";
        
        try
        {
            var results = await context.Database
                .SqlQueryRaw<AccountForMssql>(vulnerableSql)
                .ToListAsync();
            
            // This query may return ALL accounts instead of none due to OR 1=1
            // In a real attack, this could expose sensitive data
            Console.WriteLine($"Vulnerable query returned {results.Count} accounts");
            
            // This assertion might fail because injection bypassed the filter
            Assert.IsFalse(results.Any(), "Vulnerable query should not return data, but injection may bypass filters");
        }
        catch (Exception ex)
        {
            // Some injections might cause syntax errors or other exceptions
            Console.WriteLine($"Injection attempt caused exception: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task VulnerableQuery_WithDestructivePayload_ShowsPotentialDamage()
    {
        using var context = new SqlDbContext(this.sqlConnectionString);
        
        // EXTREMELY DANGEROUS: This could actually delete data
        string destructiveSql = $"SELECT * FROM dbo.Accounts WHERE Name = '{this.maliciousInput}'";
        
        try
        {
            // DO NOT RUN THIS IN PRODUCTION - Could destroy data
            var results = await context.Database
                .SqlQueryRaw<AccountForMssql>(destructiveSql)
                .ToListAsync();
        }
        catch (SqlException ex) when (ex.Number == 208) // Invalid object name
        {
            // If the table was actually dropped, we'd get this error on subsequent queries
            Console.WriteLine("Table may have been dropped by injection attack");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Injection payload caused: {ex.Message}");
        }
    }
}
```

### Secure Code (RECOMMENDED)

```csharp
[TestClass]
public class SecureQueryExampleAsync
{
    private readonly string sqlConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=MyApp;Integrated Security=SSPI";
    
    private readonly string maliciousInput = "'; DROP TABLE Accounts; --";
    private readonly string basicInjection = "' OR 1=1 --";
    private readonly string validInput = "Test Account 1";

    [TestMethod]
    public async Task SecureParameterizedQuery_WithMaliciousInput_PreventsSQLInjection()
    {
        using var context = new SqlDbContext(this.sqlConnectionString);
        
        // SECURE: Using parameterized queries prevents injection
        var results = await context.Accounts
            .FromSqlRaw("SELECT * FROM dbo.Accounts WHERE Name = @nameParameter",
                new SqlParameter("nameParameter", this.maliciousInput))
            .ToListAsync();
        
        // Parameterized query treats malicious input as literal data
        Assert.AreEqual(0, results.Count, "Secure query should return no results for malicious input");
    }

    [TestMethod]
    public async Task SecureParameterizedQuery_WithValidInput_ReturnsExpectedResults()
    {
        using var context = new SqlDbContext(this.sqlConnectionString);
        
        // SECURE: Parameters are properly escaped and typed
        var results = await context.Accounts
            .FromSqlRaw("SELECT * FROM dbo.Accounts WHERE Name = @nameParameter",
                new SqlParameter("nameParameter", this.validInput))
            .ToListAsync();
        
        Assert.AreEqual(1, results.Count, "Secure query should return expected results for valid input");
        Assert.AreEqual(this.validInput, results.First().Name);
    }

    [TestMethod]
    public async Task SecureLinqQuery_WithMaliciousInput_IsNaturallyProtected()
    {
        using var context = new SqlDbContext(this.sqlConnectionString);
        
        // SECURE: LINQ queries are automatically parameterized by Entity Framework
        var results = await context.Accounts
            .Where(a => a.Name == this.maliciousInput)
            .ToListAsync();
        
        // LINQ naturally prevents injection by using parameters
        Assert.AreEqual(0, results.Count, "LINQ query should safely handle malicious input");
    }

    [TestMethod]
    public async Task SecureStoredProcedure_WithParameters_PreventsSQLInjection()
    {
        using var context = new SqlDbContext(this.sqlConnectionString);
        
        // SECURE: Stored procedures with parameters are safe
        var nameParam = new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = this.maliciousInput };
        
        var results = await context.Accounts
            .FromSqlRaw("EXEC GetAccountsByName @Name", nameParam)
            .ToListAsync();
        
        Assert.AreEqual(0, results.Count, "Stored procedure should safely handle malicious input");
    }
}
```

## Advanced Injection Prevention Techniques

### Input Validation and Sanitization

```csharp
public class InputValidator
{
    public static async Task<bool> IsValidAccountNameAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
            
        // Whitelist approach: only allow specific characters
        var allowedPattern = @"^[a-zA-Z0-9\s\-_\.]+$";
        if (!Regex.IsMatch(input, allowedPattern))
            return false;
            
        // Length validation
        if (input.Length > 100)
            return false;
            
        // Check for common injection patterns
        var injectionPatterns = new[]
        {
            @"('([\s]*[\w]*[\s]*=[\s]*[\w]*[\s]*)*--)",  // SQL comment injection
            @"([\s]*;[\s]*drop[\s]+table)",               // DROP TABLE
            @"([\s]*union[\s]+select)",                   // UNION SELECT
            @"([\s]*or[\s]+1[\s]*=[\s]*1)",              // OR 1=1
            @"(exec[\s]*\()",                            // EXEC function calls
        };
        
        foreach (var pattern in injectionPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                return false;
        }
        
        return true;
    }
}

[TestMethod]
public async Task ValidatedInput_RejectsInjectionAttempts()
{
    var maliciousInputs = new[]
    {
        "'; DROP TABLE Accounts; --",
        "' OR 1=1 --",
        "' UNION SELECT * FROM Users --",
        "admin'--",
        "1' EXEC xp_cmdshell('dir') --"
    };
    
    foreach (var input in maliciousInputs)
    {
        var isValid = await InputValidator.IsValidAccountNameAsync(input);
        Assert.IsFalse(isValid, $"Input '{input}' should be rejected by validation");
    }
}
```

### Database-Level Protection

```csharp
public class DatabaseSecurityService
{
    public async Task ConfigureDatabaseSecurityAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        
        // Create limited privilege user for application
        await connection.ExecuteAsync(@"
            CREATE LOGIN [AppUser] WITH PASSWORD = 'SecurePassword123!';
            CREATE USER [AppUser] FOR LOGIN [AppUser];
            
            -- Grant only necessary permissions
            GRANT SELECT, INSERT, UPDATE ON dbo.Accounts TO [AppUser];
            GRANT EXECUTE ON dbo.GetAccountsByName TO [AppUser];
            
            -- Explicitly deny dangerous permissions
            DENY ALTER ANY TABLE TO [AppUser];
            DENY DROP ANY TABLE TO [AppUser];
            DENY EXECUTE ON SCHEMA::sys TO [AppUser];
        ");
    }
    
    [TestMethod]
    public async Task LimitedPrivilegeUser_CannotExecuteDestructiveCommands()
    {
        var limitedConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=MyApp;User Id=AppUser;Password=SecurePassword123!;";
        
        using var connection = new SqlConnection(limitedConnectionString);
        await connection.OpenAsync();
        
        // This should fail due to insufficient privileges
        var ex = await Assert.ThrowsAsync<SqlException>(() =>
            connection.ExecuteAsync("DROP TABLE Accounts"));
            
        Assert.That(ex.Message, Contains.Substring("permission"));
    }
}
```

## NoSQL Injection Prevention

Code injection isn't limited to SQL databases. NoSQL databases can also be vulnerable:

### MongoDB Injection Example

```csharp
public class MongoInjectionExample
{
    private readonly IMongoDatabase _database;
    
    [TestMethod]
    public async Task VulnerableMongoQuery_AllowsInjection()
    {
        var userInput = "{ $ne: null }"; // This bypasses the intended filter
        
        // VULNERABLE: Direct string interpolation in MongoDB queries
        var vulnerableFilter = $"{{ name: '{userInput}' }}";
        var bsonDocument = BsonDocument.Parse(vulnerableFilter);
        
        // This could return all documents instead of filtering by name
        var results = await _database.GetCollection<Account>("accounts")
            .Find(bsonDocument)
            .ToListAsync();
    }
    
    [TestMethod]
    public async Task SecureMongoQuery_PreventsByParameterization()
    {
        var userInput = "{ $ne: null }";
        
        // SECURE: Using proper filter builders
        var filter = Builders<Account>.Filter.Eq(a => a.Name, userInput);
        
        var results = await _database.GetCollection<Account>("accounts")
            .Find(filter)
            .ToListAsync();
            
        // The malicious input is treated as literal text, not as MongoDB operators
        Assert.AreEqual(0, results.Count);
    }
}
```

## Defense in Depth Strategy

Implement multiple layers of protection:

### 1. Application Layer

```csharp
public class SecureDataService
{
    private readonly SqlDbContext _context;
    private readonly ILogger<SecureDataService> _logger;
    
    public async Task<List<Account>> SearchAccountsAsync(string searchTerm)
    {
        // Layer 1: Input validation
        if (!await InputValidator.IsValidAccountNameAsync(searchTerm))
        {
            _logger.LogWarning("Invalid search term rejected: {SearchTerm}", searchTerm);
            throw new ArgumentException("Invalid search term");
        }
        
        // Layer 2: Parameterized query
        var results = await _context.Accounts
            .Where(a => a.Name.Contains(searchTerm))
            .ToListAsync();
            
        // Layer 3: Output sanitization if needed
        return results.Select(a => new Account 
        { 
            Name = HttpUtility.HtmlEncode(a.Name),
            // ... other properties
        }).ToList();
    }
}
```

### 2. Configuration and Monitoring

```csharp
[TestMethod]
public async Task LoggingAndMonitoring_DetectsSuspiciousActivity()
{
    var suspiciousInputs = new[]
    {
        "'; DROP TABLE",
        "' OR 1=1",
        "UNION SELECT",
        "../../../etc/passwd"
    };
    
    var loggedEvents = new List<string>();
    
    foreach (var input in suspiciousInputs)
    {
        try
        {
            await InputValidator.IsValidAccountNameAsync(input);
        }
        catch
        {
            // Log security events for monitoring
            loggedEvents.Add($"Potential injection attempt: {input}");
        }
    }
    
    Assert.That(loggedEvents.Count, Is.GreaterThan(0), "Security events should be logged");
}
```

## Best Practices Summary

1. **Always use parameterized queries** - Never concatenate user input directly into SQL
2. **Validate input** - Use whitelist validation and reject suspicious patterns
3. **Principle of least privilege** - Database users should have minimal necessary permissions
4. **Use ORM properly** - LINQ queries are automatically parameterized
5. **Sanitize output** - Encode data when displaying to prevent XSS
6. **Monitor and log** - Track suspicious activity for security analysis
7. **Regular security testing** - Include injection testing in your security validation

## Testing Your Defenses

```csharp
[TestClass]
public class InjectionDefenseTests
{
    [TestMethod]
    [DataRow("'; DROP TABLE Users; --")]
    [DataRow("' OR 1=1 --")]
    [DataRow("' UNION SELECT password FROM admin --")]
    [DataRow("admin'/*")]
    [DataRow("1' EXEC xp_cmdshell('calc') --")]
    public async Task SecurityDefenses_RejectCommonInjectionPatterns(string maliciousInput)
    {
        // Test that your security measures block common injection attempts
        var isBlocked = await SecurityService.IsInputBlockedAsync(maliciousInput);
        Assert.IsTrue(isBlocked, $"Security defense should block: {maliciousInput}");
    }
}
```

Remember: Security is not a feature you add later—it must be built into your application from the ground up. Code injection prevention is critical for protecting your data and your users.

---

**Navigation:**

- Previous page: [Testing](./testing.md)
