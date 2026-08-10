namespace DmarcAnalyzer.Tests.Dmarc;

/// <summary>Sample RFC 7489 aggregate report XML used across parser and attachment-extraction tests.</summary>
public static class DmarcXmlFixtures
{
    public const string ValidFull = """
        <?xml version="1.0" encoding="UTF-8" ?>
        <feedback>
          <report_metadata>
            <org_name>Example Receiver Inc.</org_name>
            <email>dmarc-noreply@example-receiver.com</email>
            <extra_contact_info>https://example-receiver.com/dmarc/support</extra_contact_info>
            <report_id>abc123-report-001</report_id>
            <date_range>
              <begin>1700000000</begin>
              <end>1700086400</end>
            </date_range>
          </report_metadata>
          <policy_published>
            <domain>contoso.com</domain>
            <adkim>r</adkim>
            <aspf>s</aspf>
            <p>reject</p>
            <sp>reject</sp>
            <pct>100</pct>
            <fo>1</fo>
          </policy_published>
          <record>
            <row>
              <source_ip>203.0.113.5</source_ip>
              <count>3</count>
              <policy_evaluated>
                <disposition>none</disposition>
                <dkim>pass</dkim>
                <spf>pass</spf>
                <reason>
                  <type>local_policy</type>
                </reason>
              </policy_evaluated>
            </row>
            <identifiers>
              <header_from>contoso.com</header_from>
              <envelope_from>contoso.com</envelope_from>
              <envelope_to>recipient.example.com</envelope_to>
            </identifiers>
            <auth_results>
              <dkim>
                <domain>contoso.com</domain>
                <selector>selector1</selector>
                <result>pass</result>
                <human_result></human_result>
              </dkim>
              <spf>
                <domain>contoso.com</domain>
                <scope>mfrom</scope>
                <result>pass</result>
              </spf>
            </auth_results>
          </record>
          <record>
            <row>
              <source_ip>198.51.100.9</source_ip>
              <count>1</count>
              <policy_evaluated>
                <disposition>reject</disposition>
                <dkim>fail</dkim>
                <spf>fail</spf>
              </policy_evaluated>
            </row>
            <identifiers>
              <header_from>contoso.com</header_from>
            </identifiers>
            <auth_results>
              <dkim>
                <domain>contoso.com</domain>
                <selector>selector1</selector>
                <result>fail</result>
              </dkim>
              <spf>
                <domain>contoso.com</domain>
                <scope>mfrom</scope>
                <result>fail</result>
              </spf>
            </auth_results>
          </record>
        </feedback>
        """;

    /// <summary>Only the fields RFC 7489 marks as required — omits comment, extra_contact_info, sp, pct, fo, dkim selector/human_result.</summary>
    public const string ValidMinimal = """
        <?xml version="1.0" encoding="UTF-8" ?>
        <feedback>
          <report_metadata>
            <org_name>Minimal Receiver</org_name>
            <email>noreply@minimal-receiver.example</email>
            <report_id>minimal-001</report_id>
            <date_range>
              <begin>1700000000</begin>
              <end>1700086400</end>
            </date_range>
          </report_metadata>
          <policy_published>
            <domain>contoso.com</domain>
            <p>none</p>
          </policy_published>
          <record>
            <row>
              <source_ip>203.0.113.5</source_ip>
              <count>1</count>
              <policy_evaluated>
                <disposition>none</disposition>
                <dkim>pass</dkim>
                <spf>pass</spf>
              </policy_evaluated>
            </row>
            <identifiers>
              <header_from>contoso.com</header_from>
            </identifiers>
            <auth_results>
              <spf>
                <domain>contoso.com</domain>
                <result>pass</result>
              </spf>
            </auth_results>
          </record>
        </feedback>
        """;

    public const string MalformedXml = """
        <?xml version="1.0" encoding="UTF-8" ?>
        <feedback>
          <report_metadata>
            <org_name>Broken Receiver
          </report_metadata>
        </feedback
        """;

    public const string MissingRequiredField = """
        <?xml version="1.0" encoding="UTF-8" ?>
        <feedback>
          <report_metadata>
            <org_name>Missing Fields Inc.</org_name>
            <email>noreply@missing.example</email>
            <date_range>
              <begin>1700000000</begin>
              <end>1700086400</end>
            </date_range>
          </report_metadata>
          <policy_published>
            <domain>contoso.com</domain>
            <p>none</p>
          </policy_published>
        </feedback>
        """;
}
