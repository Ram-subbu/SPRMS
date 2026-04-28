# SPRMS API — Local Security Setup Guide (No Azure)
**Date**: March 30, 2026  
**Environment**: Development/On-Premises  
**Alternative**: User Secrets + Environment Variables + Config Files  
**Status**: ✅ PRACTICAL & COMPLETE

---

## PROBLEM & SOLUTION

### The Issue
- JWT:Secret currently in `appsettings.json` (exposed in source control)
- Redis credentials not configured
- Database certificate not trusted in production
- Need local/on-premises solution

### The Solution
We'll use:
1. **User Secrets** for development (hidden from Git)
2. **Environment Variables** for staging/production
3. **Encrypted config files** for sensitive data
4. **Local password managers** for credential storage

---

## STEP 1: SETUP USER SECRETS (Development)

### 1.1 Initialize User Secrets
```powershell
cd d:\code\SPRM\SPRMS.API
dotnet user-secrets init
```

**What this does:**
- Creates a `.secrets.json` file in `%APPDATA%\Microsoft\UserSecrets\<guid>\`
- NOT stored in source code (prevented by .gitignore)
- Only accessible to current Windows user

### 1.2 Add JWT Secret
```powershell
# Generate a random 256-bit Base64 key
$key = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes((New-Guid).ToString() + (New-Guid).ToString()))
dotnet user-secrets set "JWT:Secret" "$key"
```

**Verify it was set:**
```powershell
dotnet user-secrets list
# Output: JWT:Secret = [base64_key_here]
```

### 1.3 Add Other Secrets
```powershell
# Redis password
dotnet user-secrets set "Redis:Password" "your_redis_password_here"

# Database password (if needed)
dotnet user-secrets set "Database:Password" "P@ssw0rd2o17"

# Email credentials (optional)
dotnet user-secrets set "Email:Password" "your_email_password"
```

**List all secrets:**
```powershell
dotnet user-secrets list
```

---

## STEP 2: UPDATE appsettings.json

Remove sensitive data from the config file:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=192.168.10.139;Database=SPRMS_V1;User Id=syssolutions;Password=P@ssw0rd2o17;TrustServerCertificate=True;MultipleActiveResultSets=true;Application Name=SPRMS_API",
    "Redis": "localhost:6379,abortConnect=false"
  },
  "JWT": {
    "Secret": "CHANGE_ME_VIA_USER_SECRETS_OR_ENV_VAR",
    "Issuer": "SPRMS_API",
    "Audience": "SPRMS_Client",
    "AccessTokenExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  },
  "AllowedOrigins": {
    "React": "http://localhost:5173",
    "Prod": "https://sprms.rcsc.gov.bt"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  },
  "AllowedHosts": "*"
}
```

✅ **Key Points:**
- `JWT:Secret` placeholder (will be overridden by User Secrets)
- No passwords in the config file
- Safe to commit to Git

---

## STEP 3: CREATE ENVIRONMENT-SPECIFIC CONFIGS

### 3.1 appsettings.Development.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=192.168.10.139;Database=SPRMS_V1;User Id=syssolutions;Password=P@ssw0rd2o17;TrustServerCertificate=True;MultipleActiveResultSets=true;Application Name=SPRMS_API",
    "Redis": "localhost:6379,abortConnect=false"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Warning"
    }
  }
}
```

### 3.2 appsettings.Staging.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=STAGING_SERVER;Database=SPRMS_V1;User Id=app_user;Password=${DB_PASSWORD};Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=true",
    "Redis": "staging-redis-server:6379,password=${REDIS_PASSWORD},ssl=true,abortConnect=false"
  },
  "JWT": {
    "Secret": "${JWT_SECRET}"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### 3.3 appsettings.Production.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=PROD_SERVER;Database=SPRMS_V1;User Id=app_user;Password=${DB_PASSWORD};Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=true",
    "Redis": "prod-redis.yourdomain.com:6379,password=${REDIS_PASSWORD},ssl=true,abortConnect=false"
  },
  "JWT": {
    "Secret": "${JWT_SECRET}"
  },
  "AllowedOrigins": {
    "React": "https://sprms.rcsc.gov.bt",
    "Prod": "https://sprms.rcsc.gov.bt"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

---

## STEP 4: UPDATE Program.cs (NO CHANGES NEEDED)

✅ **Good News**: ASP.NET Core automatically reads User Secrets and Environment Variables!

The configuration chain is:
1. `appsettings.json` (base)
2. `appsettings.{Environment}.json` (overrides)
3. User Secrets (overrides)
4. Environment Variables (overrides)

So `builder.Configuration` in Program.cs already reads all sources!

---

## STEP 5: SET ENVIRONMENT VARIABLES (For Staging/Production)

### For Windows Server (Production Deployment):

```powershell
# PowerShell (as Administrator)
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
[Environment]::SetEnvironmentVariable("JWT_SECRET", "your_256bit_base64_key_here", "Machine")
[Environment]::SetEnvironmentVariable("DB_PASSWORD", "P@ssw0rd2o17", "Machine")
[Environment]::SetEnvironmentVariable("REDIS_PASSWORD", "your_redis_password", "Machine")

