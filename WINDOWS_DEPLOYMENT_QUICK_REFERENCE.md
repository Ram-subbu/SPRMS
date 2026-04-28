# SPRMS API — Windows Deployment Quick Reference

**For Windows Server / On-Premises Deployment**

---

## ⚡ 60-Second Setup (Per Environment)

### Staging Server Setup
```powershell
# As Administrator on staging server

# 1. Navigate to app directory
cd C:\app\sprms-staging

# 2. Set environment variables (persistent)
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Staging", "Machine")
[Environment]::SetEnvironmentVariable("JWT_SECRET", "YOUR_STAGING_JWT_SECRET_HERE", "Machine")
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=192.168.10.139;Database=SPRMS_V1;User Id=syssolutions;Password=STAGING_PASSWORD;TrustServerCertificate=True;", "Machine")
[Environment]::SetEnvironmentVariable("Redis__ConnectionString", "localhost:6379", "Machine")

# 3. Restart to apply variables
Restart-Computer -Force

# 4. Start app (after restart)
cd C:\app\sprms-staging
.\SPRMS.API.exe
```

### Production Server Setup
```powershell
# As Administrator on production server

# 1-3. (Same as staging, but with Production values)
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
[Environment]::SetEnvironmentVariable("JWT_SECRET", "YOUR_NEW_PRODUCTION_JWT_SECRET", "Machine")
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=prod-sql.yourdomain.com;Database=SPRMS_V1;User Id=prod_db_user;Password=PROD_STRONG_PASSWORD;TrustServerCertificate=False;", "Machine")

# 4. Install as Windows Service (optional, for production)
nssm install SPRMS_API "C:\app\sprms-prod\SPRMS.API.exe"
nssm start SPRMS_API
```

---

## 🔧 Quick Command Reference

### Development (Local Machine)
```powershell
# Run with auto-reload
cd d:\code\SPRM\SPRMS.API
dotnet run

# Build only (no run)
dotnet build

# Clean build
dotnet clean && dotnet build

# Check build errors
dotnet build --no-restore
```

### User Secrets Management
```powershell
# View all secrets
dotnet user-secrets list

# Set a secret
dotnet user-secrets set "JWT:Secret" "your_secret_here"

# Remove a secret
dotnet user-secrets remove "JWT:Secret"

# Clear all secrets
dotnet user-secrets clear

# Get path to secrets file
dotnet user-secrets path
```

### Environment Variables (Production)
```powershell
# Set variable (permanent on machine)
[Environment]::SetEnvironmentVariable("VAR_NAME", "value", "Machine")

# View specific variable
$env:VAR_NAME

# View all environment variables
Get-ChildItem -Path Env: | Sort-Object Name

# Clear variable
Remove-Item env:VAR_NAME -ErrorAction SilentlyContinue

# List all SPRMS variables
Get-ChildItem -Path Env: | Where-Object { $_.Name -match "JWT|DB|REDIS|ASPNETCORE" }
```

### Windows Service Management
```powershell
# Start service
Start-Service -Name "SPRMS_API"

# Stop service
Stop-Service -Name "SPRMS_API"

# Restart service
Restart-Service -Name "SPRMS_API"

# View service status
Get-Service -Name "SPRMS_API"

# View service logs
Get-EventLog -LogName "Application" -Source "SPRMS_API" -Newest 10

# Remove service (using NSSM)
nssm remove SPRMS_API confirm
```

### Database Connectivity Testing
```powershell
# Test SQL Server connectivity
sqlcmd -S 192.168.10.139 -U syssolutions -P P@ssw0rd2o17 -d SPRMS_V1 -Q "SELECT @@VERSION"

# Test Redis connectivity
redis-cli -h localhost -p 6379 PING

# Test with password
redis-cli -h localhost -p 6379 -a your_redis_password PING
```

---

## 📋 Deployment Checklist

### Pre-Deployment (1 week before)
- [ ] Create release build
- [ ] Run security tests
- [ ] Document all passwords (in secure location)
- [ ] Create database backup
- [ ] Test rollback procedure

### Day-Of Deployment
- [ ] Set environment variables on target server
- [ ] Copy application files (or pull from repo)
- [ ] Run health check endpoint
- [ ] Verify database connectivity
- [ ] Test API endpoints with sample requests
- [ ] Monitor error logs for 30 minutes
- [ ] Update DNS/firewall if needed
- [ ] Notify users of deployment

### Post-Deployment
- [ ] Monitor logs for errors
- [ ] Run security verification script
- [ ] Verify all integrations working
- [ ] Check monitoring/alerts configured
- [ ] Document any issues encountered
- [ ] Schedule follow-up review (1 week)

---

## 🚨 Emergency Procedures

### If API Won't Start
```powershell
# 1. Check environment variables
Get-ChildItem -Path Env: | Where-Object { $_.Name -match "ASPNETCORE|JWT|DB_" }

# 2. Check if port is in use
netstat -ano | findstr :58231

# 3. Try manual start with verbose output
cd C:\app\sprms
.\SPRMS.API.exe 2>&1 | Tee-Object startup.log

# 4. If still failing, check event logs
Get-EventLog -LogName "Application" -Newest 20 | Format-Table -AutoSize
```

### If Database Connection Fails
```powershell
# 1. Verify server is running
ping 192.168.10.139

# 2. Test credentials
sqlcmd -S 192.168.10.139 -U syssolutions -P P@ssw0rd2o17 -d SPRMS_V1 -Q "SELECT 1"

# 3. Check firewall
netstat -an | findstr :1433

# 4. Check connection string in app
# Verify: Server, Database, User,  Password match credentials above
```

