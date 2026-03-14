namespace UPMS.Data.ProblemRequests;

public sealed class ProblemRequestValidationException : Exception
{
    public ProblemRequestValidationException(string fieldName, string message)
        : base(message)
    {
        FieldName = string.IsNullOrWhiteSpace(fieldName) ? "problemRequest" : fieldName;
    }

    public string FieldName { get; }
}