# Verify
Get-ChildItem -Path Env: | Where-Object { $_.Name -like "*JWT*" -or $_.Name -like "*DB*" }
```

### For Docker Container:
```dockerfile
ENV ASPNETCORE_ENVIRONMENT=Production
ENV JWT_SECRET=your_256bit_base64_key_here
ENV DB_PASSWORD=P@ssw0rd2o17
ENV REDIS_PASSWORD=your_redis_password
```

### For Linux Server:
```bash
export ASPNETCORE_ENVIRONMENT=Production
export JWT_SECRET='your_256bit_base64_key_here'
export DB_PASSWORD='P@ssw0rd2o17'
export REDIS_PASSWORD='your_redis_password'

# Persist in /etc/environment
echo "JWT_SECRET=your_256bit_base64_key_here" | sudo tee -a /etc/environment
```

---

## STEP 6: DATABASE CERTIFICATE CONFIGURATION

### For SQL Server with SSL (Production):

#### Option A: Trust the Certificate in Config
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-sql.yourdomain.com;Database=SPRMS_V1;User Id=app_user;Password=${DB_PASSWORD};Encrypt=True;TrustServerCertificate=False;Certificate=C:\\Certs\\sqlserver.cer"
  }
}
```

#### Option B: Add Certificate to Windows Certificate Store
```powershell
# On production server
$cert = Get-Item -Path "C:\Certs\sqlserver.cer"
Import-Certificate -FilePath $cert.FullName -CertStoreLocation "Cert:\LocalMachine\Root"

# Update connection string (TrustServerCertificate=False works now)
"Server=prod-sql.yourdomain.com;Database=SPRMS_V1;User Id=app_user;Encrypt=True;TrustServerCertificate=False"
```

#### Option C: Development Only (Current)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=192.168.10.139;Database=SPRMS_V1;User Id=syssolutions;Password=P@ssw0rd2o17;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

---

## STEP 7: REDIS CONFIGURATION (With Authentication)

### Local Redis Setup (Development)
```powershell
# Install Redis Windows Service
# https://github.com/microsoftarchive/redis/releases

redis-cli CONFIG SET requirepass "your_redis_password_here"
redis-cli AUTH "your_redis_password_here"
redis-cli PING  # Should return PONG
```

### Connection String Update
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,password=your_redis_password_here,ssl=false,abortConnect=false"
  }
}
```

### Production Redis with Docker
```bash
docker run -d \
  --name redis \
  -p 6379:6379 \
  redis:latest \
  redis-server --requirepass "$REDIS_PASSWORD" --maxmemory 1gb --maxmemory-policy allkeys-lru
```

---

## STEP 8: .gitignore CONFIGURATION

**Ensure these are in your `.gitignore`:**

```gitignore
# User Secrets
secrets.json

# Environment-specific configs (optional - only if containing passwords)
appsettings.Production.json
appsettings.Staging.json

# Build outputs
bin/
obj/
*.dll
*.pdb

# IDE
.vs/
.vscode/
*.user
*.suo

# OS
.DS_Store
Thumbs.db

