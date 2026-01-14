using System.Data;

namespace UPMS.Worker.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
