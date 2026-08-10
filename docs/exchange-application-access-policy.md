# Scoping the app registration to only the DMARC shared mailboxes

The app registration used by DMARC Analyzer is granted the Microsoft Graph **application**
permission `Mail.Read`. Application permissions are tenant-wide by default — without further
restriction, this app registration's client credentials could read **every mailbox** in the
tenant, not just the shared mailboxes you intend to monitor.

Exchange Online's **Application Access Policy** feature restricts an app registration's Graph
mailbox access to an explicit allow-list of mailboxes. This cannot be configured from the
DMARC Analyzer app itself or from Bicep — it's an Exchange Online-side control, applied once via
Exchange Online PowerShell. Do this as part of onboarding, after creating the app registration and
before (or shortly after) completing the in-app setup wizard.

## Steps

1. **Connect to Exchange Online PowerShell** (requires the ExchangeOnlineManagement module and an
   account with Exchange admin rights):

   ```powershell
   Install-Module -Name ExchangeOnlineManagement -Scope CurrentUser
   Connect-ExchangeOnline -UserPrincipalName admin@yourtenant.onmicrosoft.com
   ```

2. **Create a mail-enabled security group** containing the shared mailboxes that receive DMARC
   reports (Application Access Policies target a group, not individual mailboxes directly):

   ```powershell
   New-DistributionGroup -Name "DMARC Report Mailboxes" -Alias dmarc-report-mailboxes -Type Security
   Add-DistributionGroupMember -Identity "DMARC Report Mailboxes" -Member dmarc-reports@contoso.com
   # Repeat Add-DistributionGroupMember for every shared mailbox configured in the setup wizard.
   ```

3. **Create the Application Access Policy**, scoping the app registration's Client ID (the same
   one entered in the setup wizard's Graph Connection step) to only that group:

   ```powershell
   New-ApplicationAccessPolicy `
     -AppId "<the app registration's Client ID>" `
     -PolicyScopeGroupId "dmarc-report-mailboxes@yourtenant.onmicrosoft.com" `
     -AccessRight RestrictAccess `
     -Description "DMARC Analyzer: restrict to DMARC report shared mailboxes only"
   ```

4. **Verify** the policy is scoping access correctly — this should report `Access Check Result:
   Granted` for a mailbox in the group and `Access Check Result: Denied` for one outside it:

   ```powershell
   Test-ApplicationAccessPolicy -AppId "<the app registration's Client ID>" -Identity dmarc-reports@contoso.com
   Test-ApplicationAccessPolicy -AppId "<the app registration's Client ID>" -Identity some-other-mailbox@contoso.com
   ```

5. When adding a new shared mailbox in DMARC Analyzer's setup wizard later, remember to also add
   it to the `DMARC Report Mailboxes` group — the app-side setup wizard configures which mailboxes
   *DMARC Analyzer polls*, but this group configures which mailboxes *the app registration is
   even allowed to reach* at the Graph API level. The two lists should stay in sync.

Application Access Policies can take up to ~30 minutes to propagate after creation or
modification (per Microsoft's documented behavior), so don't be alarmed if `Test-ApplicationAccessPolicy`
doesn't reflect a very recent change immediately.