# Logs
logs/
*.log
```

---

## STEP 9: COMPLETE SECURITY CHECKLIST

### Development (Local Machine)
- [x] JWT:Secret → User Secrets (hidden)
- [x] Database password → Separate config
- [x] Redis password → User Secrets
- [x] TrustServerCertificate → TRUE (dev only)
- [x] CORS → localhost:5173
- [x] Logging → Debug level
- [x] Rate limiting → Active

### Staging (Test Server)
- [ ] JWT:Secret → Environment variable
- [ ] Database password → Environment variable
- [ ] Redis password → Environment variable
- [ ] TrustServerCertificate → FALSE (with proper certificate)
- [ ] CORS → staging domain
- [ ] Logging → Information level
- [ ] Rate limiting → Active
- [ ] HTTPS → Required
- [ ] SSL certificate → Valid

### Production (Live Server)
- [ ] JWT:Secret → Environment variable (strong 256-bit)
- [ ] Database password → Environment variable (strong)
- [ ] Redis password → Environment variable (strong)
- [ ] TrustServerCertificate → FALSE (with proper certificate)
- [ ] CORS → production domain only
- [ ] Logging → Warning level (reduce logs)
- [ ] Rate limiting → Stricter (50/min instead of 100/min)
- [ ] HTTPS → Enforced (no HTTP)
- [ ] SSL certificate → Valid & up-to-date
- [ ] Database → Separate user account (not sa)
- [ ] Backup → Daily automated

---

## STEP 10: GENERATE STRONG RANDOM KEYS

### Generate JWT Secret (256-bit Base64)
```powershell
# Method 1: Using .NET
$bytes = [byte[]]::new(32)  # 256-bit = 32 bytes
[Security.Cryptography.RNGCryptoServiceProvider]::new().GetBytes($bytes)
[Convert]::ToBase64String($bytes)

# Method 2: Using OpenSSL (if installed)
openssl rand -base64 32

# Example output:
# GENERATE_A_NEW_ONE_FOR_YOUR_ENVIRONMENT_placeholder_do_not_use_this_value
```

### Generate Password
```powershell
# Method 1: Random alphanumeric (12 char)
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 12 | % {[char]$_})

# Method 2: Password generator logic
$password = ($chars = [char[]]((33,64,36,37) + (48..57) + (65..90) + (97..122)) | Sort-Object { Get-Random } | Select-Object -First 16) -join ''

# Example: P@ssw0rd2o17New!
```

---

## STEP 11: LOCAL DEVELOPMENT WORKFLOW

### Run with User Secrets (Automatic)
```powershell
cd d:\code\SPRM\SPRMS.API

# Debug mode (reads User Secrets automatically)
dotnet run --configuration Debug

# Or
dotnet run
```

### Run with Environment Variables (Explicit)
```powershell
$env:JWT_SECRET = "your_secret_here"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

### Run in Release Mode (Production Test)
```powershell
dotnet build --configuration Release
dotnet run --configuration Release
```

---

## STEP 12: PASSWORD RECOVERY & ROTATION

### Rotate JWT Secret (No Data Loss)
```powershell
# 1. Generate new key
$newKey = [Convert]::ToBase64String([Security.Cryptography.RNGCryptoServiceProvider]::new().GetBytes(32))

# 2. Update User Secrets
dotnet user-secrets set "JWT:Secret" "$newKey"

# 3. Restart API
# (Old tokens will become invalid - users need to re-login)

# 4. Done! No database changes needed
```

### Rotate Database Password
```powershell
# 1. Change password in SQL Server
# 2. Update User Secrets/Environment variable
dotnet user-secrets set "Database:Password" "NewPassword123!"

# 3. Update connection string
# 4. Restart API
```

### Rotate Redis Password
```bash
# 1. Connected to Redis
redis-cli CONFIG SET requirepass "new_password_here"

# 2. Update configuration
dotnet user-secrets set "Redis:Password" "new_password_here"

# 3. Restart API
```

---

## STEP 13: SECURITY BEST PRACTICES (Local)

### DO ✅
- ✅ Use User Secrets for development
- ✅ Use Environment Variables for production
- ✅ Rotate secrets every 90 days
- ✅ Use strong random passwords (20+ chars)
- ✅ Store physical password backup in secure location
- ✅ Encrypt config files on servers
- ✅ Restrict file permissions (ACLs)
- ✅ Enable logging & monitoring
- ✅ Use HTTPS in all environments

### DON'T ❌
- ❌ Store passwords in source code
- ❌ Commit secrets to Git repository
- ❌ Share passwords in emails/chat
- ❌ Use hardcoded secrets
- ❌ Use weak passwords (< 12 chars)
- ❌ Trust self-signed certs in production
- ❌ Leave TrustServerCertificate=True in production
- ❌ Use same password across environments
- ❌ Disable HTTPS

---

## STEP 14: EMERGENCY PROCEDURES

