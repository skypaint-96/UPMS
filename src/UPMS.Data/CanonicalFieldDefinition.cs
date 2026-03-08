namespace UPMS.Data;

public class CanonicalFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public CanonicalFieldDataType DataType { get; set; } = CanonicalFieldDataType.Text;
    public bool IsSystemRequired { get; set; }
}
