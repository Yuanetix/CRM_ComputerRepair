using Microsoft.AspNetCore.Authorization;
using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.api.Services;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Controllers;

[ApiController]
[Route("tenant/{companyId:int}/retention")]
[Authorize(Roles = "Manager,Admin,Super Admin")]
public class RetentionController : ControllerBase
{
    private readonly ITenantDbContextFactory _factory;
    private readonly RetentionEngine _engine;
    private readonly IAuditWriter _audit;
    private readonly IEmailDeliveryService _emailDelivery;

    public RetentionController(
        ITenantDbContextFactory factory,
        RetentionEngine engine,
        IAuditWriter audit,
        IEmailDeliveryService emailDelivery)
    {
        _factory = factory;
        _engine = engine;
        _audit = audit;
        _emailDelivery = emailDelivery;
    }

    // ═══════════════════════════════════════════════════════
    // 1. SEGMENTS & RECOMMENDATIONS
    // ═══════════════════════════════════════════════════════

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(int companyId)
    {
        var metrics = await _engine.ComputeMetricsAsync(companyId);
        return Ok(metrics);
    }

    [HttpGet("recommendations")]
    public async Task<IActionResult> GetRecommendations(
        int companyId,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] decimal? minSpend)
    {
        var result = await _engine.BuildRecommendationsAsync(companyId,
            new RevenueFilter
            {
                Category = category,
                Search = search,
                MinSpent = minSpend ?? 0
            });

        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════
    // 2. RETENTION REQUESTS & APPROVALS WORKFLOW
    // ═══════════════════════════════════════════════════════

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(
        int companyId,
        [FromQuery] RetentionRequestStatus? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var query = db.RetentionRequests
            .Include(r => r.Customer)
            .Include(r => r.EmailLogs)
            .AsNoTracking();

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(r => r.SubmittedAt >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(r => r.SubmittedAt <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var list = await query
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new RetentionRequestDto
            {
                RetentionRequestId = r.RetentionRequestId,
                CustomerId = r.CustomerId,
                CustomerName = r.Customer != null ? $"{r.Customer.FirstName} {r.Customer.LastName}".Trim() : $"Customer #{r.CustomerId}",
                CustomerEmail = r.Customer != null ? r.Customer.Email : null,
                CustomerPhone = r.Customer != null ? r.Customer.Phone : null,
                TargetSegment = r.TargetSegment,
                ActionType = r.ActionType,
                ProposedDiscountPercent = r.ProposedDiscountPercent,
                RetentionDetails = r.RetentionDetails,
                ReasonCategory = r.ReasonCategory,
                ReasonNote = r.ReasonNote,
                Status = r.Status,
                SubmittedByUserId = r.SubmittedByUserId,
                SubmittedByName = r.SubmittedByName,
                SubmittedAt = r.SubmittedAt,
                ReviewedByUserId = r.ReviewedByUserId,
                ReviewedByName = r.ReviewedByName,
                ReviewedAt = r.ReviewedAt,
                ReviewRemarks = r.ReviewRemarks,
                RejectionReason = r.RejectionReason,
                AddedToCampaign = r.AddedToCampaign,
                CampaignAddedAt = r.CampaignAddedAt,
                CampaignEmailLogId = r.EmailLogs.Select(l => (int?)l.RetentionEmailLogId).FirstOrDefault(),
                IsDispatched = r.EmailLogs.Any(l => l.IsDispatched)
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("requests/{id:int}")]
    public async Task<IActionResult> GetRequestById(int companyId, int id)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var r = await db.RetentionRequests
            .Include(x => x.Customer)
            .Include(x => x.EmailLogs)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RetentionRequestId == id);

        if (r is null) return NotFound($"Retention request {id} not found.");

        var dto = new RetentionRequestDto
        {
            RetentionRequestId = r.RetentionRequestId,
            CustomerId = r.CustomerId,
            CustomerName = r.Customer != null ? $"{r.Customer.FirstName} {r.Customer.LastName}".Trim() : $"Customer #{r.CustomerId}",
            CustomerEmail = r.Customer != null ? r.Customer.Email : null,
            CustomerPhone = r.Customer != null ? r.Customer.Phone : null,
            TargetSegment = r.TargetSegment,
            ActionType = r.ActionType,
            ProposedDiscountPercent = r.ProposedDiscountPercent,
            RetentionDetails = r.RetentionDetails,
            ReasonCategory = r.ReasonCategory,
            ReasonNote = r.ReasonNote,
            Status = r.Status,
            SubmittedByUserId = r.SubmittedByUserId,
            SubmittedByName = r.SubmittedByName,
            SubmittedAt = r.SubmittedAt,
            ReviewedByUserId = r.ReviewedByUserId,
            ReviewedByName = r.ReviewedByName,
            ReviewedAt = r.ReviewedAt,
            ReviewRemarks = r.ReviewRemarks,
            RejectionReason = r.RejectionReason,
            AddedToCampaign = r.AddedToCampaign,
            CampaignAddedAt = r.CampaignAddedAt,
            CampaignEmailLogId = r.EmailLogs.Select(l => (int?)l.RetentionEmailLogId).FirstOrDefault(),
            IsDispatched = r.EmailLogs.Any(l => l.IsDispatched)
        };

        return Ok(dto);
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest(
        int companyId, [FromBody] CreateRetentionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers.FindAsync(request.CustomerId);
        if (customer is null) return NotFound($"Customer {request.CustomerId} not found.");

        var currentUserId = UserSessionHelper.GetUserId(HttpContext) ?? "manager";
        var currentUserName = User.Identity?.Name ?? currentUserId;

        var entity = new RetentionRequest
        {
            CustomerId = request.CustomerId,
            TargetSegment = request.TargetSegment,
            ActionType = request.ActionType.Trim(),
            ProposedDiscountPercent = request.ProposedDiscountPercent,
            RetentionDetails = request.RetentionDetails.Trim(),
            ReasonCategory = request.ReasonCategory.Trim(),
            ReasonNote = request.ReasonNote?.Trim(),
            Status = RetentionRequestStatus.Pending,
            SubmittedByUserId = currentUserId,
            SubmittedByName = currentUserName,
            SubmittedAt = DateTime.UtcNow,
            AddedToCampaign = false
        };

        db.RetentionRequests.Add(entity);
        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            currentUserId,
            "Create",
            "RetentionRequest",
            entity.RetentionRequestId.ToString(),
            $"Created retention request for {customer.FirstName} {customer.LastName} ({request.ProposedDiscountPercent}% - {request.ReasonCategory})");

        return CreatedAtAction(nameof(GetRequestById),
            new { companyId, id = entity.RetentionRequestId },
            new { entity.RetentionRequestId, message = "Retention request submitted successfully." });
    }

    [HttpPost("requests/{id:int}/approve")]
    [Authorize(Roles = "Admin,Super Admin")]
    public async Task<IActionResult> ApproveRequest(
        int companyId, int id, [FromBody] ApproveRetentionRequest approval)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var r = await db.RetentionRequests
            .Include(x => x.Customer)
            .Include(x => x.EmailLogs)
            .FirstOrDefaultAsync(x => x.RetentionRequestId == id);

        if (r is null) return NotFound($"Retention request {id} not found.");

        if (r.Status != RetentionRequestStatus.Pending)
            return BadRequest($"Request is already {r.Status}. Only pending requests can be approved.");

        var currentUserId = UserSessionHelper.GetUserId(HttpContext) ?? "admin";
        var currentUserName = User.Identity?.Name ?? currentUserId;

        // ── SELF-APPROVAL PREVENTION ──
        if (string.Equals(r.SubmittedByUserId, currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Self-approval is forbidden. A request must be reviewed and approved by a different administrator.");
        }

        r.Status = RetentionRequestStatus.Approved;
        r.ReviewedByUserId = currentUserId;
        r.ReviewedByName = currentUserName;
        r.ReviewedAt = DateTime.UtcNow;
        r.ReviewRemarks = approval.ReviewRemarks?.Trim();

        // ── AUTOMATIC IDEMPOTENT CAMPAIGN ADDITION ──
        RetentionEmailLog? campaignLog = r.EmailLogs.FirstOrDefault();
        if (campaignLog is null)
        {
            var settings = await _engine.GetSettingsAsync(db);
            var template = await db.RetentionEmailTemplates
                .FirstOrDefaultAsync(t => t.Segment == r.TargetSegment && t.IsActive);

            var promoCode = $"FIXORY-{r.TargetSegment.ToString().ToUpperInvariant()}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
            var validityDays = template?.ValidityDays ?? settings.DefaultOfferValidityDays;

            var subject = template?.Subject ?? $"{r.ProposedDiscountPercent:0.#}% Off Your Next Fixory Computer Tune-Up or Repair";
            var formattedBody = _engine.FormatRetentionEmail(
                r.Customer!,
                r.TargetSegment,
                r.ProposedDiscountPercent,
                promoCode,
                validityDays,
                r.RetentionDetails,
                template);

            campaignLog = new RetentionEmailLog
            {
                RetentionRequestId = r.RetentionRequestId,
                CustomerId = r.CustomerId,
                RecipientName = $"{r.Customer!.FirstName} {r.Customer.LastName}".Trim(),
                RecipientEmail = r.Customer.Email ?? "customer@example.local",
                Subject = subject,
                FormattedBody = formattedBody,
                Segment = r.TargetSegment,
                DiscountPercent = r.ProposedDiscountPercent,
                PromoCode = promoCode,
                ValidUntil = DateTime.UtcNow.AddDays(validityDays),
                IsDispatched = false,
                IsAutomated = true,
                CreatedAt = DateTime.UtcNow,
                DeliveryStatus = "Pending"
            };

            db.RetentionEmailLogs.Add(campaignLog);
            r.AddedToCampaign = true;
            r.CampaignAddedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            currentUserId,
            "Approve",
            "RetentionRequest",
            r.RetentionRequestId.ToString(),
            $"Approved retention request for {r.Customer?.FirstName} {r.Customer?.LastName} and automatically added to email campaigns.");

        return Ok(new
        {
            message = "Retention request approved and campaign created successfully.",
            requestId = r.RetentionRequestId,
            campaignLogId = campaignLog.RetentionEmailLogId
        });
    }

    [HttpPost("requests/{id:int}/reject")]
    [Authorize(Roles = "Admin,Super Admin")]
    public async Task<IActionResult> RejectRequest(
        int companyId, int id, [FromBody] RejectRetentionRequest rejection)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var r = await db.RetentionRequests
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.RetentionRequestId == id);

        if (r is null) return NotFound($"Retention request {id} not found.");

        if (r.Status != RetentionRequestStatus.Pending)
            return BadRequest($"Request is already {r.Status}. Only pending requests can be rejected.");

        var currentUserId = UserSessionHelper.GetUserId(HttpContext) ?? "admin";
        var currentUserName = User.Identity?.Name ?? currentUserId;

        r.Status = RetentionRequestStatus.Rejected;
        r.ReviewedByUserId = currentUserId;
        r.ReviewedByName = currentUserName;
        r.ReviewedAt = DateTime.UtcNow;
        r.RejectionReason = rejection.RejectionReason.Trim();
        r.ReviewRemarks = rejection.ReviewRemarks?.Trim();

        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            currentUserId,
            "Reject",
            "RetentionRequest",
            r.RetentionRequestId.ToString(),
            $"Rejected retention request for {r.Customer?.FirstName} {r.Customer?.LastName}. Reason: {rejection.RejectionReason}");

        return Ok(new
        {
            message = "Retention request rejected successfully.",
            requestId = r.RetentionRequestId
        });
    }

    // ═══════════════════════════════════════════════════════
    // 3. EMAIL CAMPAIGNS & DISPATCHING
    // ═══════════════════════════════════════════════════════

    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaigns(
        int companyId,
        [FromQuery] bool? isDispatched = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var query = db.RetentionEmailLogs
            .Include(l => l.Customer)
            .AsNoTracking();

        if (isDispatched.HasValue)
            query = query.Where(l => l.IsDispatched == isDispatched.Value);

        if (fromDate.HasValue)
            query = query.Where(l => l.CreatedAt >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(l => l.CreatedAt <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var list = await query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new RetentionCampaignDto
            {
                RetentionEmailLogId = l.RetentionEmailLogId,
                RetentionRequestId = l.RetentionRequestId,
                CustomerId = l.CustomerId,
                RecipientName = l.RecipientName,
                RecipientEmail = l.RecipientEmail,
                Subject = l.Subject,
                FormattedBody = l.FormattedBody,
                Segment = l.Segment,
                DiscountPercent = l.DiscountPercent,
                PromoCode = l.PromoCode,
                ValidUntil = l.ValidUntil,
                IsDispatched = l.IsDispatched,
                DispatchedAt = l.DispatchedAt,
                DispatchedByUserId = l.DispatchedByUserId,
                IsAutomated = l.IsAutomated,
                CreatedAt = l.CreatedAt,
                DeliveryStatus = l.DeliveryStatus,
                DeliveryError = l.DeliveryError
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost("campaigns/{id:int}/dispatch")]
    public async Task<IActionResult> DispatchCampaign(
        int companyId, int id, [FromBody] DispatchRetentionEmailRequest? request)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var log = await db.RetentionEmailLogs
            .Include(l => l.Customer)
            .FirstOrDefaultAsync(l => l.RetentionEmailLogId == id);

        if (log is null) return NotFound($"Campaign entry {id} not found.");

        if (log.IsDispatched)
            return BadRequest($"Campaign entry {id} has already been dispatched at {log.DispatchedAt:g}.");

        var settings = await _engine.GetSettingsAsync(db);
        var currentUserId = UserSessionHelper.GetUserId(HttpContext) ?? "manager";

        var subject = !string.IsNullOrWhiteSpace(request?.CustomSubject)
            ? request.CustomSubject.Trim()
            : log.Subject;

        var body = !string.IsNullOrWhiteSpace(request?.CustomBody)
            ? request.CustomBody
            : log.FormattedBody;

        log.Subject = subject;
        log.FormattedBody = body;

        // Execute SMTP delivery
        var result = await _emailDelivery.SendEmailAsync(
            log.RecipientEmail,
            log.RecipientName,
            subject,
            body,
            settings);

        log.IsDispatched = true;
        log.DispatchedAt = DateTime.UtcNow;
        log.DispatchedByUserId = currentUserId;
        log.DeliveryStatus = result.Status;
        log.DeliveryError = result.ErrorMessage;

        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            currentUserId,
            "DispatchEmail",
            "RetentionEmailLog",
            log.RetentionEmailLogId.ToString(),
            $"Dispatched retention email to {log.RecipientEmail} ({result.Status}). Promo: {log.PromoCode}");

        return Ok(new
        {
            message = "Retention email dispatched successfully via SMTP.",
            logId = log.RetentionEmailLogId,
            deliveryStatus = result.Status,
            deliveryError = result.ErrorMessage
        });
    }

    [HttpPost("campaigns/manual-send")]
    public async Task<IActionResult> ManualSend(
        int companyId, [FromBody] SendManualRetentionEmailRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers.FindAsync(request.CustomerId);
        if (customer is null) return NotFound($"Customer {request.CustomerId} not found.");

        if (string.IsNullOrWhiteSpace(customer.Email))
            return BadRequest($"Customer {customer.FirstName} {customer.LastName} has no email address on file.");

        var settings = await _engine.GetSettingsAsync(db);
        var now = DateTime.UtcNow;

        // Anti-fatigue cooldown check (14 days)
        var lastEmail = await db.RetentionEmailLogs
            .Where(l => l.CustomerId == customer.CustomerId && l.IsDispatched && l.DispatchedAt.HasValue)
            .OrderByDescending(l => l.DispatchedAt)
            .FirstOrDefaultAsync();

        if (lastEmail != null && lastEmail.DispatchedAt.HasValue)
        {
            var daysSince = (now - lastEmail.DispatchedAt.Value).TotalDays;
            if (daysSince < settings.AntiFatigueDays && !request.OverrideCooldown)
            {
                var remaining = Math.Max(1, (int)Math.Ceiling(settings.AntiFatigueDays - daysSince));
                return Conflict(new
                {
                    error = "Anti-Fatigue Cooldown Active",
                    message = $"This customer received a retention email {Math.Floor(daysSince)} day(s) ago. Anti-fatigue cooldown requires {settings.AntiFatigueDays} days ({remaining} days remaining). Please confirm override to proceed.",
                    inCooldown = true,
                    daysRemaining = remaining
                });
            }
        }

        var currentUserId = UserSessionHelper.GetUserId(HttpContext) ?? "manager";
        var promoCode = !string.IsNullOrWhiteSpace(request.PromoCode)
            ? request.PromoCode.Trim()
            : $"FIXORY-MANUAL-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";

        // Deliver via SMTP
        var delivery = await _emailDelivery.SendEmailAsync(
            customer.Email,
            $"{customer.FirstName} {customer.LastName}".Trim(),
            request.Subject.Trim(),
            request.Body,
            settings);

        var log = new RetentionEmailLog
        {
            CustomerId = customer.CustomerId,
            RecipientName = $"{customer.FirstName} {customer.LastName}".Trim(),
            RecipientEmail = customer.Email,
            Subject = request.Subject.Trim(),
            FormattedBody = request.Body,
            Segment = request.Segment,
            DiscountPercent = request.DiscountPercent,
            PromoCode = promoCode,
            ValidUntil = now.AddDays(request.ValidityDays),
            IsDispatched = true,
            DispatchedAt = now,
            DispatchedByUserId = currentUserId,
            IsAutomated = false,
            CreatedAt = now,
            DeliveryStatus = delivery.Status,
            DeliveryError = delivery.ErrorMessage
        };

        db.RetentionEmailLogs.Add(log);
        await db.SaveChangesAsync();

        await _audit.WriteAsync(
            currentUserId,
            "ManualSendEmail",
            "RetentionEmailLog",
            log.RetentionEmailLogId.ToString(),
            $"Manual retention email sent to {customer.Email} ({delivery.Status}). Overridden cooldown: {request.OverrideCooldown}");

        return Ok(new
        {
            message = "Manual retention email dispatched successfully via SMTP.",
            logId = log.RetentionEmailLogId,
            deliveryStatus = delivery.Status,
            deliveryError = delivery.ErrorMessage
        });
    }

    // ═══════════════════════════════════════════════════════
    // 4. TEMPLATES & SETTINGS (Admin Only)
    // ═══════════════════════════════════════════════════════

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(int companyId)
    {
        await using var db = await _factory.CreateAsync(companyId);

        var templates = await db.RetentionEmailTemplates
            .AsNoTracking()
            .OrderBy(t => t.Segment)
            .Select(t => new RetentionTemplateDto
            {
                RetentionEmailTemplateId = t.RetentionEmailTemplateId,
                Segment = t.Segment,
                TemplateName = t.TemplateName,
                Subject = t.Subject,
                Body = t.Body,
                DefaultDiscountPercent = t.DefaultDiscountPercent,
                ValidityDays = t.ValidityDays,
                IsActive = t.IsActive
            })
            .ToListAsync();

        return Ok(templates);
    }

    [HttpPut("templates/{id:int}")]
    [Authorize(Roles = "Admin,Super Admin")]
    public async Task<IActionResult> UpdateTemplate(
        int companyId, int id, [FromBody] UpdateRetentionTemplateRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var template = await db.RetentionEmailTemplates.FindAsync(id);
        if (template is null) return NotFound($"Template {id} not found.");

        template.TemplateName = request.TemplateName.Trim();
        template.Subject = request.Subject.Trim();
        template.Body = request.Body;
        template.DefaultDiscountPercent = request.DefaultDiscountPercent;
        template.ValidityDays = request.ValidityDays;
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var currentUserId = UserSessionHelper.GetUserId(HttpContext) ?? "admin";
        await _audit.WriteAsync(
            currentUserId,
            "UpdateTemplate",
            "RetentionEmailTemplate",
            id.ToString(),
            $"Updated retention email template for segment {template.Segment}.");

        return Ok(new { message = "Template updated successfully." });
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(int companyId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var s = await _engine.GetSettingsAsync(db);

        return Ok(new RetentionSettingsDto
        {
            InactiveThresholdDays = s.InactiveThresholdDays,
            AtRiskThresholdDays = s.AtRiskThresholdDays,
            AntiFatigueDays = s.AntiFatigueDays,
            DefaultOfferValidityDays = s.DefaultOfferValidityDays,
            SmtpHost = s.SmtpHost,
            SmtpPort = s.SmtpPort,
            SmtpUsername = s.SmtpUsername,
            SmtpPassword = string.IsNullOrEmpty(s.SmtpPassword) ? "" : "******",
            SmtpFromEmail = s.SmtpFromEmail,
            SmtpFromName = s.SmtpFromName,
            SmtpEnableSsl = s.SmtpEnableSsl
        });
    }

    [HttpPut("settings")]
    [Authorize(Roles = "Admin,Super Admin")]
    public async Task<IActionResult> UpdateSettings(
        int companyId, [FromBody] UpdateRetentionSettingsRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);
        var s = await _engine.GetSettingsAsync(db);

        s.InactiveThresholdDays = request.InactiveThresholdDays;
        s.AtRiskThresholdDays = request.AtRiskThresholdDays;
        s.AntiFatigueDays = request.AntiFatigueDays;
        s.DefaultOfferValidityDays = request.DefaultOfferValidityDays;
        s.SmtpHost = request.SmtpHost?.Trim();
        s.SmtpPort = request.SmtpPort;
        s.SmtpUsername = request.SmtpUsername?.Trim();
        if (!string.IsNullOrWhiteSpace(request.SmtpPassword) && request.SmtpPassword != "******")
            s.SmtpPassword = request.SmtpPassword;
        s.SmtpFromEmail = request.SmtpFromEmail?.Trim();
        s.SmtpFromName = request.SmtpFromName?.Trim();
        s.SmtpEnableSsl = request.SmtpEnableSsl;
        s.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var currentUserId = UserSessionHelper.GetUserId(HttpContext) ?? "admin";
        await _audit.WriteAsync(
            currentUserId,
            "UpdateSettings",
            "RetentionSettings",
            s.RetentionSettingsId.ToString(),
            $"Updated retention thresholds and SMTP settings: Inactive={s.InactiveThresholdDays}d, AtRisk={s.AtRiskThresholdDays}d, AntiFatigue={s.AntiFatigueDays}d.");

        return Ok(new { message = "Retention settings updated successfully." });
    }

    // ═══════════════════════════════════════════════════════
    // 5. EXISTING INTERACTION LOG (Backwards compatibility)
    // ═══════════════════════════════════════════════════════

    [HttpPost("{customerId:int}/contact")]
    public async Task<IActionResult> LogContact(
        int companyId, int customerId,
        [FromBody] RetentionContactRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _factory.CreateAsync(companyId);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        if (customer is null) return NotFound($"Customer {customerId} not found.");

        var interaction = new CustomerInteraction
        {
            CustomerId = customerId,
            InteractionType = InteractionType.Inquiry,
            Status = InteractionStatus.Open,
            Priority = InteractionPriority.Medium,
            Subject = request.Subject.Trim(),
            Notes = request.Notes.Trim(),
            InteractionByUserId = request.PerformedByUserId ?? UserSessionHelper.GetUserId(HttpContext) ?? "manager",
            InteractionDate = DateTime.UtcNow,
            IsActive = true
        };

        db.CustomerInteractions.Add(interaction);
        await db.SaveChangesAsync();

        if (request.ScheduleFollowUpInDays.HasValue && request.ScheduleFollowUpInDays.Value > 0)
        {
            var followUp = new FollowUp
            {
                CustomerId = customerId,
                Subject = $"Follow up: {request.Subject.Trim()}",
                Notes = "Auto-created from Retention outreach.",
                ScheduledAt = DateTime.UtcNow.AddDays(request.ScheduleFollowUpInDays.Value),
                Channel = FollowUpChannel.Call,
                Status = FollowUpStatus.Scheduled,
                AssignedToUserId = request.PerformedByUserId ?? UserSessionHelper.GetUserId(HttpContext) ?? "manager",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            db.FollowUps.Add(followUp);
            await db.SaveChangesAsync();
        }

        return Ok(new
        {
            message = $"Retention contact logged for customer {customerId}.",
            interactionId = interaction.CustomerInteractionId,
            customerId
        });
    }
}