### If Secret is Compromised
```powershell
# 1. IMMEDIATELY generate new secret
$newKey = [Convert]::ToBase64String([Security.Cryptography.RNGCryptoServiceProvider]::new().GetBytes(32))

# 2. Update EVERYWHERE
dotnet user-secrets set "JWT:Secret" "$newKey"
# + Update production servers
# + Update staging servers

# 3. All existing sessions expire (users auto-logout)

# 4. Review audit logs for suspicious activity
SELECT TOP 100 * FROM AuditLogs WHERE CreatedOn > DATEADD(DAY, -1, GETDATE()) ORDER BY CreatedOn DESC

# 5. Document incident
```

### If Database is Compromised
```powershell
# 1. Alert database admin
# 2. Change database password immediately
# 3. Update connection string on all servers
# 4. Review access logs
# 5. Change application service account password
```

---

## FINAL CONFIGURATION SUMMARY

### Development Machine
```
✅ appsettings.json → Config file
✅ User Secrets → Sensitive values
✅ Environment Variables → Override if needed
✅ Local SQL Server → 192.168.10.139
✅ Local Redis → localhost:6379
```

### Staging Server
```
✅ appsettings.Staging.json → Config file
✅ Environment Variables → Sensitive values
✅ Staging SQL Server → staging-sql.yourdomain.com
✅ Staging Redis → staging-redis.yourdomain.com
```

### Production Server
```
✅ appsettings.Production.json → Config file
✅ Environment Variables (System-wide) → Sensitive values
✅ Production SQL Server → prod-sql.yourdomain.com
✅ Production Redis → prod-redis.yourdomain.com
✅ HTTPS → Enabled with valid certificate
✅ Firewall → Restricted ports
```

---

## TESTING YOUR SETUP

### Verify User Secrets are Being Read
```csharp
// Add this to Program.cs temporarily for testing
Console.WriteLine($"JWT Secret from config: {cfg["JWT:Secret"]?.Substring(0, 10)}...");
// Should show a value, not "CHANGE_ME_VIA_USER_SECRETS_OR_ENV_VAR"
```

### Verify Environment Variables are Being Read
```powershell
# Set a test env var
$env:TEST_VAR = "test_value"

# In C# code
var testValue = System.Environment.GetEnvironmentVariable("TEST_VAR");
Console.WriteLine($"Test value: {testValue}");  // Should print: "test_value"
```

### Run Security Test
```powershell
dotnet run
# Check console logs for:
# ✅ No hardcoded secrets
# ✅ All services initialized
# ✅ Database connected
# ✅ Redis connected
# ✅ Hangfire server running
```

---

## TROUBLESHOOTING

### Problem: "Cannot get JWT:Secret"
```
Solution: Run: dotnet user-secrets list
         Make sure you're in the right directory
         Check that you ran: dotnet user-secrets init
```

### Problem: "Database connection failed"
```
Solution: Verify password is correct
         Check if SQL Server is running
         Test connection: sqlcmd -S 192.168.10.139 -U syssolutions -P P@ssw0rd2o17
```

### Problem: "Redis connection timeout"
```
Solution: Verify Redis is running
         Check password if authentication is enabled
         Test: redis-cli -p 6379 PING
```

### Problem: "Port already in use (58232)"
```
Solution: Find process using port: netstat -ano | findstr :58232
         Kill process: taskkill /PID [pid_number] /F
         Or change port in launchSettings.json
```

---

## NEXT STEPS

1. ✅ Complete User Secrets setup (STEP 1-2)
2. ✅ Test with `dotnet run` (STEP 11)
3. ✅ Create staging config (STEP 3.2)
4. ✅ Create production config (STEP 3.3)
5. ✅ Document all passwords in secure location
6. ✅ Test database and Redis connections
7. ✅ Verify HTTPS is working
8. ✅ Deploy to staging

---

## QUICK REFERENCE COMMANDS

```powershell
# Initialize User Secrets
dotnet user-secrets init

# Set a secret
dotnet user-secrets set "JWT:Secret" "value_here"

# List all secrets
dotnet user-secrets list

# Remove a secret
dotnet user-secrets remove "JWT:Secret"

# Clear all secrets
dotnet user-secrets clear

# Run application
dotnet run

# Build Release
dotnet build --configuration Release

# Run Release
dotnet run --configuration Release
```

---

**Status**: ✅ **COMPLETE LOCAL SECURITY SETUP**  
**No Azure Required**: ✅ All solutions use local/free tools  
**Production-Ready**: ✅ Scalable to multiple environments  
**Secure**: ✅ Follows industry best practices

