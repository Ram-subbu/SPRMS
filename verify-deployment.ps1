# COMPREHENSIVE VERIFICATION SCRIPT
# Run this PowerShell script to verify complete SPRMS API security setup

param(
    [ValidateSet("Quick", "Full", "Production")]
    [string]$Mode = "Full"
)

$ErrorActionPreference = "Stop"
$WarningPreference = "Continue"

# Color codes
$colors = @{
    Success = "Green"
    Error   = "Red"
    Warning = "Yellow"
    Info    = "Cyan"
}

function Write-Result {
    param([string]$Test, [bool]$Passed, [string]$Message = "")
    $status = if ($Passed) { "✅ PASS" } else { "❌ FAIL" }
    $color = if ($Passed) { $colors.Success } else { $colors.Error }
    
    Write-Host "$status | $Test" -ForegroundColor $color
    if ($Message) {
        Write-Host "       └─ $Message" -ForegroundColor $colors.Info
    }
}

function Write-Section {
    param([string]$Title)
    Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor $colors.Info
    Write-Host "  $Title" -ForegroundColor $colors.Info
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor $colors.Info
}

$testsRun = 0
$testsPass = 0

# ============================================================================
# SECTION 1: USER SECRETS VERIFICATION
# ============================================================================

Write-Section "1. USER SECRETS VERIFICATION"

# Test 1.1: User Secrets initialized
$testsRun++
try {
    $secretPath = dotnet user-secrets path 2>&1
    $passed = -not ($secretPath -like "*not configured*")
    Write-Result "User Secrets folder exists" $passed $secretPath
    if ($passed) { $testsPass++ }
} catch {
    Write-Result "User Secrets folder exists" $false "Error: $_"
}

# Test 1.2: JWT Secret stored
$testsRun++
try {
    $jwtSecret = (dotnet user-secrets list 2>&1 | Select-String "JWT:Secret" -ErrorAction SilentlyContinue)
    $passed = $null -ne $jwtSecret
    Write-Result "JWT:Secret in User Secrets" $passed
    if ($passed) { $testsPass++ }
} catch {
    Write-Result "JWT:Secret in User Secrets" $false "Error: $_"
}

# Test 1.3: Connection string stored
$testsRun++
try {
    $connStr = (dotnet user-secrets list 2>&1 | Select-String "DefaultConnection" -ErrorAction SilentlyContinue)
    $passed = $null -ne $connStr
    Write-Result "ConnectionStrings:DefaultConnection in User Secrets" $passed
    if ($passed) { $testsPass++ }
} catch {
    Write-Result "ConnectionStrings:DefaultConnection in User Secrets" $false "Error: $_"
}

# ============================================================================
# SECTION 2: CONFIGURATION FILES VERIFICATION
# ============================================================================

Write-Section "2. CONFIGURATION FILES VERIFICATION"

# Test 2.1: appsettings.json has no passwords
$testsRun++
try {
    $appSettings = Get-Content "appsettings.json" -Raw
    $passwordExposed = $appSettings -match "P@ssw0rd\d" -or $appSettings -match "password\s*[=:].*\d"
    Write-Result "No hardcoded passwords in appsettings.json" (-not $passwordExposed)
    if (-not $passwordExposed) { $testsPass++ }
} catch {
    Write-Result "No hardcoded passwords in appsettings.json" $false "Error reading file"
}

# Test 2.2: appsettings.Development.json exists
$testsRun++
$devConfigExists = Test-Path "appsettings.Development.json"
Write-Result "appsettings.Development.json exists" $devConfigExists
if ($devConfigExists) { $testsPass++ }

# Test 2.3: appsettings.Staging.json exists
$testsRun++
$stagingConfigExists = Test-Path "appsettings.Staging.json"
Write-Result "appsettings.Staging.json exists" $stagingConfigExists
if ($stagingConfigExists) { $testsPass++ }

# Test 2.4: appsettings.Production.json exists
$testsRun++
$prodConfigExists = Test-Path "appsettings.Production.json"
Write-Result "appsettings.Production.json exists" $prodConfigExists
if ($prodConfigExists) { $testsPass++ }

# ============================================================================
# SECTION 3: BUILD VERIFICATION
# ============================================================================

Write-Section "3. BUILD VERIFICATION"

# Test 3.1: Project builds without errors
$testsRun++
$buildOutput = dotnet build 2>&1
$buildSuccess = $buildOutput -like "*Build succeeded*" -and $buildOutput -NotLike "*error*"

if ($buildSuccess) {
    # Extract warning/error count
    $warningMatch = $buildOutput -match "(\d+) Warning"
    $errorMatch = $buildOutput -match "(\d+) Error"
    $warnings = if ($warningMatch) { $matches[1] } else { "0" }
    $errors = if ($errorMatch) { $matches[1] } else { "0" }
    
    Write-Result "Build succeeds" $true "$errors Errors, $warnings Warnings"
    $testsPass++
} else {
    Write-Result "Build succeeds" $false "See output above"
}

# ============================================================================
# SECTION 4: PROJECT STRUCTURE VERIFICATION
# ============================================================================

Write-Section "4. PROJECT STRUCTURE VERIFICATION"

$expectedDirs = @(
    "Controllers",
    "Middleware", 
    "Services",
    "Data",
    "DTOs",
    "Common"
)

foreach ($dir in $expectedDirs) {
    $testsRun++
    $exists = Test-Path $dir
    Write-Result "Directory exists: $dir" $exists
    if ($exists) { $testsPass++ }
}

