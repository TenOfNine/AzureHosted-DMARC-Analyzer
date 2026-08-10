using System.Net;
using System.Net.Sockets;
using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Core.Dkim;
using DmarcAnalyzer.Core.Dmarc;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Core.Legitimacy;
using DmarcAnalyzer.Core.Spf;
using DmarcAnalyzer.Core.Verification;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DmarcAnalyzer.Infrastructure.Ingestion;

public class DmarcReportIngestionPipeline(
    DmarcAnalyzerDbContext db,
    SpfEvaluator spfEvaluator,
    DkimSelectorChecker dkimSelectorChecker,
    ISpfDnsResolver forwardDnsResolver,
    IReverseDnsResolver reverseDnsResolver,
    ILogger<DmarcReportIngestionPipeline> logger) : IDmarcReportIngestionPipeline
{
    public async Task<IngestionOutcome> ProcessMessageAsync(
        Mailbox mailbox,
        string graphMessageId,
        IReadOnlyList<GraphAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        ExtractedXmlAttachment? extracted = null;
        foreach (var attachment in attachments)
        {
            extracted = AttachmentExtractor.TryExtractXml(attachment.Name, attachment.ContentBytes);
            if (extracted is not null)
            {
                break;
            }
        }

        if (extracted is null)
        {
            return new IngestionOutcome(MessageProcessingStatus.Skipped, AttachmentType.Unknown, "No DMARC aggregate report XML attachment found on this message.");
        }

        ParsedDmarcReport parsed;
        try
        {
            using (extracted.XmlContent)
            {
                parsed = DmarcXmlParser.Parse(extracted.XmlContent);
            }
        }
        catch (DmarcParseException ex)
        {
            logger.LogWarning(ex, "Failed to parse DMARC report XML for message {MessageId} in mailbox {Mailbox}", graphMessageId, mailbox.MailboxUpn);
            return new IngestionOutcome(MessageProcessingStatus.Failed, extracted.Type, ex.Message);
        }

        var policyDomainLower = parsed.Policy.Domain.ToLowerInvariant();
        var domain = await db.Domains.FirstOrDefaultAsync(
            d => d.IsActive && d.DomainName.ToLower() == policyDomainLower,
            cancellationToken);

        if (domain is null)
        {
            return new IngestionOutcome(
                MessageProcessingStatus.Skipped,
                extracted.Type,
                $"Report is for domain '{parsed.Policy.Domain}', which is not a monitored domain.");
        }

        var report = MapReport(parsed, domain, mailbox, graphMessageId);
        db.AggregateReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);

        var overrides = await db.VerifiedSenderOverrides
            .Where(v => v.DomainId == domain.Id)
            .ToListAsync(cancellationToken);

        // Keyed cache so two records in the same report for the same source IP (rare, but not
        // impossible) reuse one tracked SenderReputation instance instead of racing to insert two
        // rows that would violate the (DomainId, SourceIp) unique index at SaveChanges.
        var reputationCache = new Dictionary<string, SenderReputation>();

        foreach (var record in report.Records)
        {
            await EvaluateSpfAsync(domain.DomainName, record, cancellationToken);
            await CheckDkimSelectorsAsync(record, cancellationToken);
            await UpsertSenderReputationAsync(domain, record, overrides, reputationCache, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new IngestionOutcome(MessageProcessingStatus.Success, extracted.Type, null);
    }

    private static DmarcAggregateReport MapReport(ParsedDmarcReport parsed, Domain domain, Mailbox mailbox, string graphMessageId)
    {
        var report = new DmarcAggregateReport
        {
            Id = Guid.NewGuid(),
            DomainId = domain.Id,
            MailboxId = mailbox.Id,
            GraphMessageId = graphMessageId,
            ReportId = parsed.Metadata.ReportId,
            OrgName = parsed.Metadata.OrgName,
            Email = parsed.Metadata.Email,
            ExtraContactInfo = parsed.Metadata.ExtraContactInfo,
            DateRangeBeginUtc = parsed.Metadata.DateRangeBeginUtc,
            DateRangeEndUtc = parsed.Metadata.DateRangeEndUtc,
            PolicyPublishedDomain = parsed.Policy.Domain,
            PolicyAdkim = DmarcEnumMapper.MapAlignmentMode(parsed.Policy.Adkim),
            PolicyAspf = DmarcEnumMapper.MapAlignmentMode(parsed.Policy.Aspf),
            PolicyP = DmarcEnumMapper.MapDisposition(parsed.Policy.P),
            PolicySp = parsed.Policy.Sp is null ? null : DmarcEnumMapper.MapDisposition(parsed.Policy.Sp),
            PolicyPct = parsed.Policy.Pct,
            PolicyFo = parsed.Policy.Fo,
            ReceivedUtc = DateTime.UtcNow
        };

        foreach (var parsedRecord in parsed.Records)
        {
            var record = new DmarcRecord
            {
                Id = Guid.NewGuid(),
                SourceIp = parsedRecord.SourceIp,
                Count = parsedRecord.Count,
                PolicyEvaluatedDisposition = DmarcEnumMapper.MapDisposition(parsedRecord.Disposition),
                PolicyEvaluatedDkim = DmarcEnumMapper.MapPolicyResult(parsedRecord.PolicyEvaluatedDkim),
                PolicyEvaluatedSpf = DmarcEnumMapper.MapPolicyResult(parsedRecord.PolicyEvaluatedSpf),
                PolicyOverrideReasons = parsedRecord.PolicyOverrideReasons is { Count: > 0 }
                    ? string.Join(",", parsedRecord.PolicyOverrideReasons)
                    : null,
                HeaderFrom = parsedRecord.HeaderFrom,
                EnvelopeFrom = parsedRecord.EnvelopeFrom,
                EnvelopeTo = parsedRecord.EnvelopeTo
            };

            foreach (var dkim in parsedRecord.DkimResults)
            {
                record.DkimAuthResults.Add(new DkimAuthResult
                {
                    Id = Guid.NewGuid(),
                    Domain = dkim.Domain,
                    Selector = dkim.Selector,
                    Result = DmarcEnumMapper.MapDkimResult(dkim.Result),
                    HumanResult = dkim.HumanResult
                });
            }

            foreach (var spf in parsedRecord.SpfResults)
            {
                record.SpfAuthResults.Add(new SpfAuthResult
                {
                    Id = Guid.NewGuid(),
                    Domain = spf.Domain,
                    Scope = DmarcEnumMapper.MapSpfScope(spf.Scope),
                    Result = DmarcEnumMapper.MapSpfResult(spf.Result)
                });
            }

            report.Records.Add(record);
        }

        return report;
    }

    private async Task EvaluateSpfAsync(string domainName, DmarcRecord record, CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(record.SourceIp, out var sourceIp))
        {
            return;
        }

        SpfEvaluationOutcome outcome;
        try
        {
            outcome = await spfEvaluator.EvaluateAsync(domainName, sourceIp, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SPF re-evaluation failed for {Domain} / {SourceIp}", domainName, record.SourceIp);
            return;
        }

        var recomputedIsPass = outcome.Result == SpfResultCode.Pass;
        var reportedIsPass = record.PolicyEvaluatedSpf == DmarcPolicyResult.Pass;
        var discrepancy = recomputedIsPass != reportedIsPass;

        var evaluation = new SpfEvaluationResult
        {
            Id = Guid.NewGuid(),
            DmarcRecordId = record.Id,
            EvaluatedUtc = DateTime.UtcNow,
            LiveSpfRecordText = outcome.RecordText,
            RecomputedResult = outcome.Result,
            MatchedMechanism = outcome.MatchedMechanism,
            LookupCount = outcome.LookupCount,
            DiscrepancyFlag = discrepancy,
            DiscrepancyNote = discrepancy
                ? $"Report recorded SPF-aligned={reportedIsPass}; live DNS re-check today yields {outcome.Result}."
                : null
        };

        // Explicitly Add rather than relying only on the navigation-property assignment below: a new
        // dependent with a pre-set (non-default) Guid key, discovered via change-tracker graph traversal
        // instead of an explicit Add(), gets misidentified as Modified rather than Added.
        db.SpfEvaluationResults.Add(evaluation);
        record.SpfEvaluation = evaluation;
    }

    private async Task CheckDkimSelectorsAsync(DmarcRecord record, CancellationToken cancellationToken)
    {
        foreach (var dkimResult in record.DkimAuthResults)
        {
            if (string.IsNullOrEmpty(dkimResult.Selector))
            {
                continue;
            }

            DkimSelectorCheckResult checkResult;
            try
            {
                checkResult = await dkimSelectorChecker.CheckAsync(dkimResult.Domain, dkimResult.Selector, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "DKIM selector check failed for {Selector}._domainkey.{Domain}", dkimResult.Selector, dkimResult.Domain);
                continue;
            }

            var stale = dkimResult.Result == DkimResultCode.Pass
                && checkResult.Status is DkimSelectorStatus.Missing or DkimSelectorStatus.Revoked;

            var selectorCheck = new DkimSelectorCheck
            {
                Id = Guid.NewGuid(),
                DkimAuthResultId = dkimResult.Id,
                CheckedUtc = DateTime.UtcNow,
                Status = checkResult.Status,
                RawTxtRecord = checkResult.RawTxtRecord,
                StaleFlag = stale
            };

            db.DkimSelectorChecks.Add(selectorCheck);
            dkimResult.SelectorCheck = selectorCheck;
        }
    }

    /// <summary>
    /// Upserts the (domain, source IP) reputation aggregate: rolls the record's volume into the
    /// cumulative aligned-pass history, refreshes the current SPF/DKIM standing from what was just
    /// computed above, performs a forward-confirmed reverse-DNS check, and re-scores the overall
    /// <see cref="SenderLegitimacyLevel"/> via <see cref="SenderLegitimacyEvaluator"/>.
    /// </summary>
    private async Task UpsertSenderReputationAsync(
        Domain domain,
        DmarcRecord record,
        IReadOnlyList<VerifiedSenderOverride> overrides,
        Dictionary<string, SenderReputation> reputationCache,
        CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(record.SourceIp, out var sourceIp))
        {
            return;
        }

        if (!reputationCache.TryGetValue(record.SourceIp, out var reputation))
        {
            reputation = await db.SenderReputations
                .FirstOrDefaultAsync(r => r.DomainId == domain.Id && r.SourceIp == record.SourceIp, cancellationToken);

            var isNew = reputation is null;
            reputation ??= new SenderReputation
            {
                Id = Guid.NewGuid(),
                DomainId = domain.Id,
                SourceIp = record.SourceIp,
                FirstSeenUtc = DateTime.UtcNow
            };

            if (isNew)
            {
                db.SenderReputations.Add(reputation);
            }

            reputationCache[record.SourceIp] = reputation;
        }

        reputation.LastSeenUtc = DateTime.UtcNow;
        reputation.TotalVolume += record.Count;

        var isAlignedPass = record.PolicyEvaluatedDkim == DmarcPolicyResult.Pass || record.PolicyEvaluatedSpf == DmarcPolicyResult.Pass;
        if (isAlignedPass)
        {
            reputation.AlignedPassVolume += record.Count;
        }

        reputation.CurrentSpfResult = record.SpfEvaluation?.RecomputedResult ?? SpfResultCode.None;
        reputation.CurrentDkimStale = record.DkimAuthResults.Any(d => d.SelectorCheck is { StaleFlag: true });
        reputation.IsOverrideMatch = SenderOverrideMatcher.Matches(record, overrides);

        string? ptrHostname = null;
        var forwardConfirmed = false;
        try
        {
            ptrHostname = await reverseDnsResolver.GetPtrHostnameAsync(sourceIp, cancellationToken);
            if (!string.IsNullOrEmpty(ptrHostname))
            {
                var forwardAddresses = sourceIp.AddressFamily == AddressFamily.InterNetwork
                    ? await forwardDnsResolver.ResolveAAsync(ptrHostname, cancellationToken)
                    : await forwardDnsResolver.ResolveAaaaAsync(ptrHostname, cancellationToken);
                forwardConfirmed = forwardAddresses.Any(a => a.Equals(sourceIp));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Reverse DNS check failed for {SourceIp}", record.SourceIp);
        }

        reputation.ReverseDnsHostname = ptrHostname;
        reputation.ForwardConfirmed = forwardConfirmed;

        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: reputation.AlignedPassRatio,
            CurrentSpfResult: reputation.CurrentSpfResult,
            CurrentDkimStale: reputation.CurrentDkimStale,
            HasReverseDns: !string.IsNullOrEmpty(ptrHostname),
            ForwardConfirmed: forwardConfirmed,
            IsOverrideMatch: reputation.IsOverrideMatch);

        reputation.LegitimacyLevel = SenderLegitimacyEvaluator.Evaluate(signals);
        reputation.LastEvaluatedUtc = DateTime.UtcNow;
    }
}
