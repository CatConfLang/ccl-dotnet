namespace CclDotnet;

/// <summary>
/// Exception thrown when CCL parsing encounters invalid input.
/// </summary>
public class CclParseException : Exception
{
  public CclParseException(string message) : base(message) { }
  public CclParseException(string message, Exception innerException) : base(message, innerException) { }
}
