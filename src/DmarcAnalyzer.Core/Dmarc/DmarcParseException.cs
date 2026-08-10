namespace DmarcAnalyzer.Core.Dmarc;

public class DmarcParseException(string message, Exception? inner = null) : Exception(message, inner);
