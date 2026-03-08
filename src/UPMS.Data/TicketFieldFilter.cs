namespace UPMS.Data;

public record TicketFieldFilter(string FieldName, string Value, CanonicalFieldDataType? DataType = null);
