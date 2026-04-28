using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPRMS.Common;
using SPRMS.API.Domain.Entities;
using SPRMS.API.Infrastructure.Persistence;
using ApplicationStatus = SPRMS.Domain.Enums.ApplicationStatus;
using Microsoft.EntityFrameworkCore;
using SPRMS.Domain.Enums;

namespace SPRMS.Controllers;

/// <summary>
/// Payment management for scholarship disbursements.
/// Tracks payment requests, approvals, and disbursements to beneficiaries.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class PaymentsController(AppDbContext db, ILogChannel log) : BaseController
{
    /// <summary>
    /// Get all payment requests with pagination and filtering.
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page (default 20)</param>
    /// <param name="status">Filter by status (Pending, Approved, Rejected, Disbursed, Cancelled)</param>
    /// <returns>List of payment requests</returns>
    [HttpGet]
    [Authorize(Roles = "Admin,Finance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        try
        {
            pageSize = Math.Min(pageSize, 100);

            var query = db.PaymentRequests.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Status>(status, out var parsedStatus))
                query = query.Where(p => p.Status == parsedStatus);

            var total = await query.CountAsync(ct);
            var payments = await query
                .Include(p => p.Application)
                .ThenInclude(a => a!.StudentProfile)
                .ThenInclude(sp => sp!.User)
                .OrderByDescending(p => p.CreatedOn)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.PaymentID,
                    p.PaymentNumber,
                    BeneficiaryName = p.Application!.StudentProfile!.User!.FullName,
                    BeneficiaryCID = p.Application.StudentProfile.User!.CIDNumber,
                    p.Amount,
                    p.Status,
                    p.DisbursedOn,
                    p.CreatedOn
                })
                .ToListAsync(ct);

            return Success(new
            {
                data = payments,
                pagination = new { page, pageSize, total, pages = (int)Math.Ceiling(total / (decimal)pageSize) }
            });
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Payment", FunctionName: nameof(GetPayments),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to retrieve payments");
        }
    }

    /// <summary>
    /// Get detailed payment request information.
    /// </summary>
    /// <param name="id">Payment ID</param>
    /// <returns>Payment details including application and audit trail</returns>
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Finance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayment(long id, CancellationToken ct = default)
    {
        try
        {
            var payment = await db.PaymentRequests
                .Include(p => p.Application)
                .ThenInclude(a => a!.StudentProfile)
                .ThenInclude(sp => sp!.User)
                .Include(p => p.Application!)
                .ThenInclude(a => a!.Program)
                .FirstOrDefaultAsync(p => p.PaymentID == id, ct);

            if (payment == null)
                return NotFound(new { message = "Payment not found" });

            var result = new
            {
                payment.PaymentID,
                payment.PaymentNumber,
                BeneficiaryName = payment.Application?.StudentProfile?.User?.FullName,
                BeneficiaryCID = payment.Application?.StudentProfile?.User?.CIDNumber,
                BeneficiaryEmail = payment.Application?.StudentProfile?.User?.Email,
                Program = payment.Application?.Program?.ProgramName,
                payment.Amount,
                payment.Status,
                payment.PaymentMethod,
                payment.AccountNumber,
                payment.ApprovedBy,
                payment.ApprovedOn,
                payment.RejectionReason,
                payment.DisbursedOn,
                payment.CreatedOn
            };

            return Success(result);
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Payment", FunctionName: nameof(GetPayment),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to retrieve payment");
        }
    }

    /// <summary>
    /// Create a new payment request for an approved application.
    /// </summary>
    /// <param name="request">Payment request details</param>
    /// <returns>Created payment request</returns>
    [HttpPost]
    [Authorize(Roles = "Admin,Finance")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        CancellationToken ct = default)
    {
        try
        {
            // Validate application exists and is approved
            var application = await db.ScholarshipApplications
                .Include(a => a.Program)
                .FirstOrDefaultAsync(a => a.ApplicationID == request.ApplicationID && a.Status == ApplicationStatus.Approved, ct);

            if (application == null)
                return ValidationError(new List<string> { "Application not found or not approved" });

            // Check if payment already exists
            var existingPayment = await db.PaymentRequests
                .FirstOrDefaultAsync(p => p.ApplicationID == request.ApplicationID && p.Status != Status.Rejected, ct);

            if (existingPayment != null)
                return ValidationError(new List<string> { "Payment already exists for this application" });

            // Validate amount
            if (request.Amount <= 0)
                return ValidationError(new List<string> { "Amount must be positive" });

            var payment = new PaymentRequest
            {
                PaymentNumber = $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{new Random().Next(1000)}",
                ApplicationID = request.ApplicationID,
                Amount = request.Amount,
                Status = Status.Pending,
                PaymentMethod = request.PaymentMethod ?? "Bank Transfer",
                AccountNumber = request.AccountNumber,
                CreatedBy = User.FindFirst("name")?.Value ?? "system",
                CreatedOn = DateTime.UtcNow
            };

            db.PaymentRequests.Add(payment);
            await db.SaveChangesAsync(ct);

            log.WriteEvent(new EventLogWrite(
                Action: "PAYMENT_CREATED", UserID: long.Parse(User.FindFirst("uid")?.Value ?? "0"),
                Username: User.FindFirst("name")?.Value ?? "system",
                Description: $"Payment request created: {payment.PaymentNumber} for amount {request.Amount}",
                Outcome: "Success"));

            return Created($"api/v1/payments/{payment.PaymentID}", new
            {
                payment.PaymentID,
                payment.PaymentNumber,
                payment.Status
            });
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Payment", FunctionName: nameof(CreatePayment),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to create payment request");
        }
    }

    /// <summary>
    /// Approve a payment request (Finance manager only).
    /// </summary>
    /// <param name="id">Payment ID</param>
    /// <param name="request">Approval details</param>
    /// <returns>Approval confirmation</returns>
    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin,Finance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApprovePayment(
        long id,
        [FromBody] ApprovePaymentRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var payment = await db.PaymentRequests.FindAsync(new object[] { id }, cancellationToken: ct);
            if (payment == null)
                return NotFound(new { message = "Payment not found" });

            if (payment.Status != Status.Pending)
                return ValidationError(new List<string> { "Only pending payments can be approved" });

            payment.Status = Status.Approved;
            payment.ApprovedBy = User.FindFirst("name")?.Value ?? "system";
            payment.ApprovedOn = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            log.WriteEvent(new EventLogWrite(
                Action: "PAYMENT_APPROVED", UserID: long.Parse(User.FindFirst("uid")?.Value ?? "0"),
                Username: User.FindFirst("name")?.Value ?? "system",
                Description: $"Payment approved: {payment.PaymentNumber} for amount {payment.Amount}",
                Outcome: "Success"));

            return Success(new { payment.PaymentID, payment.Status, message = "Payment approved successfully" });
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Payment", FunctionName: nameof(ApprovePayment),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to approve payment");
        }
    }

    /// <summary>
    /// Reject a payment request (Finance manager only).
    /// </summary>
    /// <param name="id">Payment ID</param>
    /// <param name="request">Rejection reason</param>
    /// <returns>Rejection confirmation</returns>
    [HttpPost("{id}/reject")]
    [Authorize(Roles = "Admin,Finance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectPayment(
        long id,
        [FromBody] RejectPaymentRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var payment = await db.PaymentRequests.FindAsync(new object[] { id }, cancellationToken: ct);
            if (payment == null)
                return NotFound(new { message = "Payment not found" });

            if (payment.Status != Status.Pending)
                return ValidationError(new List<string> { "Only pending payments can be rejected" });

            payment.Status = Status.Rejected;
            payment.RejectionReason = request.Reason;
            payment.ApprovedOn = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            log.WriteEvent(new EventLogWrite(
                Action: "PAYMENT_REJECTED", UserID: long.Parse(User.FindFirst("uid")?.Value ?? "0"),
                Username: User.FindFirst("name")?.Value ?? "system",
                Description: $"Payment rejected: {payment.PaymentNumber}. Reason: {request.Reason}",
                Outcome: "Success"));

            return Success(new { payment.PaymentID, payment.Status, message = "Payment rejected" });
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Payment", FunctionName: nameof(RejectPayment),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to reject payment");
        }
    }

    /// <summary>
    /// Mark payment as disbursed (After bank transfer confirmation).
    /// </summary>
    /// <param name="id">Payment ID</param>
    /// <returns>Disbursement confirmation</returns>
    [HttpPost("{id}/disburse")]
    [Authorize(Roles = "Admin,Finance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisbursePayment(long id, CancellationToken ct = default)
    {
        try
        {
            var payment = await db.PaymentRequests.FindAsync(new object[] { id }, cancellationToken: ct);
            if (payment == null)
                return NotFound(new { message = "Payment not found" });

            if (payment.Status != Status.Approved)
                return ValidationError(new List<string> { "Only approved payments can be disbursed" });

            payment.Status = Status.Disbursed;
            payment.DisbursedOn = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            log.WriteEvent(new EventLogWrite(
                Action: "PAYMENT_DISBURSED", UserID: long.Parse(User.FindFirst("uid")?.Value ?? "0"),
                Username: User.FindFirst("name")?.Value ?? "system",
                Description: $"Payment disbursed: {payment.PaymentNumber} for amount {payment.Amount}",
                Outcome: "Success"));

            return Success(new { payment.PaymentID, payment.Status, message = "Payment disbursed successfully" });
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Payment", FunctionName: nameof(DisbursePayment),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to disburse payment");
        }
    }

    /// <summary>
    /// Get payment statistics dashboard.
    /// Total disbursed, pending approvals, rejection rate, etc.
    /// </summary>
    /// <returns>Payment statistics</returns>
    [HttpGet("stats/summary")]
    [Authorize(Roles = "Admin,Finance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentStats(CancellationToken ct = default)
    {
        try
        {
            var total = await db.PaymentRequests.CountAsync(ct);
            var pending = await db.PaymentRequests.CountAsync(p => p.Status == Status.Pending, ct);
            var approved = await db.PaymentRequests.CountAsync(p => p.Status == Status.Approved, ct);
            var disbursed = await db.PaymentRequests.CountAsync(p => p.Status == Status.Disbursed, ct);
            var rejected = await db.PaymentRequests.CountAsync(p => p.Status == Status.Rejected, ct);

            var totalAmount = await db.PaymentRequests
                .Where(p => p.Status != Status.Rejected)
                .SumAsync(p => p.Amount, ct);

            var disbursedAmount = await db.PaymentRequests
                .Where(p => p.Status == Status.Disbursed)
                .SumAsync(p => p.Amount, ct);

            var stats = new
            {
                TotalPayments = total,
                Pending = pending,
                Approved = approved,
                Disbursed = disbursed,
                Rejected = rejected,
                TotalAmount = totalAmount,
                DisbursedAmount = disbursedAmount,
                PendingAmount = (await db.PaymentRequests
                    .Where(p => p.Status == Status.Pending || p.Status == Status.Approved)
                    .SumAsync(p => p.Amount, ct)),
                AverageDisbursement = disbursed > 0 ? disbursedAmount / disbursed : 0m
            };

            return Success(stats);
        }
        catch (Exception ex)
        {
            log.WriteError(new ErrorLogWrite(
                Module: "Payment", FunctionName: nameof(GetPaymentStats),
                Message: ex.Message, StackTrace: ex.StackTrace));
            return Failed("Failed to retrieve statistics");
        }
    }
}

// â”€â”€ Request/Response Models â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public record CreatePaymentRequest(
    long ApplicationID,
    decimal Amount,
    string? PaymentMethod = "Bank Transfer",
    string? AccountNumber = null
);

public record ApprovePaymentRequest(string? Comments = null);

public record RejectPaymentRequest(string Reason);


