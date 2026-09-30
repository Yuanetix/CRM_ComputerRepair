using CRM_ComputerRepair.api.Dtos;
using CRM_ComputerRepair.domain.Entities;
using CRM_ComputerRepair.infrastructure.Data;
using CRM_ComputerRepair.infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CRM_ComputerRepair.api.Services;

/// <summary>
/// Retention & Email Marketing Engine for Computer Repair CRM.
/// Evaluates dynamic retention segments from completed repair history:
/// 1. Inactive: Last completed repair was > 180 days ago.
/// 2. At Risk: Last completed repair was 91–180 days ago.
/// 3. Loyal: 3 or more completed repairs AND last repair within past 90 days.
/// 4. Returning: Exactly 2 completed repairs AND last repair within past 90 days.
/// 5. New: Exactly 1 completed repair within past 90 days.
/// </summary>
public class RetentionEngine
{
    private readonly ITenantDbContextFactory _factory;
    private readonly MasterCrmDbContext _master;

    public RetentionEngine(ITenantDbContextFactory factory, MasterCrmDbContext master)
    {
        _factory = factory;
        _master = master;
    }

    public async Task<RetentionSettings> GetSettingsAsync(TenantCrmDbContext db)
    {
        var settings = await db.RetentionSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new RetentionSettings
            {
                InactiveThresholdDays = 180,
                AtRiskThresholdDays = 90,
                AntiFatigueDays = 14,
                DefaultOfferValidityDays = 14,
                SmtpHost = "localhost",
                SmtpPort = 25,
                SmtpFromEmail = "retention@fixorycrm.local",
                SmtpFromName = "Fixory Computer Repair Services",
                SmtpEnableSsl = false,
                UpdatedAt = DateTime.UtcNow
            };
            db.RetentionSettings.Add(settings);
            await db.SaveChangesAsync();
        }
        return settings;
    }

    /// <summary>Compute per-customer loyalty/retention metrics from the tenant DB.</summary>
    public async Task<List<CustomerRetentionMetricsDto>> ComputeMetricsAsync(int companyId)
    {
        await using var db = await _factory.CreateAsync(companyId);
        var settings = await GetSettingsAsync(db);

        var now = DateTime.UtcNow;

        var customers = await db.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync();

        var completedRepairs = await db.RepairRequests.AsNoTracking()
            .Where(r => r.Status == RepairStatus.Completed && r.CompletionDate.HasValue)
            .ToListAsync();

        var payments = await db.Payments.AsNoTracking()
            .Where(p => !p.IsVoid && p.IsPaid)
            .ToListAsync();

        var paymentByRepair = payments
            .GroupBy(p => p.RepairRequestId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var repairsByCustomer = completedRepairs
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Anti-fatigue check: last dispatched email per customer
        var lastEmails = await db.RetentionEmailLogs.AsNoTracking()
            .Where(l => l.IsDispatched && l.DispatchedAt.HasValue)
            .GroupBy(l => l.CustomerId)
            .Select(g => new { CustomerId = g.Key, LastSent = g.Max(x => x.DispatchedAt!.Value) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.LastSent);

        var metrics = new List<CustomerRetentionMetricsDto>();

        foreach (var customer in customers)
        {
            var repairs = repairsByCustomer.TryGetValue(customer.CustomerId, out var list)
                ? list
                : new List<RepairRequest>();

            var txCount = repairs.Count;
            var totalSpent = repairs.Sum(r =>
                paymentByRepair.TryGetValue(r.RepairRequestId, out var amount) ? amount : 0m);

            DateTime? lastTx = repairs.Count > 0
                ? repairs.Max(r => r.CompletionDate)
                : null;

            var daysSinceLastTx = lastTx.HasValue
                ? (int)(now - lastTx.Value).TotalDays
                : (int)(now - customer.CreatedAt).TotalDays;

            var transactions90 = repairs.Count(r =>
                r.CompletionDate!.Value >= now.AddDays(-90));

            var activeMonths = repairs
                .Select(r => new DateTime(r.CompletionDate!.Value.Year, r.CompletionDate.Value.Month, 1))
                .Distinct()
                .Count();

            var services = repairs
                .Select(r => r.DeviceModel)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            // Segment evaluation in exact priority order
            RetentionSegment segment;
            if (txCount > 0 && daysSinceLastTx > settings.InactiveThresholdDays)
            {
                segment = RetentionSegment.Inactive;
            }
            else if (txCount > 0 && daysSinceLastTx > settings.AtRiskThresholdDays)
            {
                segment = RetentionSegment.AtRisk;
            }
            else if (txCount >= 3 && daysSinceLastTx <= 90)
            {
                segment = RetentionSegment.Loyal;
            }
            else if (txCount == 2 && daysSinceLastTx <= 90)
            {
                segment = RetentionSegment.Returning;
            }
            else
            {
                segment = RetentionSegment.New;
            }

            // Anti-fatigue cooldown evaluation
            bool inCooldown = false;
            DateTime? lastEmailDate = null;
            int? daysUntilEligible = null;

            if (lastEmails.TryGetValue(customer.CustomerId, out var sentDate))
            {
                lastEmailDate = sentDate;
                var daysSinceEmail = (now - sentDate).TotalDays;
                if (daysSinceEmail < settings.AntiFatigueDays)
                {
                    inCooldown = true;
                    daysUntilEligible = Math.Max(1, (int)Math.Ceiling(settings.AntiFatigueDays - daysSinceEmail));
                }
            }

            metrics.Add(new CustomerRetentionMetricsDto
            {
                CustomerId = customer.CustomerId,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                Phone = customer.Phone,
                LoyaltyPoints = customer.LoyaltyPoints,
                CreatedAt = customer.CreatedAt,
                IsActive = customer.IsActive,
                CompletedTransactions = txCount,
                TotalSpent = totalSpent,
                LastTransactionDate = lastTx,
                DaysSinceLastTransaction = Math.Max(0, daysSinceLastTx),
                ActiveMonths = activeMonths,
                TransactionsLast90Days = transactions90,
                PreviousServices = string.Join(", ", services),
                LastService = services.LastOrDefault(),
                SegmentEnum = segment,
                InCooldown = inCooldown,
                LastEmailSentDate = lastEmailDate,
                DaysUntilNextEligible = daysUntilEligible
            });
        }

        return metrics;
    }

    /// <summary>Build retention recommendations with human-readable basis and segment matching.</summary>
    public async Task<List<RetentionRecommendationDto>> BuildRecommendationsAsync(
        int companyId, RevenueFilter? filter = null)
    {
        var metrics = await ComputeMetricsAsync(companyId);
        var recommendations = new List<RetentionRecommendationDto>();

        foreach (var m in metrics)
        {
            string category;
            string action;
            string basis;
            string reward;

            switch (m.SegmentEnum)
            {
                case RetentionSegment.Inactive:
                    category = "Re-engagement";
                    action = "Send 15% Win-back Offer & Comprehensive Diagnostics";
                    basis = $"Last completed service was {m.DaysSinceLastTransaction} days ago (>{180}d inactive) · ₱{m.TotalSpent:N0} total spent";
                    reward = "15% discount on full service / diagnostic";
                    break;

                case RetentionSegment.AtRisk:
                    category = "At Risk";
                    action = "Send 10% Preventative Care Tune-up";
                    basis = $"Last service was {m.DaysSinceLastTransaction} days ago (91–180d window) · At risk of churn";
                    reward = "10% off preventative tune-up & cleaning";
                    break;

                case RetentionSegment.Loyal:
                    category = "Loyalty";
                    action = "Grant 10% VIP Loyalty Reward";
                    basis = $"{m.CompletedTransactions} completed repairs with recent service ({m.DaysSinceLastTransaction}d ago) · ₱{m.TotalSpent:N0} total spent";
                    reward = "10% VIP service discount + priority queue";
                    break;

                case RetentionSegment.Returning:
                    category = "Repeat Care";
                    action = "Send Follow-up Upgrade & Checkup Reminder";
                    basis = $"2 completed repairs, active within past 90 days · Regular client";
                    reward = "Free hardware diagnostic check";
                    break;

                case RetentionSegment.New:
                default:
                    category = "New Customer";
                    action = "Send First-Repair Thank You & 5% Next Service";
                    basis = m.CompletedTransactions > 0
                        ? $"1 completed repair on {m.LastService ?? "Device"} ({m.DaysSinceLastTransaction}d ago) · Encourage second visit"
                        : $"New customer registered {m.DaysSinceLastTransaction}d ago · No completed repairs yet";
                    reward = "5% off next maintenance or repair";
                    break;
            }

            recommendations.Add(new RetentionRecommendationDto
            {
                CustomerId = m.CustomerId,
                CustomerName = m.FullName,
                Email = m.Email,
                Phone = m.Phone,
                Segment = m.SegmentEnum,
                SegmentName = m.Segment,
                Category = category,
                Action = action,
                Basis = basis,
                Reward = reward,
                TransactionCount = m.CompletedTransactions,
                TotalSpent = m.TotalSpent,
                DaysSinceLastTransaction = m.DaysSinceLastTransaction,
                Points = m.LoyaltyPoints ?? 0,
                InCooldown = m.InCooldown,
                DaysUntilNextEligible = m.DaysUntilNextEligible,
                LastEmailSentDate = m.LastEmailSentDate
            });
        }

        IEnumerable<RetentionRecommendationDto> result = recommendations;

        if (filter?.Category is { Length: > 0 } cat && cat != "All")
            result = result.Where(r => r.Category.Equals(cat, StringComparison.OrdinalIgnoreCase) ||
                                       r.SegmentName.Equals(cat, StringComparison.OrdinalIgnoreCase));

        if (filter?.MinSpent > 0)
            result = result.Where(r => r.TotalSpent >= filter.MinSpent);

        if (!string.IsNullOrWhiteSpace(filter?.Search))
        {
            var term = filter.Search.Trim();
            result = result.Where(r =>
                r.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.Phone ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Basis.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return result
            .OrderByDescending(r => r.TotalSpent)
            .ThenBy(r => r.InCooldown)
            .ToList();
    }

    /// <summary>
    /// Generates a professional, fully-formatted HTML retention email tailored
    /// specifically to Fixory Computer Repair Services.
    /// Exposes ZERO internal database IDs, column names, or raw system tags.
    /// </summary>
    public string FormatRetentionEmail(
        Customer customer,
        RetentionSegment segment,
        decimal discountPercent,
        string promoCode,
        int validityDays,
        string? customNotes = null,
        RetentionEmailTemplate? template = null)
    {
        var firstName = !string.IsNullOrWhiteSpace(customer.FirstName)
            ? customer.FirstName.Trim()
            : "Valued Customer";

        var expirationDate = DateTime.UtcNow.AddDays(validityDays).ToString("MMMM d, yyyy");

        if (template != null && !string.IsNullOrWhiteSpace(template.Body))
        {
            var bodyText = template.Body
                .Replace("{{customer_name}}", firstName)
                .Replace("{{discount_percent}}", $"{discountPercent:0.#}%")
                .Replace("{{promo_code}}", promoCode)
                .Replace("{{validity_days}}", validityDays.ToString())
                .Replace("{{expiration_date}}", expirationDate)
                .Replace("{{notes}}", customNotes ?? "");

            return WrapHtmlEmail(firstName, bodyText, discountPercent, promoCode, expirationDate);
        }

        string headline;
        string leadParagraph;
        string offerDescription;

        switch (segment)
        {
            case RetentionSegment.Inactive:
                headline = "We Miss You at Fixory! Here is a Special Welcome-Back Offer";
                leadParagraph = "It has been a while since your last service with us. We want to ensure your desktop, laptop, and office hardware continue to run smoothly, safely, and at peak performance.";
                offerDescription = $"Enjoy <strong>{discountPercent:0.#}% OFF</strong> your next full diagnostic, hardware upgrade, or system tune-up. Whether your computer is running slow or needs a routine dust and thermal overhaul, our expert technicians are ready to help.";
                break;

            case RetentionSegment.AtRisk:
                headline = "Time for a Preventative Maintenance Check?";
                leadParagraph = "Over time, dust build-up, software clutter, and cooling wear can impact your computer's speed and reliability. Since it has been a few months since your last visit, we are reaching out with an exclusive preventative service reward.";
                offerDescription = $"Claim <strong>{discountPercent:0.#}% OFF</strong> on any maintenance service, component replacement, or hardware cleaning.";
                break;

            case RetentionSegment.Loyal:
                headline = "Thank You for Being a Preferred Fixory VIP Customer";
                leadParagraph = "We truly appreciate your continued trust in Fixory for your computer service and repair needs. To show our gratitude for your loyalty, we have activated an exclusive VIP discount on your account.";
                offerDescription = $"Receive <strong>{discountPercent:0.#}% OFF</strong> your next repair, component replacement, or custom build service, with complimentary priority bench service.";
                break;

            case RetentionSegment.Returning:
                headline = "Another Device Needing Care? We're Here to Help";
                leadParagraph = "We hope your recently serviced device is running flawlessly! Whether you have another PC that needs attention, an upgrade you've been considering, or questions about device optimization, our repair center is at your service.";
                offerDescription = $"Use promo code <strong>{promoCode}</strong> to save <strong>{discountPercent:0.#}%</strong> on your next repair or upgrade.";
                break;

            case RetentionSegment.New:
            default:
                headline = "Thank You for Choosing Fixory Computer Repair Services";
                leadParagraph = "Thank you for trusting our repair team with your computer. We stand behind our workmanship with our 30-day labor warranty and dedicated support.";
                offerDescription = $"As a special welcome gift, enjoy <strong>{discountPercent:0.#}% OFF</strong> your next accessory, scheduled check-up, or hardware upgrade.";
                break;
        }

        var customNoteHtml = !string.IsNullOrWhiteSpace(customNotes)
            ? $"<div style='background-color:#F8FAFC;border-left:3px solid #0F172A;padding:12px 16px;margin:20px 0;font-size:14px;color:#334155;'><strong>Special Note from Your Technician:</strong><br/>{System.Net.WebUtility.HtmlEncode(customNotes)}</div>"
            : "";

        return $@"<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'/>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
  <title>Fixory Computer Repair</title>
</head>
<body style='margin:0;padding:24px 16px;background-color:#F8FAFC;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;font-size:15px;color:#1E293B;line-height:1.6;'>
  <div style='max-width:580px;margin:0 auto;background:#FFFFFF;border:1px solid #E2E8F0;border-radius:8px;padding:32px 32px;'>
    <div style='border-bottom:1px solid #E2E8F0;padding-bottom:16px;margin-bottom:24px;'>
      <div style='font-size:18px;font-weight:700;color:#0F172A;letter-spacing:-0.01em;'>Fixory Computer Repair Services</div>
      <div style='font-size:13px;color:#64748B;margin-top:2px;'>Device Diagnostics &bull; Maintenance &bull; Hardware Restoration</div>
    </div>

    <div style='color:#334155;'>
      <p style='margin:0 0 16px 0;font-size:15px;'>Dear <strong>{firstName}</strong>,</p>
      <p style='margin:0 0 16px 0;'>{leadParagraph}</p>

      <div style='background-color:#F8FAFC;border:1px solid #E2E8F0;border-radius:6px;padding:20px 24px;margin:24px 0;'>
        <div style='font-size:16px;font-weight:700;color:#0F172A;margin-bottom:6px;'>{headline}</div>
        <div style='font-size:14px;color:#475569;margin-bottom:14px;'>{offerDescription}</div>
        <div style='display:inline-block;background:#0F172A;color:#FFFFFF;padding:6px 14px;border-radius:4px;font-size:14px;font-weight:700;letter-spacing:0.04em;'>PROMO CODE: {promoCode}</div>
        <div style='font-size:12px;color:#64748B;margin-top:8px;'>Valid through: <strong>{expirationDate}</strong></div>
      </div>

      {customNoteHtml}

      <div style='margin:24px 0;'>
        <div style='font-size:14px;font-weight:600;color:#0F172A;margin-bottom:8px;'>How to Redeem:</div>
        <ul style='margin:0;padding-left:20px;font-size:14px;color:#475569;'>
          <li style='margin-bottom:4px;'>Present promo code <strong>{promoCode}</strong> upon check-in at our repair center.</li>
          <li style='margin-bottom:4px;'>Or reply directly to this email to reserve an express diagnosis slot.</li>
        </ul>
      </div>

      <p style='margin:28px 0 0 0;font-size:14px;color:#475569;'>
        Warm regards,<br/>
        <strong style='color:#0F172A;'>Fixory Repair Team</strong><br/>
        <em>Fixory Computer Repair Services</em>
      </p>
    </div>

    <div style='border-top:1px solid #F1F5F9;margin-top:32px;padding-top:16px;font-size:12px;color:#94A3B8;text-align:center;'>
      Fixory Computer Repair Services &bull; Support: (555) 019-2834 &bull; Email: support@fixorycrm.local<br/>
      You are receiving this communication as a registered client of Fixory Computer Repair.
    </div>
  </div>
</body>
</html>";
    }

    private static string WrapHtmlEmail(string firstName, string bodyText, decimal discountPercent, string promoCode, string expirationDate)
    {
        // If bodyText already contains clean plain text, format paragraphs cleanly
        string formattedContent = bodyText;
        if (!bodyText.Contains('<') && !bodyText.Contains('>'))
        {
            formattedContent = string.Join("\n", bodyText
                .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => $"<p style='margin:0 0 16px 0;line-height:1.6;'>{System.Net.WebUtility.HtmlEncode(p).Replace("\r\n", "<br/>").Replace("\n", "<br/>")}</p>"));
        }

        return $@"<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'/>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'/>
  <title>Fixory Computer Repair</title>
</head>
<body style='margin:0;padding:24px 16px;background-color:#F8FAFC;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;font-size:15px;color:#1E293B;line-height:1.6;'>
  <div style='max-width:580px;margin:0 auto;background:#FFFFFF;border:1px solid #E2E8F0;border-radius:8px;padding:32px 32px;'>
    <div style='border-bottom:1px solid #E2E8F0;padding-bottom:16px;margin-bottom:24px;'>
      <div style='font-size:18px;font-weight:700;color:#0F172A;letter-spacing:-0.01em;'>Fixory Computer Repair Services</div>
      <div style='font-size:13px;color:#64748B;margin-top:2px;'>Quality Hardware Diagnostics &bull; Maintenance &bull; Repair Services</div>
    </div>
    <div style='color:#334155;'>
      {formattedContent}
    </div>
    <div style='border-top:1px solid #F1F5F9;margin-top:32px;padding-top:16px;font-size:12px;color:#94A3B8;text-align:center;'>
      Fixory Computer Repair Services &bull; Support: (555) 019-2834 &bull; Email: support@fixorycrm.local
    </div>
  </div>
</body>
</html>";
    }
}