# ============================================================================
# SECTION 5: KEY FILES VERIFICATION (ONLY IN FULL/PRODUCTION MODE)
# ============================================================================

if ($Mode -in @("Full", "Production")) {
    Write-Section "5. KEY FILES CONTENT VERIFICATION"
    
    # Test 5.1: Program.cs has JWT configuration
    $testsRun++
    try {
        $program = Get-Content "Program.cs" -Raw
        $hasJWT = $program -like "*AddAuthentication*" -and $program -like "*JwtBearerDefaults*"
        Write-Result "Program.cs has JWT configuration" $hasJWT
        if ($hasJWT) { $testsPass++ }
    } catch {
        Write-Result "Program.cs has JWT configuration" $false "Error reading file"
    }
    
    # Test 5.2: Middleware has security headers
    $testsRun++
    try {
        $middleware = Get-Content "Middleware/AllMiddleware.cs" -Raw
        $hasHeaders = $middleware -like "*X-Content-Type-Options*" -and $middleware -like "*Strict-Transport-Security*"
        Write-Result "Security headers configured in middleware" $hasHeaders
        if ($hasHeaders) { $testsPass++ }
    } catch {
        Write-Result "Security headers configured in middleware" $false "Error reading file"
    }
    
    # Test 5.3: No sealed LogItem class
    $testsRun++
    try {
        $interfaces = Get-Content "Common/Interfaces.cs" -Raw
        $hasSealed = $interfaces -like "*sealed class LogItem*"
        Write-Result "LogItem is not sealed (inheritance enabled)" (-not $hasSealed)
        if (-not $hasSealed) { $testsPass++ }
    } catch {
        Write-Result "LogItem is not sealed (inheritance enabled)" $false "Error reading file"
    }
}

# ============================================================================
# SECTION 6: RUNTIME VERIFICATION (IF API IS RUNNING)
# ============================================================================

if ($Mode -in @("Full", "Production")) {
    Write-Section "6. RUNTIME VERIFICATION"
    
    # Check if API is accessible
    $apiHealth = $null
    try {
        $apiHealth = Invoke-WebRequest -Uri "http://localhost:58232/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
    } catch {
        # API might not be running, that's okay for verification
    }
    
    if ($null -ne $apiHealth) {
        # Test 6.1: API health endpoint
        $testsRun++
        $isHealthy = $apiHealth.StatusCode -eq 200
        Write-Result "API health endpoint accessible" $isHealthy
        if ($isHealthy) { $testsPass++ }
        
        # Test 6.2: Security headers present
        $testsRun++
        $hasSecurityHeaders = ($apiHealth.Headers.ContainsKey("Strict-Transport-Security") -or 
                              $apiHealth.Headers.ContainsKey("X-Content-Type-Options"))
        Write-Result "Security headers present in responses" $hasSecurityHeaders
        if ($hasSecurityHeaders) { $testsPass++ }
    } else {
        Write-Host "`n⏭️  API not currently running (start with: dotnet run)" -ForegroundColor $colors.Warning
    }
}

# ============================================================================
# SECTION 7: DEPENDENCY SCAN (ONLY IN PRODUCTION MODE)
# ============================================================================

if ($Mode -eq "Production") {
    Write-Section "7. DEPENDENCY VULNERABILITY SCAN"
    
    # Test 7.1: No vulnerable packages
    $testsRun++
    $vulnCheck = dotnet list package --vulnerable 2>&1
    $hasVulnerabilities = $vulnCheck -like "*no vulnerable packages*" -or $vulnCheck -like "*0 vulnerable*"
    Write-Result "No vulnerable NuGet packages" $hasVulnerabilities
    if ($hasVulnerabilities) { $testsPass++ }
}

# ============================================================================
# FINAL SUMMARY
# ============================================================================

Write-Section "VERIFICATION SUMMARY"

$percentage = if ($testsRun -gt 0) { [math]::Round(($testsPass / $testsRun) * 100) } else { 0 }

Write-Host "`nTests Run:   $testsRun" -ForegroundColor $colors.Info
Write-Host "Tests Pass:  $testsPass" -ForegroundColor $colors.Info
Write-Host "Tests Fail:  $($testsRun - $testsPass)" -ForegroundColor $colors.Info
Write-Host "`nScore: $percentage%`n" -ForegroundColor (if ($percentage -ge 90) { $colors.Success } else { $colors.Warning })

if ($percentage -eq 100) {
    Write-Host "🎉 ALL CHECKS PASSED! System is ready for deployment." -ForegroundColor $colors.Success
    exit 0
} elseif ($percentage -ge 90) {
    Write-Host "✅ Most checks passed. Review failures above." -ForegroundColor $colors.Info
    exit 1
} else {
    Write-Host "⚠️  Some checks failed. Review failures above." -ForegroundColor $colors.Warning
    exit 1
}

<#
.SYNOPSIS
Comprehensive verification script for SPRMS API security setup

.DESCRIPTION
Checks User Secrets, configuration files, build status, and runtime security

.PARAMETER Mode
  Quick   - Basic checks only (1 minute)
  Full    - All checks + code review (5 minutes)
  Production - All checks + vulnerability scan (10 minutes)

.EXAMPLE
.\verify-deployment.ps1 -Mode Full

.NOTES
Run from: d:\code\SPRM\SPRMS.API
Requires: PowerShell 5.1+, .NET 8 SDK
#>