### If Redis Connection Fails
```powershell
# 1. Verify Redis is running
redis-cli PING

# 2. Check port
netstat -ano | findstr :6379

# 3. If using password, test it
redis-cli -a your_redis_password PING

# 4. Check connection string in config
# Format should be: localhost:6379 or hostname:6379
```

### Emergency Rollback
```powershell
# 1. Stop current service
Stop-Service -Name "SPRMS_API"

# 2. Restore from backup
robocopy C:\backups\sprms-previous C:\app\sprms /MIR

# 3. Restore previous environment (if needed)
# Restore database from backup
sqlcmd -S 192.168.10.139 -U syssolutions -P P@ssw0rd2o17 < restore_database.sql

# 4. Start service
Start-Service -Name "SPRMS_API"

# 5. Verify
Get-Service -Name "SPRMS_API"
```

---

## 🔐 Password Rotation (Every 90 Days)

### Step 1: Generate New Passwords
```powershell
# Generate cryptographically secure random password
$SecurePassword = -join ((33..126) | Get-Random -Count 32 | ForEach-Object {[char]$_})
Write-Host "New password: $SecurePassword"

# Keep offline in password manager
```

### Step 2: Update Database Password
```powershell
# SQL Server - change syssolutions password
sqlcmd -S 192.168.10.139 -U syssolutions -P OLD_PASSWORD -d master -Q "ALTER LOGIN syssolutions WITH PASSWORD = 'NEW_PASSWORD'"

# Verify
sqlcmd -S 192.168.10.139 -U syssolutions -P NEW_PASSWORD -d SPRMS_V1 -Q "SELECT 1"
```

### Step 3: Update JWT Secret
```powershell
# Development (local)
cd d:\code\SPRM\SPRMS.API
dotnet user-secrets set "JWT:Secret" "NEW_JWT_SECRET_VERY_LONG_RANDOM_STRING"

# Staging
[Environment]::SetEnvironmentVariable("JWT_SECRET", "NEW_JWT_SECRET", "Machine")

# Production
[Environment]::SetEnvironmentVariable("JWT_SECRET", "NEW_JWT_SECRET", "Machine")
```

### Step 4: Restart Application
```powershell
# Development: Restart dotnet run
# Staging/Production: 
Stop-Service -Name "SPRMS_API"
Start-Service -Name "SPRMS_API"
```

### Step 5: Verify
```powershell
# Test login still works with new JWT_SECRET
# Test database queries still work with new password
# Check logs for authentication errors

Get-EventLog -LogName "Application" -Newest 5 | Select-Object TimeGenerated, Message
```

---

## 📊 Monitoring Setup

### Enable Application Event Logging
```powershell
# Create event source (if doesn't exist)
New-EventLog -LogName Application -Source SPRMS_API -ErrorAction SilentlyContinue

# View SPRMS_API logs
Get-EventLog -LogName "Application" -Source "SPRMS_API" -Newest 20
```

### Setup Log Files
```powershell
# Create log directory
mkdir "C:\logs\sprms" -Force

# Grant app permission to write
icacls "C:\logs\sprms" /grant "NETWORK SERVICE:(OI)(CI)F"

# In appsettings.Production.json, set:
# "Serilog": { "WriteTo": [ { "Name": "File", "Args": { "path": "C:\\logs\\sprms\\api-.txt" } } ] }
```

### Monitor Disk Space
```powershell
# Check log file size (runs daily)
$logPath = "C:\logs\sprms"
Get-ChildItem $logPath -File | Measure-Object -Property Length -Sum | Select-Object @{Name="Size(GB)";Expression={$_.Sum / 1GB}}

# Archive old logs (monthly)
Get-ChildItem $logPath -Filter "*.txt" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddMonths(-1) } | Move-Item -Destination "C:\logs\archive\"
```

---

## 🆘 Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| `EADDRINUSE: address already in use :::58231` | Port already in use | `netstat -ano \| findstr 58231` → kill process |
| `Connect ECONNREFUSED 127.0.0.1:6379` | Redis not running | `redis-cli PING` or start Redis service |
| `Login failed for user 'syssolutions'` | Wrong password or server offline | Test with `sqlcmd` directly |
| `JWT appears to be truncated` | JWT secret too short | Regenerate 64+ character secret |
| `Cannot connect to database` | Connection string incorrect | Verify host, port, database name |
| `Stack trace visible in error response` | Still in Development environment | Set `ASPNETCORE_ENVIRONMENT=Production` |
| `Rate limit exceeded` | Too many requests | Wait 1 minute or increase limit in code |
| `CORS blocked request` | Origin not in allowed list | Add origin to CORS configuration |

---

## 📞 Support Information

**If deployment fails:**

1. **Check error logs**: `Get-EventLog -LogName "Application" -Newest 50`
2. **Check startup output**: Redirect stderr to file
3. **Verify dependencies**: SQL Server running? Redis running? Network connectivity?
4. **Test in DEV first**: Same issue on dev? Then it's code, not environment
5. **Restore from backup**: Use emergency rollback procedure above

**Key Log Locations:**
- Windows Event Log: `Event Viewer` → Application
- App logs (if configured): `C:\logs\sprms\`
- JSON logs: `appsettings.*.json` (Serilog configuration)

---

**Version**: 1.0  
**Last Updated**: March 30, 2026  
**Status**: PRODUCTION READY

