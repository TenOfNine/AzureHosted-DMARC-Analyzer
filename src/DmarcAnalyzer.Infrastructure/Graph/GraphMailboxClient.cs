using DmarcAnalyzer.Core.Abstractions;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.MailFolders.Item.Messages.Delta;
using Microsoft.Kiota.Abstractions;

namespace DmarcAnalyzer.Infrastructure.Graph;

/// <summary>
/// Microsoft Graph app-only (client-credentials) mailbox access. Reads use delta queries so repeated
/// polls only fetch messages new since the last run. Only well-known folder names (e.g. "inbox") or a
/// raw folder ID are supported for <c>mailFolder</c> — resolving arbitrary folder display names to IDs
/// is left out of MVP scope.
/// </summary>
public class GraphMailboxClient(GraphClientFactory clientFactory) : IGraphMailboxClient
{
    private static readonly string[] MessageSelectFields = ["id", "subject", "receivedDateTime", "hasAttachments"];

    public async Task<MailboxPollResult> ListNewMessagesAsync(string mailboxUpn, string mailFolder, string? deltaLink, CancellationToken cancellationToken = default)
    {
        var client = await RequireClientAsync(cancellationToken);
        var folder = string.IsNullOrWhiteSpace(mailFolder) ? "inbox" : mailFolder;

        var messages = new List<GraphMessageSummary>();
        string? nextDeltaLink;

        if (!string.IsNullOrEmpty(deltaLink))
        {
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.GET,
                URI = new Uri(deltaLink)
            };
            var page = await client.RequestAdapter.SendAsync(
                requestInfo,
                DeltaGetResponse.CreateFromDiscriminatorValue,
                cancellationToken: cancellationToken);
            nextDeltaLink = await DrainDeltaPagesAsync(client, page, messages, cancellationToken);
        }
        else
        {
            var page = await client.Users[mailboxUpn].MailFolders[folder].Messages.Delta.GetAsDeltaGetResponseAsync(config =>
            {
                config.QueryParameters.Select = MessageSelectFields;
            }, cancellationToken);
            nextDeltaLink = await DrainDeltaPagesAsync(client, page, messages, cancellationToken);
        }

        return new MailboxPollResult(messages, nextDeltaLink);
    }

    private static async Task<string?> DrainDeltaPagesAsync(
        GraphServiceClient client,
        DeltaGetResponse? page,
        List<GraphMessageSummary> messages,
        CancellationToken cancellationToken)
    {
        string? deltaLink = null;

        while (page is not null)
        {
            foreach (var item in page.Value ?? [])
            {
                if (item is Message message && message.Id is not null)
                {
                    messages.Add(new GraphMessageSummary(
                        message.Id,
                        message.Subject ?? string.Empty,
                        message.ReceivedDateTime ?? DateTimeOffset.UtcNow,
                        message.HasAttachments ?? false));
                }
            }

            deltaLink = page.OdataDeltaLink;

            if (page.OdataNextLink is not null)
            {
                var nextRequest = new RequestInformation
                {
                    HttpMethod = Method.GET,
                    URI = new Uri(page.OdataNextLink)
                };
                page = await client.RequestAdapter.SendAsync(
                    nextRequest,
                    DeltaGetResponse.CreateFromDiscriminatorValue,
                    cancellationToken: cancellationToken);
            }
            else
            {
                page = null;
            }
        }

        return deltaLink;
    }

    public async Task<IReadOnlyList<GraphAttachment>> GetAttachmentsAsync(string mailboxUpn, string messageId, CancellationToken cancellationToken = default)
    {
        var client = await RequireClientAsync(cancellationToken);
        var attachments = await client.Users[mailboxUpn].Messages[messageId].Attachments.GetAsync(cancellationToken: cancellationToken);

        var result = new List<GraphAttachment>();
        foreach (var attachment in attachments?.Value ?? [])
        {
            if (attachment is FileAttachment { ContentBytes: not null } fileAttachment)
            {
                result.Add(new GraphAttachment(
                    fileAttachment.Name ?? "attachment",
                    fileAttachment.ContentType,
                    fileAttachment.ContentBytes));
            }
        }

        return result;
    }

    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.CreateClientAsync(cancellationToken);
        if (client is null)
        {
            return false;
        }

        try
        {
            await client.Organization.GetAsync(config =>
            {
                config.QueryParameters.Top = 1;
                config.QueryParameters.Select = ["id"];
            }, cancellationToken);
            return true;
        }
        catch (ServiceException)
        {
            return false;
        }
    }

    private async Task<GraphServiceClient> RequireClientAsync(CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateClientAsync(cancellationToken);
        if (client is null)
        {
            throw new InvalidOperationException("Microsoft Graph connection is not configured. Complete the setup wizard first.");
        }

        return client;
    }
}
